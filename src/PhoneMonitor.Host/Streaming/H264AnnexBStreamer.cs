using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using PhoneMonitor.Host.Windows;
using SIPSorcery.Net;

namespace PhoneMonitor.Host.Streaming
{
    public sealed class H264AnnexBStreamer
    {
        private string resolvedEncoderName;
        private bool encoderNameChecked;
        private readonly DisplayFrameSource frameSource;
        private readonly H264StreamMetrics metrics;
        private readonly object activeStreamSync = new object();
        private CancellationTokenSource activeStreamCts;
        private string resolvedFfmpegPath;
        private bool ffmpegPathChecked;

        public H264AnnexBStreamer(DisplayFrameSource frameSource, H264StreamMetrics metrics)
        {
            this.frameSource = frameSource;
            this.metrics = metrics;
        }

        public bool IsAvailable => ResolveFfmpegPath() != null;

        public string EncoderDescription => resolvedEncoderName != null ? $"ffmpeg/{resolvedEncoderName}" : "ffmpeg/libx264";

        public async Task StreamAsync(WebSocket socket, string deviceName, int fps, int quality, CancellationToken cancellationToken)
        {
            var ffmpegPath = ResolveFfmpegPath();
            if (ffmpegPath == null)
            {
                throw new InvalidOperationException("ffmpeg.exe was not found.");
            }

            fps = Math.Max(1, Math.Min(60, fps));
            quality = Math.Max(25, Math.Min(85, quality));

            using var holder = new ReusableBitmapHolder();
            var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var streamToken = streamCts.Token;
            string stopError = null;
            Process process = null;
            Task errorTask = null;

            try
            {
                using var firstFrame = frameSource.CaptureBitmapFrame(deviceName, holder);
                var width = firstFrame.Bitmap.Width;
                var height = firstFrame.Bitmap.Height;
                var bitrateKbps = EstimateBitrateKbps(width, height, fps, quality);
                var encoderName = ResolveEncoderName(ffmpegPath);
                
                process = StartFfmpeg(ffmpegPath, encoderName, width, height, fps, bitrateKbps);
                var frameBytes = width * height * 4;
                var rawFrame = new byte[frameBytes];
                
                ReplaceActiveStream(streamCts);
                metrics.Start(width, height, fps, quality, bitrateKbps);
                
                var outputTask = RelayEncodedOutputAsync(process.StandardOutput.BaseStream, socket, metrics, streamToken);
                errorTask = DrainErrorAsync(process.StandardError);

                await WriteBitmapFrameAsync(firstFrame.Bitmap, process.StandardInput.BaseStream, rawFrame, streamToken);
                metrics.RecordQueuedFrame();
                
                await PumpFramesAsync(
                    process,
                    deviceName,
                    width,
                    height,
                    fps,
                    rawFrame,
                    firstFrame.Fingerprint,
                    firstFrame.HasCursor,
                    firstFrame.CursorX,
                    firstFrame.CursorY,
                    outputTask,
                    holder,
                    false,
                    streamToken);
                await outputTask;
                await errorTask;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && streamToken.IsCancellationRequested))
            {
                stopError = ex.Message;
                throw;
            }
            finally
            {
                metrics.Stop(stopError);
                if (process != null)
                {
                    TryClose(process.StandardInput.BaseStream);
                    TryKill(process);
                    process.Dispose();
                }
                ClearActiveStream(streamCts);
                streamCts.Dispose();
                try
                {
                    if (errorTask != null)
                    {
                        await errorTask;
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Encodes the virtual display as Annex-B H.264 and sends complete
        /// access units through an already-negotiated WebRTC video track.
        /// The browser receives standard RTP/SRTP rather than raw WebSocket
        /// bytes, so Safari can use its hardware H.264 decoder.
        /// </summary>
        public async Task StreamToWebRtcAsync(RTCPeerConnection peer, string deviceName, int fps, int quality, CancellationToken cancellationToken)
        {
            var ffmpegPath = ResolveFfmpegPath();
            if (ffmpegPath == null)
            {
                throw new InvalidOperationException("ffmpeg.exe was not found.");
            }

            fps = Math.Max(1, Math.Min(60, fps));
            quality = Math.Max(25, Math.Min(85, quality));

            using var holder = new ReusableBitmapHolder();
            Process process = null;
            Task errorTask = null;
            try
            {
                using var firstFrame = frameSource.CaptureBitmapFrame(deviceName, holder);
                var width = firstFrame.Bitmap.Width;
                var height = firstFrame.Bitmap.Height;
                var bitrateKbps = EstimateBitrateKbps(width, height, fps, quality);
                var encoderName = ResolveEncoderName(ffmpegPath);
                // Safari can offer more than one H.264 payload (different
                // profile/packetization combinations).  RTPSession.SendVideo
                // uses Single() by codec name and crashes in that case, so
                // resolve the negotiated payload once and send H.264 frames
                // directly with that payload ID.
                var h264PayloadTypeId = Convert.ToInt32(peer.GetSendingFormat(SDPMediaTypesEnum.video).ID);
                process = StartFfmpeg(ffmpegPath, encoderName, width, height, fps, bitrateKbps);

                var rawFrame = new byte[width * height * 4];
                metrics.Start(width, height, fps, quality, bitrateKbps);
                var outputTask = RelayEncodedOutputToWebRtcAsync(process.StandardOutput.BaseStream, peer, h264PayloadTypeId, fps, metrics, cancellationToken);
                errorTask = DrainErrorAsync(process.StandardError);

                await WriteBitmapFrameAsync(firstFrame.Bitmap, process.StandardInput.BaseStream, rawFrame, cancellationToken);
                await PumpFramesAsync(
                    process,
                    deviceName,
                    width,
                    height,
                    fps,
                    rawFrame,
                    firstFrame.Fingerprint,
                    firstFrame.HasCursor,
                    firstFrame.CursorX,
                    firstFrame.CursorY,
                    outputTask,
                    holder,
                    true,
                    cancellationToken);
                await outputTask;
                await errorTask;
            }
            finally
            {
                metrics.Stop();
                if (process != null)
                {
                    TryClose(process.StandardInput.BaseStream);
                    TryKill(process);
                    process.Dispose();
                }

                try
                {
                    if (errorTask != null)
                    {
                        await errorTask;
                    }
                }
                catch
                {
                }
            }
        }

        private void ReplaceActiveStream(CancellationTokenSource streamCts)
        {
            CancellationTokenSource previous;
            lock (activeStreamSync)
            {
                previous = activeStreamCts;
                activeStreamCts = streamCts;
            }

            try
            {
                previous?.Cancel();
            }
            catch
            {
            }
        }

        private void ClearActiveStream(CancellationTokenSource streamCts)
        {
            lock (activeStreamSync)
            {
                if (ReferenceEquals(activeStreamCts, streamCts))
                {
                    activeStreamCts = null;
                }
            }
        }

        private async Task PumpFramesAsync(
            Process process,
            string deviceName,
            int width,
            int height,
            int fps,
            byte[] rawFrame,
            DisplayFrameFingerprint lastSentFingerprint,
            bool lastSentHasCursor,
            int lastSentCursorX,
            int lastSentCursorY,
            Task outputTask,
            ReusableBitmapHolder holder,
            bool forceConstantCadence,
            CancellationToken cancellationToken)
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / fps);
            var maxIdleInterval = TimeSpan.FromMilliseconds(Math.Max(120, frameInterval.TotalMilliseconds * 4));
            var keepAliveInterval = TimeSpan.FromMilliseconds(220);
            var stopwatch = Stopwatch.StartNew();
            var nextDue = stopwatch.Elapsed + frameInterval;
            var lastSentAt = stopwatch.Elapsed;
            var idleStreak = 0;

            while (!cancellationToken.IsCancellationRequested && !outputTask.IsCompleted && !process.HasExited)
            {
                var delay = nextDue - stopwatch.Elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                using var frame = frameSource.CaptureBitmapFrame(deviceName, holder);
                if (frame.Bitmap.Width != width || frame.Bitmap.Height != height)
                {
                    throw new InvalidOperationException("Display size changed during H.264 streaming.");
                }

                var changeScore = lastSentFingerprint == null || frame.Fingerprint == null
                    ? 1
                    : frame.Fingerprint.DifferenceFrom(lastSentFingerprint);
                var cursorMoved = frame.HasCursor != lastSentHasCursor ||
                    (frame.HasCursor && (Math.Abs(frame.CursorX - lastSentCursorX) >= 1 || Math.Abs(frame.CursorY - lastSentCursorY) >= 1));
                var hasMeaningfulChange = frame.IsStatusFrame || cursorMoved || changeScore >= 0.00045;
                var shouldSend = forceConstantCadence || hasMeaningfulChange || stopwatch.Elapsed - lastSentAt >= keepAliveInterval;

                if (shouldSend)
                {
                    await WriteBitmapFrameAsync(frame.Bitmap, process.StandardInput.BaseStream, rawFrame, cancellationToken);
                    metrics.RecordQueuedFrame();
                    lastSentFingerprint = frame.Fingerprint;
                    lastSentHasCursor = frame.HasCursor;
                    lastSentCursorX = frame.CursorX;
                    lastSentCursorY = frame.CursorY;
                    lastSentAt = stopwatch.Elapsed;
                    idleStreak = hasMeaningfulChange ? 0 : Math.Min(idleStreak + 1, 10);
                }
                else
                {
                    metrics.RecordSkippedFrame();
                    idleStreak = Math.Min(idleStreak + 1, 10);
                }

                var nextDelay = forceConstantCadence || hasMeaningfulChange
                    ? frameInterval
                    : TimeSpan.FromMilliseconds(Math.Min(maxIdleInterval.TotalMilliseconds, frameInterval.TotalMilliseconds * (1.8 + idleStreak * 0.65)));
                // Advance an absolute deadline. Scheduling from "now" adds
                // capture and pipe-write time to every frame interval.
                nextDue += nextDelay;
                if (nextDue < stopwatch.Elapsed - nextDelay)
                {
                    // Skip missed slots instead of bursting after a stall.
                    nextDue = stopwatch.Elapsed;
                }
            }
        }

        private static Process StartFfmpeg(string ffmpegPath, string encoderName, int width, int height, int fps, int bitrateKbps)
        {
            var startInfo = new ProcessStartInfo(ffmpegPath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var bufferKbps = Math.Max(120, (int)Math.Ceiling(bitrateKbps * 2.0 / Math.Max(1, fps)));

            // Basic input arguments (common to all)
            AddArgs(startInfo,
                "-hide_banner",
                "-loglevel", "warning",
                "-fflags", "nobuffer",
                "-flags", "low_delay",
                "-f", "rawvideo",
                "-pixel_format", "bgra",
                "-video_size", $"{width}x{height}",
                "-framerate", fps.ToString(),
                "-i", "pipe:0",
                "-an",
                "-c:v", encoderName);

            // Encoder-specific low latency tuning (disabling B-frames and internal queues)
            if (string.Equals(encoderName, "h264_nvenc", StringComparison.OrdinalIgnoreCase))
            {
                // NVIDIA NVENC low latency settings
                AddArgs(startInfo,
                    "-preset", "p1",             // Lowest latency preset in modern NVENC
                    "-tune", "ull",             // Ultra Low Latency
                    "-delay", "0",              // Disable frame delay
                    "-zerolatency", "1",        // Force low latency mode
                    "-bf", "0",                 // No B-frames to prevent decoding delay
                    "-forced-idr", "1");
            }
            else if (string.Equals(encoderName, "h264_qsv", StringComparison.OrdinalIgnoreCase))
            {
                // Intel QuickSync Video low latency settings
                AddArgs(startInfo,
                    "-preset", "veryfast",
                    "-async_depth", "1",        // Lower thread/buffer async depth
                    "-bf", "0",
                    "-aud", "1");
            }
            else if (string.Equals(encoderName, "h264_amf", StringComparison.OrdinalIgnoreCase))
            {
                // AMD AMF low latency settings
                AddArgs(startInfo,
                    "-usage", "lowlatency",
                    "-bf", "0",
                    "-aud", "1");
            }
            else
            {
                // Fallback / CPU: libx264 low latency settings
                AddArgs(startInfo,
                    "-preset", "ultrafast",
                    "-tune", "zerolatency",
                    "-profile:v", "baseline",
                    "-bf", "0");
            }

            // Common output arguments
            AddArgs(startInfo,
                "-pix_fmt", "yuv420p",
                "-g", fps.ToString(),
                "-keyint_min", fps.ToString(),
                "-sc_threshold", "0",
                "-refs", "1",
                "-b:v", $"{bitrateKbps}k",
                "-maxrate", $"{bitrateKbps}k",
                "-bufsize", $"{bufferKbps}k");

            if (string.Equals(encoderName, "libx264", StringComparison.OrdinalIgnoreCase))
            {
                // libx264-specific parameters for Annex B output headers
                AddArgs(startInfo,
                    "-x264-params", $"aud=1:repeat-headers=1:keyint={fps}:min-keyint={fps}:scenecut=0:sync-lookahead=0:rc-lookahead=0:sliced-threads=0");
            }
            else if (string.Equals(encoderName, "h264_nvenc", StringComparison.OrdinalIgnoreCase))
            {
                // nvenc-specific repeat headers for client stream joining
                AddArgs(startInfo,
                    "-aud", "1",
                    "-surfaces", "2");
            }

            AddArgs(startInfo,
                "-flush_packets", "1",
                "-f", "h264",
                "pipe:1");

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = false
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Unable to start ffmpeg.");
            }

            return process;
        }

        /// <summary>
        /// Probes the available FFmpeg hardware encoders in priority order:
        /// h264_nvenc (NVIDIA) → h264_qsv (Intel Quick Sync) → h264_amf (AMD),
        /// and falls back to the software encoder libx264 if none succeed.
        /// The result is cached so probing only happens once per instance.
        /// </summary>
        private string ResolveEncoderName(string ffmpegPath)
        {
            if (encoderNameChecked)
            {
                return resolvedEncoderName;
            }

            encoderNameChecked = true;

            // Try hardware encoders in priority order.
            var candidates = new[] { "h264_nvenc", "h264_qsv", "h264_amf" };
            foreach (var candidate in candidates)
            {
                if (ProbeEncoder(ffmpegPath, candidate))
                {
                    resolvedEncoderName = candidate;
                    return resolvedEncoderName;
                }
            }

            // Fall back to the software encoder.
            resolvedEncoderName = "libx264";
            return resolvedEncoderName;
        }

        /// <summary>
        /// Runs a short FFmpeg probe to test whether the given encoder is available and functional.
        /// Returns true if FFmpeg exits with code 0 (success).
        /// </summary>
        private static bool ProbeEncoder(string ffmpegPath, string encoderName)
        {
            try
            {
                var startInfo = new ProcessStartInfo(ffmpegPath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Hardware encoders commonly reject tiny RGB frames even when
                // they are fully functional. Probe with a valid desktop-sized
                // YUV420 frame so NVENC/QSV/AMF are not falsely disabled.
                AddArgs(startInfo,
                    "-hide_banner",
                    "-loglevel", "error",
                    "-f", "lavfi",
                    "-i", "testsrc=duration=0.12:size=1280x720:rate=30",
                    "-vf", "format=yuv420p",
                    "-vcodec", encoderName,
                    "-f", "null",
                    "-");

                using var probe = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = false
                };

                if (!probe.Start())
                {
                    return false;
                }

                // Drain stdout/stderr to prevent deadlock on process exit.
                probe.StandardOutput.ReadToEnd();
                probe.StandardError.ReadToEnd();
                probe.WaitForExit();

                return probe.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void AddArgs(ProcessStartInfo startInfo, params string[] args)
        {
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        private static async Task RelayEncodedOutputAsync(Stream output, WebSocket socket, H264StreamMetrics metrics, CancellationToken cancellationToken)
        {
            var buffer = new byte[32 * 1024];
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var read = await output.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read <= 0)
                {
                    return;
                }

                await socket.SendAsync(new ArraySegment<byte>(buffer, 0, read), WebSocketMessageType.Binary, true, cancellationToken);
                metrics.RecordEncodedBytes(read);
            }
        }

        private static async Task RelayEncodedOutputToWebRtcAsync(Stream output, RTCPeerConnection peer, int h264PayloadTypeId, int fps, H264StreamMetrics metrics, CancellationToken cancellationToken)
        {
            var buffer = new byte[32 * 1024];
            var accessUnits = new H264AccessUnitAssembler();
            var nominalDuration = (uint)Math.Max(1, 90000 / Math.Max(1, fps));
            var clock = Stopwatch.StartNew();
            var lastSentAt = clock.Elapsed;
            var sentAny = false;

            while (!cancellationToken.IsCancellationRequested && peer.connectionState == RTCPeerConnectionState.connected)
            {
                var read = await output.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read <= 0)
                {
                    return;
                }
                metrics.RecordEncodedBytes(read);

                foreach (var accessUnit in accessUnits.Append(buffer, read))
                {
                    if (peer.connectionState != RTCPeerConnectionState.connected)
                    {
                        return;
                    }

                    var now = clock.Elapsed;
                    var elapsed = now - lastSentAt;
                    var duration = !sentAny || elapsed.TotalMilliseconds < 1
                        ? nominalDuration
                        : (uint)Math.Max(1, Math.Min(90000, Math.Round(elapsed.TotalSeconds * 90000.0)));
                    peer.SendH264Frame(duration, h264PayloadTypeId, accessUnit);
                    sentAny = true;
                    lastSentAt = now;
                }
            }
        }

        private sealed class H264AccessUnitAssembler
        {
            private readonly List<byte> buffer = new List<byte>(128 * 1024);

            public IEnumerable<byte[]> Append(byte[] source, int count)
            {
                for (var index = 0; index < count; index++)
                {
                    buffer.Add(source[index]);
                }

                var firstAud = FindAudStart(0);
                if (firstAud < 0)
                {
                    // Keep enough data to span a split start code but never let
                    // a malformed encoder stream grow without bound.
                    if (buffer.Count > 2 * 1024 * 1024)
                    {
                        buffer.RemoveRange(0, buffer.Count - 4);
                    }
                    yield break;
                }

                if (firstAud > 0)
                {
                    buffer.RemoveRange(0, firstAud);
                }

                while (true)
                {
                    var nextAud = FindAudStart(3);
                    if (nextAud <= 0)
                    {
                        yield break;
                    }

                    var accessUnit = buffer.GetRange(0, nextAud).ToArray();
                    buffer.RemoveRange(0, nextAud);
                    yield return accessUnit;
                }
            }

            private int FindAudStart(int offset)
            {
                for (var index = Math.Max(0, offset); index + 3 < buffer.Count; index++)
                {
                    if (buffer[index] != 0 || buffer[index + 1] != 0)
                    {
                        continue;
                    }

                    if (buffer[index + 2] == 1 && (buffer[index + 3] & 0x1F) == 9)
                    {
                        return index;
                    }

                    if (index + 4 < buffer.Count && buffer[index + 2] == 0 && buffer[index + 3] == 1 && (buffer[index + 4] & 0x1F) == 9)
                    {
                        return index;
                    }
                }

                return -1;
            }
        }

        private static async Task DrainErrorAsync(StreamReader error)
        {
            var buffer = new char[2048];
            while (await error.ReadAsync(buffer, 0, buffer.Length) > 0)
            {
            }
        }

        private static async Task WriteBitmapFrameAsync(Bitmap bitmap, Stream input, byte[] rawFrame, CancellationToken cancellationToken)
        {
            CopyBitmapBgra(bitmap, rawFrame);
            await input.WriteAsync(rawFrame, 0, bitmap.Width * bitmap.Height * 4, cancellationToken);
        }

        private static void CopyBitmapBgra(Bitmap bitmap, byte[] destination)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var rowBytes = bitmap.Width * 4;
                if (data.Stride == rowBytes)
                {
                    // Fast path: tightly packed rows — single bulk copy avoids per-row loop at 30-60 fps.
                    Marshal.Copy(data.Scan0, destination, 0, rowBytes * bitmap.Height);
                }
                else
                {
                    // Stride includes padding bytes — fall back to row-by-row copy.
                    for (var row = 0; row < bitmap.Height; row++)
                    {
                        var source = IntPtr.Add(data.Scan0, row * data.Stride);
                        Marshal.Copy(source, destination, row * rowBytes, rowBytes);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static int EstimateBitrateKbps(int width, int height, int fps, int quality)
        {
            var normalizedQuality = (quality - 25) / 60.0;
            // A desktop remote-view stream should stay interactive rather than
            // spend the whole Wi-Fi link on a sharp still frame.  The old
            // 0.035..0.09 bpp curve pushed an iPhone XS-sized 30fps stream to
            // the 12 Mbps cap, which made Safari's jitter buffer visibly grow.
            var bitsPerPixel = 0.010 + (normalizedQuality * 0.024);
            var bitrate = (int)Math.Round(width * height * fps * bitsPerPixel / 1000.0);
            return Math.Max(700, Math.Min(10000, bitrate));
        }

        private string ResolveFfmpegPath()
        {
            if (ffmpegPathChecked)
            {
                return resolvedFfmpegPath;
            }

            ffmpegPathChecked = true;
            resolvedFfmpegPath = ResolveConfiguredFfmpegPath()
                ?? ResolveFromPath("ffmpeg.exe")
                ?? ResolveFromPath("ffmpeg");
            return resolvedFfmpegPath;
        }

        private static string ResolveConfiguredFfmpegPath()
        {
            var value = Environment.GetEnvironmentVariable("VIBEDECK_FFMPEG")
                ?? Environment.GetEnvironmentVariable("PHONE_MONITOR_FFMPEG");
            return !string.IsNullOrWhiteSpace(value) && File.Exists(value) ? value : null;
        }

        private static string ResolveFromPath(string fileName)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            return path
                .Split(Path.PathSeparator)
                .Select(part => part.Trim().Trim('"'))
                .Where(part => part.Length > 0)
                .Select(part => Path.Combine(part, fileName))
                .FirstOrDefault(File.Exists);
        }

        private static void TryClose(Stream stream)
        {
            try
            {
                stream.Close();
            }
            catch
            {
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }
    }
}
