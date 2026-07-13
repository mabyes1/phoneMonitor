using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PhoneMonitor.Host.Streaming;
using PhoneMonitor.Host.Windows;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        private static async Task StreamDisplayAsync(
            WebSocket socket,
            DisplayFrameSource frameSource,
            string deviceName,
            int fps,
            int quality,
            CancellationToken cancellationToken)
        {
            var frameInterval = TimeSpan.FromMilliseconds(1000.0 / fps);
            var maxIdleInterval = TimeSpan.FromMilliseconds(Math.Max(450, frameInterval.TotalMilliseconds * 7));
            var keepAliveInterval = TimeSpan.FromSeconds(2.5);
            DisplayFrameFingerprint lastSentFingerprint = null;
            var lastSentAt = DateTimeOffset.MinValue;
            var idleStreak = 0;
            using var holder = new ReusableBitmapHolder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
                {
                    var requestedQuality = idleStreak > 0
                        ? Math.Max(38, quality - Math.Min(12, idleStreak * 2))
                        : quality;
                    using var frame = frameSource.CaptureBitmapFrame(deviceName, holder);
                    var changeScore = lastSentFingerprint == null || frame.Fingerprint == null
                        ? 1
                        : frame.Fingerprint.DifferenceFrom(lastSentFingerprint);
                    var hasMeaningfulChange = changeScore >= 0.0125;
                    var shouldSend = frame.IsStatusFrame
                        || hasMeaningfulChange
                        || (DateTimeOffset.UtcNow - lastSentAt) >= keepAliveInterval;

                    if (shouldSend)
                    {
                        var jpegBytes = JpegFrameEncoder.Encode(frame.Bitmap, requestedQuality);
                        await socket.SendAsync(jpegBytes, WebSocketMessageType.Binary, true, cancellationToken);
                        lastSentAt = DateTimeOffset.UtcNow;
                        lastSentFingerprint = frame.Fingerprint;
                        idleStreak = hasMeaningfulChange ? 0 : Math.Min(idleStreak + 1, 8);
                    }
                    else
                    {
                        idleStreak = Math.Min(idleStreak + 1, 8);
                    }

                    var nextDelay = hasMeaningfulChange
                        ? frameInterval
                        : TimeSpan.FromMilliseconds(Math.Min(maxIdleInterval.TotalMilliseconds, frameInterval.TotalMilliseconds * (1.8 + idleStreak * 0.55)));
                    await Task.Delay(nextDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        private static async Task ReceiveInputAsync(WebSocket socket, WindowsInputController input, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested || socket.State != WebSocketState.Open)
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client closed", cancellationToken);
                        }
                        catch (Exception) when (cancellationToken.IsCancellationRequested || socket.State != WebSocketState.Open)
                        {
                        }
                    }
                    return;
                }

                var payload = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var inputEvent = JsonSerializer.Deserialize<InputEvent>(payload, SocketJsonOptions);
                    if (!input.Apply(inputEvent))
                    {
                        Console.WriteLine($"ignored input {inputEvent?.Type} x={inputEvent?.X:0.000} y={inputEvent?.Y:0.000}");
                    }
                }
                catch (JsonException)
                {
                    Console.WriteLine($"invalid input payload: {payload}");
                }
            }
        }
    }
}
