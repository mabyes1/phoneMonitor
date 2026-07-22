using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PhoneMonitor.Host.Streaming;
using PhoneMonitor.Host.Windows;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        // Live display/input streaming surface: JPEG-over-WebSocket display,
        // WebRTC H.264 signalling, and the input channel. Extracted verbatim from
        // Startup.cs (no behavior change); the runtime loops StreamDisplayAsync /
        // ReceiveInputAsync live in this same file, and ParseInt / SocketJsonOptions
        // remain on the Startup partial class.
        private static void MapStreamingEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.Map("/ws/display", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                if (!await RequireTrustedDeviceAsync(context))
                {
                    return;
                }

                var deviceName = context.Request.Query["deviceName"].ToString();
                var fps = ParseInt(context.Request.Query["fps"], 10, 1, 60);
                var quality = ParseInt(context.Request.Query["quality"], 55, 25, 85);
                var frameSource = context.RequestServices.GetRequiredService<DisplayFrameSource>();
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await StreamDisplayAsync(socket, frameSource, deviceName, fps, quality, context.RequestAborted);
            });

            endpoints.MapPost("/api/stream/webrtc/offer", async context =>
            {
                if (!await RequireTrustedDeviceAsync(context))
                {
                    return;
                }

                var request = await JsonSerializer.DeserializeAsync<WebRtcOfferRequest>(context.Request.Body, SocketJsonOptions)
                    ?? new WebRtcOfferRequest();
                var webrtc = context.RequestServices.GetRequiredService<WebRtcH264Service>();
                if (!webrtc.IsAvailable)
                {
                    context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        error = "WebRTC H.264 requires ffmpeg.exe on the Host."
                    }));
                    return;
                }

                try
                {
                    var answer = await webrtc.CreateAnswerAsync(
                        request.Sdp,
                        request.DeviceName ?? string.Empty,
                        Math.Max(1, Math.Min(60, request.Fps)),
                        Math.Max(25, Math.Min(85, request.Quality)),
                        context.RequestAborted);
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(answer));
                }
                catch (ArgumentException error)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error.Message }));
                }
                catch (Exception error)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error.Message }));
                }
            });

            endpoints.Map("/ws/input", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                if (!await RequireTrustedDeviceAsync(context))
                {
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var input = context.RequestServices.GetRequiredService<WindowsInputController>();
                await ReceiveInputAsync(socket, input, context.RequestAborted);
            });
        }

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
