using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneMonitor.Host.Connect;
using PhoneMonitor.Host.Display;
using PhoneMonitor.Host.Dashboard;
using PhoneMonitor.Host.Quotas;
using PhoneMonitor.Host.Security;
using PhoneMonitor.Host.Sideboard;
using PhoneMonitor.Host.Streaming;
using PhoneMonitor.Host.Windows;
using QRCoder;

namespace PhoneMonitor.Host
{
    public class Startup
    {
        private static readonly JsonSerializerOptions SocketJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DisplayCatalog>();
            services.AddSingleton<DisplayFrameSource>();
            services.AddSingleton<H264StreamMetrics>();
            services.AddSingleton<H264AnnexBStreamer>();
            services.AddSingleton<WebRtcH264Service>();
            services.AddSingleton<DisplayViewerTracker>();
            services.AddSingleton<WindowsInputController>();
            services.AddSingleton<DeckWindowLauncher>();
            services.AddSingleton<DisplayModeController>();
            services.AddSingleton<VirtualDisplayController>();
            services.AddSingleton<ConnectInfoProvider>();
            services.AddSingleton<GlanceBoardProxy>();
            services.AddSingleton<AiQuotaService>();
            services.AddSingleton<DashboardEventHub>();
            services.AddHostedService<DashboardChangeMonitor>();
            services.AddSingleton<ActionTokenService>();
            services.AddSingleton<DeviceTrustService>();
            services.AddSingleton<HostAccessAuthService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(15)
            });
            app.UseStaticFiles();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/health", async context =>
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        status = "ok",
                        app = "PhoneMonitor.Host",
                        transport = "wifi-websocket-jpeg"
                    }));
                });

                endpoints.MapGet("/api/auth/status", async context =>
                {
                    var auth = context.RequestServices.GetRequiredService<HostAccessAuthService>();
                    var local = IsLocalRequest(context);
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        enabled = auth.Enabled,
                        authenticated = local || auth.IsAuthenticated(context),
                        required = auth.Enabled && !local,
                        httpsRequired = auth.Enabled && !local && !context.Request.IsHttps
                    }));
                });

                endpoints.MapPost("/api/auth/login", async context =>
                {
                    var auth = context.RequestServices.GetRequiredService<HostAccessAuthService>();
                    var local = IsLocalRequest(context);
                    var request = await JsonSerializer.DeserializeAsync<HostLoginRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new HostLoginRequest();

                    if (auth.Enabled && !local && !context.Request.IsHttps)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "遠端登入必須使用 HTTPS。" }));
                        return;
                    }

                    var result = auth.Login(request.Password, GetRemoteAddress(context));
                    if (result.Success)
                    {
                        context.Response.Cookies.Append(
                            HostAccessAuthService.CookieName,
                            result.SessionToken,
                            new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = context.Request.IsHttps,
                                SameSite = SameSiteMode.Lax,
                                Path = "/",
                                MaxAge = result.SessionLifetime,
                                IsEssential = true
                            });
                    }

                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = result.Success,
                        message = result.Message
                    }));
                });

                endpoints.MapPost("/api/auth/logout", async context =>
                {
                    context.RequestServices.GetRequiredService<HostAccessAuthService>().Logout(context);
                    context.Response.Cookies.Delete(HostAccessAuthService.CookieName, new CookieOptions { Path = "/", Secure = context.Request.IsHttps });
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                });

                endpoints.MapGet("/api/connect", async context =>
                {
                    var provider = context.RequestServices.GetRequiredService<ConnectInfoProvider>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(provider.Get(context.Request)));
                });

                endpoints.MapGet("/api/stream/capabilities", async context =>
                {
                    context.Response.ContentType = "application/json";
                    await WriteStreamCapabilitiesAsync(context);
                });

                endpoints.MapGet("/api/session", async context =>
                {
                    if (!IsLocalRequest(context) &&
                        !context.RequestServices.GetRequiredService<HostAccessAuthService>().IsAuthenticated(context))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Remote login required." }));
                        return;
                    }

                    var token = context.RequestServices.GetRequiredService<ActionTokenService>();
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        actionToken = token.Token,
                        actionHeader = ActionTokenService.HeaderName,
                        deviceHeader = DeviceTrustService.HeaderName
                    }));
                });

                endpoints.MapGet("/api/devices/status", async context =>
                {
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var isLocal = IsLocalRequest(context);
                    var hostAuthenticated = context.RequestServices.GetRequiredService<HostAccessAuthService>().IsAuthenticated(context);
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(devices.GetStatus(
                        ReadDeviceToken(context),
                        GetRemoteAddress(context),
                        context.Request.Headers["User-Agent"].FirstOrDefault(),
                        isLocal,
                        hostAuthenticated)));
                });

                endpoints.MapPost("/api/devices/pairing/start", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var request = await JsonSerializer.DeserializeAsync<PairingStartRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingStartRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var connectInfo = context.RequestServices.GetRequiredService<ConnectInfoProvider>().Get(context.Request);
                    var pairing = devices.StartPairing(request.Name);
                    // One path: HTTPS when the local cert exists (iPhone needs secure context for wake lock / PWA).
                    // HTTP only if HTTPS is not configured on this PC.
                    var pairingBase = connectInfo.HttpsAvailable ? connectInfo.HttpsUrl : connectInfo.HttpUrl;
                    var pairingUrl = BuildPairingUrl(pairingBase, pairing);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        pairingId = pairing.PairingId,
                        expiresAt = pairing.ExpiresAt,
                        pairingUrl,
                        httpsRequired = connectInfo.HttpsAvailable,
                        qrSvg = BuildQrSvg(pairingUrl)
                    }));
                });

                endpoints.MapPost("/api/devices/pairing/complete", async context =>
                {
                    var request = await JsonSerializer.DeserializeAsync<PairingCompleteRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingCompleteRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.CompletePairing(
                        request.PairingId,
                        request.PairingSecret,
                        context.Request.Headers["User-Agent"].FirstOrDefault(),
                        GetRemoteAddress(context));
                    if (result.Success && !string.IsNullOrWhiteSpace(result.DeviceToken))
                    {
                        // Cookie helps iPhone Home Screen Web share auth with Safari more reliably than localStorage alone.
                        context.Response.Cookies.Append(
                            DeviceTrustService.CookieName,
                            result.DeviceToken,
                            new CookieOptions
                            {
                                HttpOnly = false,
                                Secure = context.Request.IsHttps,
                                SameSite = SameSiteMode.Lax,
                                Path = "/",
                                MaxAge = TimeSpan.FromDays(400),
                                IsEssential = true
                            });
                    }

                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/revoke", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var request = await JsonSerializer.DeserializeAsync<DeviceRevokeRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new DeviceRevokeRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.RevokeDevice(request.DeviceId);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/clear", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.ClearDevices();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/qr.svg", async context =>
                {
                    var provider = context.RequestServices.GetRequiredService<ConnectInfoProvider>();
                    var connectInfo = provider.Get(context.Request);
                    await WriteQrSvgAsync(context, connectInfo.PreferredUrl);
                });

                endpoints.MapGet("/cert/phone-monitor-root.cer", async context =>
                {
                    await WriteCertificateFileAsync(
                        context,
                        LocalHttpsCertificate.RootCertificatePath,
                        LocalHttpsCertificate.RootCerFileName,
                        "application/x-x509-ca-cert");
                });

                endpoints.MapGet("/cert/phone-monitor-host.cer", async context =>
                {
                    await WriteCertificateFileAsync(
                        context,
                        LocalHttpsCertificate.HostCertificatePath,
                        LocalHttpsCertificate.HostCerFileName,
                        "application/x-x509-ca-cert");
                });

                endpoints.MapGet("/api/displays", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var catalog = context.RequestServices.GetRequiredService<DisplayCatalog>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(catalog.GetDisplays()));
                });

                endpoints.MapPost("/api/deck/launch", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var request = await JsonSerializer.DeserializeAsync<DeckLaunchRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new DeckLaunchRequest();
                    var mode = NormalizeDeckMode(request.Mode);
                    var deckUrl = BuildLocalDeckUrl(mode);
                    var launcher = context.RequestServices.GetRequiredService<DeckWindowLauncher>();
                    var result = launcher.Launch(deckUrl, mode);
                    if (result.Success)
                    {
                        context.RequestServices.GetRequiredService<DisplayViewerTracker>().ScheduleReturnIfIdle();
                    }

                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/deck/return", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var launcher = context.RequestServices.GetRequiredService<DeckWindowLauncher>();
                    var result = launcher.ReturnToPrimary();
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/api/display/modes", async context =>
                {
                    var controller = context.RequestServices.GetRequiredService<DisplayModeController>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(controller.GetPresets()));
                });

                endpoints.MapPost("/api/display/mode", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var controller = context.RequestServices.GetRequiredService<DisplayModeController>();
                    var request = await JsonSerializer.DeserializeAsync<SetDisplayModeRequest>(context.Request.Body)
                        ?? new SetDisplayModeRequest();
                    var result = controller.Apply(request.Width, request.Height, request.RefreshRate);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/api/display/status", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var controller = context.RequestServices.GetRequiredService<VirtualDisplayController>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(controller.GetStatus()));
                });

                endpoints.MapGet("/api/sideboard/stats", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var proxy = context.RequestServices.GetRequiredService<GlanceBoardProxy>();
                    await WriteGlanceBoardResponseAsync(context, await proxy.GetStatsAsync(context.RequestAborted));
                });

                endpoints.MapGet("/api/sideboard/work-pulse", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var proxy = context.RequestServices.GetRequiredService<GlanceBoardProxy>();
                    await WriteGlanceBoardResponseAsync(context, await proxy.GetWorkPulseAsync(context.RequestAborted));
                });

                endpoints.MapPost("/api/sideboard/refresh", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var proxy = context.RequestServices.GetRequiredService<GlanceBoardProxy>();
                    await WriteGlanceBoardResponseAsync(context, await proxy.RefreshStatsAsync(context.RequestAborted));
                });

                endpoints.MapGet("/api/quotas", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(await quotas.GetSnapshotAsync(context.RequestAborted)));
                });

                endpoints.MapPost("/api/quotas/refresh", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(await quotas.RefreshSnapshotAsync(context.RequestAborted)));
                });

                endpoints.MapPost("/api/quotas/agy/import", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(quotas.ImportAgyAccountsFromAntigravity()));
                });

                endpoints.MapPost("/api/quotas/agy/oauth/start", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var openQuery = context.Request.Query["open"].ToString();
                    var openBrowser = !IsFalseValue(openQuery);
                    var redirectUri = BuildAgyOAuthRedirectUri(context.Request);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(quotas.StartAgyOAuth(redirectUri, openBrowser)));
                });

                endpoints.MapPost("/api/quotas/agy/cli/open", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var request = await JsonSerializer.DeserializeAsync<AgyAccountRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new AgyAccountRequest();
                    var openQuery = context.Request.Query["open"].ToString();
                    var openWindow = !IsFalseValue(openQuery);
                    var result = quotas.OpenAgyCli(request.AccountId, request.Email, openWindow);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/quotas/agy/account/delete", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var request = await JsonSerializer.DeserializeAsync<AgyAccountRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new AgyAccountRequest();
                    var result = quotas.DeleteAgyAccount(request.AccountId, request.Email);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/api/quotas/agy/oauth/callback", async context =>
                {
                    await WriteAgyOAuthCallbackAsync(context);
                });

                endpoints.MapPost("/api/display/enable", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var controller = context.RequestServices.GetRequiredService<VirtualDisplayController>();
                    var request = await JsonSerializer.DeserializeAsync<EnableDisplayRequest>(context.Request.Body)
                        ?? new EnableDisplayRequest();
                    var status = controller.Enable(request.Width, request.Height, request.RefreshRate);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(status));
                });

                endpoints.MapPost("/api/display/disable", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var controller = context.RequestServices.GetRequiredService<VirtualDisplayController>();
                    var status = controller.Disable();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(status));
                });

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
                    var tracker = context.RequestServices.GetRequiredService<DisplayViewerTracker>();
                    using var viewerLease = TrackRemoteViewerIfNeeded(context, tracker);
                    using var socket = await context.WebSockets.AcceptWebSocketAsync();
                    await StreamDisplayAsync(socket, frameSource, deviceName, fps, quality, context.RequestAborted);
                });

                endpoints.MapPost("/api/devices/pairing/request", async context =>
                {
                    if (!await RequirePrivateLanRequestAsync(context)) return;
                    var request = await JsonSerializer.DeserializeAsync<PairingApprovalStartRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingApprovalStartRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.RequestApproval(
                        request.Name,
                        request.Platform,
                        context.Request.Headers["User-Agent"].FirstOrDefault(),
                        GetRemoteAddress(context));
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/quotas/codex/account/delete", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var request = await JsonSerializer.DeserializeAsync<AgyAccountRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new AgyAccountRequest();
                    var result = quotas.DeleteCodexAccount(request.AccountId, request.Email);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/pairing/poll", async context =>
                {
                    if (!await RequirePrivateLanRequestAsync(context)) return;
                    var request = await JsonSerializer.DeserializeAsync<PairingApprovalPollRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingApprovalPollRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.PollApproval(request.RequestId, request.RequestSecret);
                    if (result.Success && result.Status == "approved" && !string.IsNullOrWhiteSpace(result.DeviceToken))
                    {
                        context.Response.Cookies.Append(DeviceTrustService.CookieName, result.DeviceToken, new CookieOptions
                        {
                            HttpOnly = false, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax,
                            Path = "/", MaxAge = TimeSpan.FromDays(400), IsEssential = true
                        });
                    }
                    context.Response.StatusCode = result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/pairing/pending", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { requests = devices.GetPendingApprovals() }));
                });

                endpoints.MapPost("/api/devices/pairing/approve", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                    var request = await JsonSerializer.DeserializeAsync<PairingApprovalActionRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingApprovalActionRequest();
                    var result = context.RequestServices.GetRequiredService<DeviceTrustService>().ApproveRequest(request.RequestId);
                    context.Response.StatusCode = result.Success ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/pairing/deny", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                    var request = await JsonSerializer.DeserializeAsync<PairingApprovalActionRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new PairingApprovalActionRequest();
                    var result = context.RequestServices.GetRequiredService<DeviceTrustService>().DenyRequest(request.RequestId);
                    context.Response.StatusCode = result.Success ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/api/dashboard/events", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var hub = context.RequestServices.GetRequiredService<DashboardEventHub>();
                    context.Response.ContentType = "text/event-stream";
                    context.Response.Headers["Cache-Control"] = "no-cache, no-transform";
                    context.Response.Headers["X-Accel-Buffering"] = "no";
                    using var subscription = hub.Subscribe();
                    try
                    {
                        await context.Response.WriteAsync("retry: 3000\nevent: sync\ndata: initial\n\n", context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);

                        while (!context.RequestAborted.IsCancellationRequested)
                        {
                            var topic = await subscription.Reader.ReadAsync(context.RequestAborted);
                            await context.Response.WriteAsync($"event: {topic}\ndata: {DateTimeOffset.UtcNow:O}\n\n", context.RequestAborted);
                            await context.Response.Body.FlushAsync(context.RequestAborted);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Clean disconnect
                    }
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
                            Math.Max(25, Math.Min(85, request.Quality)));
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

                endpoints.MapGet("/", async context =>
                {
                    if (IsAgyOAuthCallbackRequest(context.Request))
                    {
                        await WriteAgyOAuthCallbackAsync(context);
                        return;
                    }

                    context.Response.Redirect("/index.html");
                    await Task.CompletedTask;
                });
            });
        }

        private static async Task StreamDisplayAsync(WebSocket socket, DisplayFrameSource frameSource, string deviceName, int fps, int quality, CancellationToken cancellationToken)
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

                    // Capture using a private Bitmap unique to this WebSocket connection
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

        private static IDisposable TrackRemoteViewerIfNeeded(HttpContext context, DisplayViewerTracker tracker)
        {
            if (IsLocalRequest(context) || string.IsNullOrWhiteSpace(ReadDeviceToken(context)))
            {
                return NullDisposable.Instance;
            }

            return tracker.TrackRemoteViewer();
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new NullDisposable();

            public void Dispose()
            {
            }
        }

        private static Task WriteStreamCapabilitiesAsync(HttpContext context)
        {
            var h264 = context.RequestServices.GetRequiredService<H264AnnexBStreamer>();
            var h264Metrics = context.RequestServices.GetRequiredService<H264StreamMetrics>();
            var webrtc = context.RequestServices.GetRequiredService<WebRtcH264Service>();
            return context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                jpeg = new
                {
                    supported = true,
                    transport = "websocket",
                    path = "/ws/display",
                    notes = "Current reliable display stream. Tunable by fps and quality."
                },
                h264 = new
                {
                    supported = h264.IsAvailable,
                    transport = "webrtc-h264",
                    path = "/api/stream/webrtc/offer",
                    encoder = h264.IsAvailable ? h264.EncoderDescription : null,
                    clientDecoder = "Browser WebRTC H.264",
                    missing = h264.IsAvailable ? null : "ffmpeg.exe was not found.",
                    next = h264.IsAvailable
                        ? "WebRTC uses constant cadence, wall-clock RTP timestamps, and a low-latency hardware encoder when available."
                        : "Install FFmpeg or set VIBEDECK_FFMPEG to an ffmpeg.exe path.",
                    metrics = h264Metrics.GetSnapshot()
                },
                webrtc = new
                {
                    supported = webrtc.IsAvailable,
                    signalling = "/api/stream/webrtc/offer",
                    transport = "webrtc-h264",
                    next = webrtc.IsAvailable
                        ? "Safari uses WebRTC H.264 first; JPEG remains the automatic fallback."
                        : "Install FFmpeg or set VIBEDECK_FFMPEG to an ffmpeg.exe path."
                }
            }));
        }

        private static async Task WriteGlanceBoardResponseAsync(HttpContext context, GlanceBoardResponse response)
        {
            context.Response.ContentType = "application/json";
            if (response.IsAvailable)
            {
                await context.Response.WriteAsync(response.Json ?? "{}");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                isAvailable = false,
                error = response.Error,
                upstream = response.Json
            }));
        }

        private static async Task WriteQrSvgAsync(HttpContext context, string value)
        {
            context.Response.ContentType = "image/svg+xml";
            context.Response.Headers["Cache-Control"] = "no-store";
            await context.Response.WriteAsync(BuildQrSvg(value));
        }

        private static string BuildQrSvg(string value)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
            var qr = new SvgQRCode(data);
            return qr.GetGraphic(4);
        }

        private static async Task WriteCertificateFileAsync(HttpContext context, string path, string fileName, string contentType)
        {
            if (!File.Exists(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "PhoneMonitor HTTPS certificate is not configured. Run scripts\\setup-https.ps1 on the PC."
                }));
                return;
            }

            context.Response.ContentType = contentType;
            context.Response.Headers["Cache-Control"] = "no-store";
            context.Response.Headers["Content-Disposition"] = $"attachment; filename=\"{fileName}\"";
            await context.Response.SendFileAsync(path);
        }

        private static int ParseInt(string value, int defaultValue, int min, int max)
        {
            if (!int.TryParse(value, out var parsed))
            {
                return defaultValue;
            }

            return Math.Max(min, Math.Min(max, parsed));
        }

        private static async Task<bool> RequireActionTokenAsync(HttpContext context)
        {
            var tokens = context.RequestServices.GetRequiredService<ActionTokenService>();
            var supplied = context.Request.Headers[ActionTokenService.HeaderName].FirstOrDefault();
            if (tokens.IsValid(supplied))
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "PhoneMonitor action token is missing or invalid."
            }));
            return false;
        }

        private static async Task<bool> RequireProtectedActionAsync(HttpContext context)
        {
            return await RequireActionTokenAsync(context) &&
                await RequireTrustedDeviceAsync(context);
        }

        private static async Task<bool> RequireLocalRequestAsync(HttpContext context)
        {
            if (IsLocalRequest(context))
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "This action can only be started from this PC."
            }));
            return false;
        }

        private static async Task<bool> RequirePrivateLanRequestAsync(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            if (address != null && address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (address != null && (IPAddress.IsLoopback(address) || IsLocalMachineAddress(address) || IsPrivateIpv4(address))) return true;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Pairing is only available on the local network." }));
            return false;
        }

        private static bool IsPrivateIpv4(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork) return false;
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                // Tailscale IPv4 uses the RFC 6598 shared address range.
                (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
        }

        private static async Task<bool> RequireTrustedDeviceAsync(HttpContext context)
        {
            if (IsLocalRequest(context))
            {
                return true;
            }

            var hostAuth = context.RequestServices.GetRequiredService<HostAccessAuthService>();
            if (hostAuth.IsAuthenticated(context))
            {
                return true;
            }

            var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
            if (devices.IsTrusted(
                ReadDeviceToken(context),
                GetRemoteAddress(context),
                context.Request.Headers["User-Agent"].FirstOrDefault()))
            {
                return true;
            }

            context.Response.StatusCode = hostAuth.Enabled
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = hostAuth.Enabled ? "Remote login required." : "Phone is not paired with this Host."
            }));
            return false;
        }

        private static bool IsLocalRequest(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            if (address == null)
            {
                return false;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            return IPAddress.IsLoopback(address) || IsLocalMachineAddress(address);
        }

        private static bool IsLocalMachineAddress(IPAddress address)
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    var localAddress = unicast.Address;
                    if (localAddress.IsIPv4MappedToIPv6)
                    {
                        localAddress = localAddress.MapToIPv4();
                    }

                    if (localAddress.AddressFamily == AddressFamily.InterNetwork &&
                        localAddress.Equals(address))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string ReadDeviceToken(HttpContext context)
        {
            var headerValue = context.Request.Headers[DeviceTrustService.HeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }

            var queryValue = context.Request.Query["deviceToken"].ToString();
            if (!string.IsNullOrWhiteSpace(queryValue))
            {
                return queryValue;
            }

            return context.Request.Cookies[DeviceTrustService.CookieName];
        }

        private static string GetRemoteAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? "";
        }

        private static string BuildPairingUrl(string hostUrl, PendingPairing pairing)
        {
            // Query string (not #fragment) so iPhone Camera → Safari keeps pairing params.
            var page = new Uri(new Uri(hostUrl), "index.html");
            var builder = new UriBuilder(page)
            {
                Fragment = string.Empty,
                Query = $"pairingId={Uri.EscapeDataString(pairing.PairingId)}&pairingSecret={Uri.EscapeDataString(pairing.PairingSecret)}"
            };
            return builder.Uri.ToString();
        }

        private static string NormalizeDeckMode(string mode)
        {
            return string.Equals(mode, "quota", StringComparison.OrdinalIgnoreCase)
                ? "quota"
                : "sideboard";
        }

        private static string BuildLocalDeckUrl(string mode)
        {
            var builder = new UriBuilder("http", "127.0.0.1", 5000, "index.html")
            {
                Query = $"mode={Uri.EscapeDataString(NormalizeDeckMode(mode))}&deck=1"
            };
            return builder.Uri.ToString();
        }

        private static string BuildAgyOAuthRedirectUri(HttpRequest request)
        {
            var port = request.Host.Port ?? 5000;
            return $"http://127.0.0.1:{port}";
        }

        private static bool IsFalseValue(string value)
        {
            return string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "no", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAgyOAuthCallbackRequest(HttpRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.Query["state"].ToString()) &&
                (!string.IsNullOrWhiteSpace(request.Query["code"].ToString()) ||
                    !string.IsNullOrWhiteSpace(request.Query["error"].ToString()));
        }

        private static async Task WriteAgyOAuthCallbackAsync(HttpContext context)
        {
            var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
            var result = await quotas.CompleteAgyOAuthAsync(
                context.Request.Query["state"].ToString(),
                context.Request.Query["code"].ToString(),
                context.Request.Query["error"].ToString(),
                context.Request.Query["error_description"].ToString(),
                context.RequestAborted);
            context.Response.StatusCode = result.Success
                ? StatusCodes.Status200OK
                : StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(BuildAgyOAuthCallbackHtml(result));
        }

        private static string BuildAgyOAuthCallbackHtml(AiQuotaService.AgyOAuthCallbackResult result)
        {
            var title = result.Success ? "AGY sign-in complete" : "AGY sign-in failed";
            var message = WebUtility.HtmlEncode(result.Message ?? title);
            var email = WebUtility.HtmlEncode(result.Email ?? string.Empty);
            var className = result.Success ? "ok" : "bad";
            var closeScript = result.Success
                ? "<script>setTimeout(function(){ window.close(); }, 1800);</script>"
                : string.Empty;

            return $@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>{WebUtility.HtmlEncode(title)}</title>
  <style>
    body {{
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      font-family: Segoe UI, system-ui, sans-serif;
      color: #172033;
      background: #f5f7fb;
    }}
    main {{
      width: min(420px, calc(100vw - 32px));
      border: 1px solid #d9e1ee;
      border-radius: 10px;
      padding: 28px;
      background: #fff;
      box-shadow: 0 20px 60px rgba(32, 45, 70, 0.14);
    }}
    h1 {{
      margin: 0 0 10px;
      font-size: 22px;
      letter-spacing: 0;
    }}
    p {{
      margin: 8px 0 0;
      color: #526178;
      line-height: 1.45;
    }}
    .ok {{ color: #16834b; }}
    .bad {{ color: #b42318; }}
  </style>
</head>
<body>
  <main>
    <h1 class=""{className}"">{WebUtility.HtmlEncode(title)}</h1>
    <p>{message}</p>
    <p>{email}</p>
  </main>
  {closeScript}
</body>
</html>";
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

    public sealed class InputEvent
    {
        public string Type { get; set; }
        public string DeviceName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int Buttons { get; set; }
    }

    public sealed class EnableDisplayRequest
    {
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public int RefreshRate { get; set; } = 60;
    }

    public sealed class SetDisplayModeRequest
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; } = 60;
    }

    public sealed class DeckLaunchRequest
    {
        public string Mode { get; set; } = "sideboard";
    }

    public sealed class WebRtcOfferRequest
    {
        public string Sdp { get; set; }
        public string DeviceName { get; set; }
        public int Fps { get; set; } = 45;
        public int Quality { get; set; } = 56;
    }

    public sealed class AgyAccountRequest
    {
        public string AccountId { get; set; }
        public string Email { get; set; }
    }
}
