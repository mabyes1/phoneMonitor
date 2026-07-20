using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneMonitor.Host.Connect;
using PhoneMonitor.Host.CustomSources;
using PhoneMonitor.Host.Diagnostics;
using PhoneMonitor.Host.Display;
using PhoneMonitor.Host.Dashboard;
using PhoneMonitor.Host.Quotas;
using PhoneMonitor.Host.Security;
using PhoneMonitor.Host.Sideboard;
using PhoneMonitor.Host.Streaming;
using PhoneMonitor.Host.Updates;
using PhoneMonitor.Host.Windows;
using PhoneMonitor.Host.WindowsNotifications;
using QRCoder;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        private static readonly JsonSerializerOptions SocketJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto |
                    ForwardedHeaders.XForwardedHost;
                options.ForwardLimit = 1;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);
            });
            services.AddSingleton<DisplayCatalog>();
            services.AddSingleton<DisplayFrameSource>();
            services.AddSingleton<H264StreamMetrics>();
            services.AddSingleton<H264AnnexBStreamer>();
            services.AddSingleton<CloudflareTurnSettingsStore>();
            services.AddHttpClient<CloudflareTurnCredentialService>(client => client.Timeout = TimeSpan.FromSeconds(12));
            services.AddSingleton<WebRtcH264Service>();
            services.AddSingleton<WindowsInputController>();
            services.AddSingleton<DeckWindowLauncher>();
            services.AddSingleton<DisplayModeController>();
            services.AddSingleton<VirtualDisplayController>();
            services.AddSingleton<VirtualDisplayInstaller>();
            services.AddSingleton<GlanceBoardProxy>();
            services.AddSingleton<AiQuotaService>();
            services.AddSingleton<DashboardEventHub>();
            services.AddSingleton<DashboardLayoutService>();
            services.AddSingleton<AuditTrailService>();
            services.AddSingleton<PublicEndpointService>();
            services.AddHttpClient<CloudflareProvisioningClient>(client => client.Timeout = TimeSpan.FromSeconds(25));
            services.AddSingleton<CloudflareConnectorService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<CloudflareConnectorService>());
            services.AddSingleton(sp => new ConnectInfoProvider(
                sp.GetRequiredService<PublicEndpointService>(),
                sp.GetRequiredService<CloudflareConnectorService>()));
            services.AddHttpClient<ConnectionCodeBrokerService>(client => client.Timeout = TimeSpan.FromSeconds(8));
            services.AddHostedService<DashboardChangeMonitor>();
            services.AddSingleton(sp =>
            {
                var options = new CustomSourceOptions();
                sp.GetRequiredService<IConfiguration>().GetSection("CustomSources").Bind(options);
                options.Normalize();
                return options;
            });
            services.AddSingleton<CustomSourceStore>();
            services.AddSingleton<CustomSourceService>();
            services.AddHostedService<CustomSourceCleanupService>();
            services.AddSingleton<WindowsNotificationListenerService>();
            services.AddHostedService(sp => sp.GetRequiredService<WindowsNotificationListenerService>());
            services.AddSingleton<ActionTokenService>();
            services.AddSingleton<DeviceTrustService>();
            services.AddSingleton<HostAccessAuthService>();
            services.AddSingleton<ProductUpdateService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.ApplicationServices.GetRequiredService<AuditTrailService>().Record(
                "information",
                "host",
                "startup",
                "ready",
                details: new Dictionary<string, string>
                {
                    ["version"] = ProductVersion.Current,
                    ["installed"] = AppPaths.IsInstalledLayout ? "true" : "false",
                    ["dataRoot"] = AppPaths.DataRoot
                });

            // Only a local connector may supply X-Forwarded-* data. Preserve the
            // socket peer before Forwarded Headers replaces it with the phone IP.
            app.Use(async (context, next) =>
            {
                context.Items[PublicEndpointService.OriginalRemoteAddressItemKey] = context.Connection.RemoteIpAddress;
                await next();
            });
            app.UseForwardedHeaders();

            app.Use(async (context, next) =>
            {
                var audit = context.RequestServices.GetRequiredService<AuditTrailService>();
                var traceId = AuditTrailService.CreateTraceId(context.Request.Headers["X-VibeDeck-Trace-Id"].FirstOrDefault());
                context.Items[AuditTrailService.TraceIdItemKey] = traceId;
                context.Response.Headers["X-VibeDeck-Trace-Id"] = traceId;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await next();
                }
                catch (Exception error)
                {
                    audit.RecordException(
                        "http",
                        $"{context.Request.Method} {context.Request.Path}",
                        error,
                        traceId,
                        GetRemoteAddress(context),
                        details: new Dictionary<string, string>
                        {
                            ["method"] = context.Request.Method,
                            ["path"] = context.Request.Path.Value ?? "",
                            ["local"] = IsLocalRequest(context) ? "true" : "false"
                        });
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    if (ShouldAuditHttpRequest(context, stopwatch.ElapsedMilliseconds))
                    {
                        audit.Record(
                            context.Response.StatusCode >= 400 ? "warning" : "information",
                            "http",
                            $"{context.Request.Method} {context.Request.Path}",
                            context.Response.StatusCode >= 400 ? "failed" : "completed",
                            traceId,
                            GetRemoteAddress(context),
                            details: new Dictionary<string, string>
                            {
                                ["status"] = context.Response.StatusCode.ToString(),
                                ["durationMs"] = stopwatch.ElapsedMilliseconds.ToString(),
                                ["local"] = IsLocalRequest(context) ? "true" : "false"
                            });
                    }
                }
            });

            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
                await next();
            });

            app.UseRouting();
            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(15)
            });
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.StartsWith("/device-lab", StringComparison.OrdinalIgnoreCase) &&
                    !IsLocalRequest(context))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next();
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = staticContext =>
                {
                    var path = staticContext.Context.Request.Path.Value ?? string.Empty;
                    var devicePreview = path.Equals("/index.html", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(staticContext.Context.Request.Query["devicePreview"].ToString()) &&
                        IsLocalRequest(staticContext.Context);
                    if (devicePreview)
                    {
                        staticContext.Context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                        staticContext.Context.Response.Headers["Content-Security-Policy"] =
                            "frame-ancestors 'self'; object-src 'none'; base-uri 'self'";
                    }

                    if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        staticContext.Context.Response.ContentType = "text/html; charset=utf-8";
                    }
                    else if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    {
                        staticContext.Context.Response.ContentType = "text/javascript; charset=utf-8";
                    }
                    else if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                    {
                        staticContext.Context.Response.ContentType = "text/css; charset=utf-8";
                    }
                    else if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        staticContext.Context.Response.ContentType = "application/json; charset=utf-8";
                    }

                    if (path.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        staticContext.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                        staticContext.Context.Response.Headers["Pragma"] = "no-cache";
                        staticContext.Context.Response.Headers["Expires"] = "0";
                    }
                }
            });

            app.UseEndpoints(endpoints =>
            {
                MapCustomSourceEndpoints(endpoints);
                MapDashboardLayoutEndpoints(endpoints);
                MapDiagnosticsEndpoints(endpoints);
                MapStreamTransportEndpoints(endpoints);
                MapProductUpdateEndpoints(endpoints);
                MapWindowsNotificationEndpoints(endpoints);

                endpoints.MapGet("/health", async context =>
                {
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        status = "ok",
                        app = "VibeDeck.Host",
                        product = AppPaths.ProductName,
                        version = ProductVersion.Current,
                        installed = AppPaths.IsInstalledLayout,
                        transport = "webrtc-h264+jpeg-fallback"
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
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "遠端登入必須使用 HTTPS。", code = "auth.https_required" }));
                        return;
                    }

                    var result = auth.Login(request.Password, GetRemoteAddress(context));
                    if (result.Success)
                    {
                        var sessionCookie = new CookieOptions
                        {
                            HttpOnly = true,
                            Secure = context.Request.IsHttps,
                            SameSite = SameSiteMode.Lax,
                            Path = "/",
                            MaxAge = result.SessionLifetime,
                            IsEssential = true
                        };
                        context.Response.Cookies.Append(HostAccessAuthService.CookieName, result.SessionToken, sessionCookie);
                        context.Response.Cookies.Append(HostAccessAuthService.LegacyCookieName, result.SessionToken, sessionCookie);
                    }

                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = result.Success,
                        code = result.Code,
                        message = result.Message
                    }));
                });

                endpoints.MapPost("/api/auth/logout", async context =>
                {
                    context.RequestServices.GetRequiredService<HostAccessAuthService>().Logout(context);
                    var clearCookie = new CookieOptions { Path = "/", Secure = context.Request.IsHttps };
                    context.Response.Cookies.Delete(HostAccessAuthService.CookieName, clearCookie);
                    context.Response.Cookies.Delete(HostAccessAuthService.LegacyCookieName, clearCookie);
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { success = true }));
                });

                endpoints.MapGet("/api/connect", async context =>
                {
                    var provider = context.RequestServices.GetRequiredService<ConnectInfoProvider>();
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(provider.Get(context)));
                });

                endpoints.MapPost("/api/connect/public-endpoint", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var request = await ReadJsonBodyOrDefaultAsync<PublicEndpointRequest>(context)
                        ?? new PublicEndpointRequest();
                    var endpointsService = context.RequestServices.GetRequiredService<PublicEndpointService>();
                    try
                    {
                        var configured = endpointsService.Configure(request.PublicUrl);
                        WriteAudit(
                            context,
                            "information",
                            "public-endpoint",
                            "configure",
                            "completed",
                            configured.InstallationId,
                            new Dictionary<string, string>
                            {
                                ["host"] = $"{configured.InstallationId}.{configured.BaseDomain}"
                            });
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(configured));
                    }
                    catch (PublicEndpointException error)
                    {
                        WriteAudit(
                            context,
                            "warning",
                            "public-endpoint",
                            "configure",
                            "rejected",
                            details: new Dictionary<string, string>
                            {
                                ["message"] = error.Message
                            });
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error.Message, code = error.Code }));
                    }
                });

                RequestDelegate issueConnectionCode = async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var endpoint = context.RequestServices.GetRequiredService<PublicEndpointService>().GetConfiguration();
                    var broker = context.RequestServices.GetRequiredService<ConnectionCodeBrokerService>();
                    var result = await broker.IssueAsync(endpoint, context.RequestAborted);
                    WriteAudit(
                        context,
                        result.IsSuccess ? "information" : "warning",
                        "connection-code",
                        "issue",
                        result.IsSuccess ? "completed" : "failed",
                        details: new Dictionary<string, string>
                        {
                            ["broker"] = result.BrokerUrl ?? "",
                            ["errorCode"] = result.ErrorCode ?? "",
                            ["expiresAt"] = result.ExpiresAt == default ? "" : result.ExpiresAt.ToString("O")
                        });
                    context.Response.StatusCode = result.IsSuccess
                        ? StatusCodes.Status200OK
                        : result.ErrorCode == "connection_code.secure_url_required"
                            ? StatusCodes.Status409Conflict
                            : StatusCodes.Status503ServiceUnavailable;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = result.IsSuccess,
                        code = result.Code,
                        brokerUrl = result.BrokerUrl,
                        expiresAt = result.ExpiresAt == default ? "" : result.ExpiresAt.ToString("O"),
                        error = result.IsSuccess ? null : new { code = result.ErrorCode }
                    }));
                };
                endpoints.MapPost("/api/connect/device-code", issueConnectionCode);
                endpoints.MapPost("/api/connect/eink-code", issueConnectionCode);

                endpoints.MapDelete("/api/connect/public-endpoint", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var endpointsService = context.RequestServices.GetRequiredService<PublicEndpointService>();
                    try
                    {
                        var cleared = endpointsService.Clear();
                        WriteAudit(
                            context,
                            "warning",
                            "public-endpoint",
                            "clear",
                            "completed",
                            cleared.InstallationId);
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(cleared));
                    }
                    catch (PublicEndpointException error)
                    {
                        WriteAudit(
                            context,
                            "error",
                            "public-endpoint",
                            "clear",
                            "failed",
                            details: new Dictionary<string, string>
                            {
                                ["message"] = error.Message
                            });
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = error.Message, code = error.Code }));
                    }
                });

                endpoints.MapGet("/api/stream/capabilities", async context =>
                {
                    context.Response.ContentType = "application/json";
                    await WriteStreamCapabilitiesAsync(context);
                });

                endpoints.MapGet("/api/session", async context =>
                {
                    if (!IsLocalRequest(context) &&
                        !context.RequestServices.GetRequiredService<HostAccessAuthService>().IsAuthenticated(context) &&
                        !context.RequestServices.GetRequiredService<DeviceTrustService>().IsTrusted(
                            ReadDeviceToken(context),
                            GetRemoteAddress(context),
                            context.Request.Headers["User-Agent"].FirstOrDefault()))
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
                        deviceHeader = DeviceTrustService.HeaderName,
                        product = AppPaths.ProductName,
                        version = ProductVersion.Current,
                        installed = AppPaths.IsInstalledLayout
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
                        hostAuthenticated,
                        Uri.UnescapeDataString(context.Request.Headers[DeviceTrustService.DeviceModelHeaderName].FirstOrDefault() ?? ""),
                        context.Request.Headers[DeviceTrustService.ClientInstanceHeaderName].FirstOrDefault())));
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
                    WriteAudit(
                        context,
                        result.Success ? "information" : "warning",
                        "pairing",
                        "revoke",
                        result.Success ? "completed" : "not-found",
                        details: new Dictionary<string, string>
                        {
                            ["message"] = result.Message
                        });
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
                    WriteAudit(
                        context,
                        result.Success ? "warning" : "error",
                        "pairing",
                        "clear-all",
                        result.Success ? "completed" : "failed",
                        details: new Dictionary<string, string>
                        {
                            ["message"] = result.Message
                        });
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapGet("/qr.svg", async context =>
                {
                    var provider = context.RequestServices.GetRequiredService<ConnectInfoProvider>();
                    var connectInfo = provider.Get(context);
                    var phonePageUrl = new Uri(new Uri(connectInfo.PreferredUrl), "index.html").ToString();
                    await WriteQrSvgAsync(context, phonePageUrl);
                });

                // Canonical product cert URLs; legacy phone-monitor-* paths stay for already-bookmarked phones.
                endpoints.MapGet("/cert/vibedeck-root.cer", async context =>
                {
                    await WriteCertificateFileAsync(
                        context,
                        LocalHttpsCertificate.RootCertificatePath,
                        "vibedeck-root.cer",
                        "application/x-x509-ca-cert");
                });

                endpoints.MapGet("/cert/vibedeck-root.crt", async context =>
                {
                    await WriteCertificateFileAsync(
                        context,
                        LocalHttpsCertificate.RootCertificatePath,
                        "vibedeck-root.crt",
                        "application/x-x509-ca-cert");
                });

                endpoints.MapGet("/cert/vibedeck-host.cer", async context =>
                {
                    await WriteCertificateFileAsync(
                        context,
                        LocalHttpsCertificate.HostCertificatePath,
                        "vibedeck-host.cer",
                        "application/x-x509-ca-cert");
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

                endpoints.MapGet("/api/display/install/status", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var installer = context.RequestServices.GetRequiredService<VirtualDisplayInstaller>();
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(installer.GetStatus()));
                });

                endpoints.MapPost("/api/display/install", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context))
                    {
                        return;
                    }

                    var installer = context.RequestServices.GetRequiredService<VirtualDisplayInstaller>();
                    var status = installer.StartInstall();
                    context.Response.StatusCode = status.State == "failed" || status.State == "unavailable"
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status202Accepted;
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(status));
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

                endpoints.MapGet("/api/quotas/codex/profiles", async context =>
                {
                    if (!await RequireTrustedDeviceAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    context.Response.ContentType = "application/json";
                    context.Response.Headers["Cache-Control"] = "no-store";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(quotas.ListCodexProfiles()));
                });

                endpoints.MapPost("/api/quotas/codex/switch", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var request = await JsonSerializer.DeserializeAsync<CodexAccountRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new CodexAccountRequest();
                    if (string.IsNullOrWhiteSpace(request.AccountId) && string.IsNullOrWhiteSpace(request.Email))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            success = false,
                            code = "quota.codex_profile_required",
                            message = "Select a Codex profile first."
                        }));
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var result = quotas.SwitchCodexAccount(request.AccountId, request.Email);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : string.Equals(result.Code, "quota.codex_profile_not_found", StringComparison.Ordinal)
                            ? StatusCodes.Status404NotFound
                            : StatusCodes.Status500InternalServerError;
                    WriteAudit(
                        context,
                        result.Success ? "information" : "error",
                        "quota",
                        "codex_switch",
                        result.Success ? "success" : "failed",
                        request.Email ?? request.AccountId,
                        new Dictionary<string, string>
                        {
                            ["code"] = result.Code ?? string.Empty
                        });
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/quotas/codex/reauth", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var result = quotas.ReAuthCodex();
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status500InternalServerError;
                    WriteAudit(
                        context,
                        result.Success ? "information" : "error",
                        "quota",
                        "codex_reauth",
                        result.Success ? "success" : "failed",
                        null,
                        new Dictionary<string, string>
                        {
                            ["code"] = result.Code ?? string.Empty
                        });
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/quotas/codex/profile/delete", async context =>
                {
                    if (!await RequireProtectedActionAsync(context))
                    {
                        return;
                    }

                    var request = await JsonSerializer.DeserializeAsync<CodexAccountRequest>(context.Request.Body, SocketJsonOptions)
                        ?? new CodexAccountRequest();
                    var quotas = context.RequestServices.GetRequiredService<AiQuotaService>();
                    var result = quotas.DeleteCodexProfile(request.AccountId, request.Email);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    WriteAudit(
                        context,
                        result.Success ? "information" : "warning",
                        "quota",
                        "codex_profile_delete",
                        result.Success ? "success" : "not_found",
                        request.Email ?? request.AccountId,
                        new Dictionary<string, string>
                        {
                            ["code"] = result.Code ?? string.Empty
                        });
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
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
                    var openQuery = context.Request.Query["open"].ToString();
                    var openWindow = !IsFalseValue(openQuery);
                    var result = quotas.OpenAgyCli(openWindow);
                    context.Response.StatusCode = result.Success
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status404NotFound;
                    WriteAudit(
                        context,
                        result.Success ? "information" : "error",
                        "quota",
                        "agy_cli_open",
                        result.Success ? "opened" : "failed",
                        null,
                        new Dictionary<string, string>
                        {
                            ["account_scope"] = "native-session"
                        });
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
                    using var socket = await context.WebSockets.AcceptWebSocketAsync();
                    await StreamDisplayAsync(socket, frameSource, deviceName, fps, quality, context.RequestAborted);
                });

                endpoints.MapPost("/api/devices/pairing/request", async context =>
                {
                    if (!await RequirePairingTransportAsync(context)) return;
                    var request = await ReadJsonBodyOrDefaultAsync<PairingApprovalStartRequest>(context)
                        ?? new PairingApprovalStartRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.RequestApproval(
                        request.Name,
                        request.Platform,
                        request.Model,
                        request.ClientInstanceId,
                        context.Request.Headers["User-Agent"].FirstOrDefault(),
                        GetRemoteAddress(context));
                    WriteAudit(
                        context,
                        "information",
                        "pairing",
                        "request",
                        "pending",
                        request.Name,
                        new Dictionary<string, string>
                        {
                            ["platform"] = request.Platform,
                            ["model"] = request.Model
                        });
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
                    if (!await RequirePairingTransportAsync(context)) return;
                    var request = await ReadJsonBodyOrDefaultAsync<PairingApprovalPollRequest>(context)
                        ?? new PairingApprovalPollRequest();
                    var devices = context.RequestServices.GetRequiredService<DeviceTrustService>();
                    var result = devices.PollApproval(request.RequestId, request.RequestSecret);
                    if (result.Success && result.Status == "approved" && !string.IsNullOrWhiteSpace(result.DeviceToken))
                    {
                        var deviceCookie = new CookieOptions
                        {
                            HttpOnly = false, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax,
                            Path = "/", MaxAge = TimeSpan.FromDays(400), IsEssential = true
                        };
                        context.Response.Cookies.Append(DeviceTrustService.CookieName, result.DeviceToken, deviceCookie);
                        context.Response.Cookies.Append(DeviceTrustService.LegacyCookieName, result.DeviceToken, deviceCookie);
                    }
                    if (!result.Success || result.Status == "approved" || result.Status == "denied")
                    {
                        WriteAudit(
                            context,
                            result.Success ? "information" : "warning",
                            "pairing",
                            "poll",
                            result.Status ?? "unknown",
                            result.DeviceName,
                            new Dictionary<string, string>
                            {
                                ["continued"] = result.Continued ? "true" : "false",
                                ["message"] = result.Message
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
                    var request = await ReadJsonBodyOrDefaultAsync<PairingApprovalActionRequest>(context)
                        ?? new PairingApprovalActionRequest();
                    var result = context.RequestServices.GetRequiredService<DeviceTrustService>().ApproveRequest(request.RequestId);
                    WriteAudit(
                        context,
                        result.Success ? "information" : "warning",
                        "pairing",
                        "approve",
                        result.Success ? "approved" : "not-found",
                        details: new Dictionary<string, string>
                        {
                            ["message"] = result.Message
                        });
                    context.Response.StatusCode = result.Success ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                });

                endpoints.MapPost("/api/devices/pairing/deny", async context =>
                {
                    if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                    var request = await ReadJsonBodyOrDefaultAsync<PairingApprovalActionRequest>(context)
                        ?? new PairingApprovalActionRequest();
                    var result = context.RequestServices.GetRequiredService<DeviceTrustService>().DenyRequest(request.RequestId);
                    WriteAudit(
                        context,
                        result.Success ? "information" : "warning",
                        "pairing",
                        "deny",
                        result.Success ? "denied" : "not-found",
                        details: new Dictionary<string, string>
                        {
                            ["message"] = result.Message
                        });
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
                            var notification = await subscription.Reader.ReadAsync(context.RequestAborted);
                            var data = notification.DataJson ?? DateTimeOffset.UtcNow.ToString("O");
                            await context.Response.WriteAsync($"event: {notification.Topic}\ndata: {data}\n\n", context.RequestAborted);
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

                endpoints.MapGet("/", async context =>
                {
                    if (IsAgyOAuthCallbackRequest(context.Request))
                    {
                        await WriteAgyOAuthCallbackAsync(context);
                        return;
                    }

                    // Serve the entry page directly. Safari rejects redirected navigation
                    // responses that were previously intercepted by an installed service
                    // worker, and the resulting stale shell can return HTML to JSON APIs.
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                    await context.Response.SendFileAsync(Path.Combine(env.WebRootPath, "index.html"));
                });
            });
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
                        ? "WebRTC uses direct STUN candidates first, optional Cloudflare TURN relay, and a stable JPEG fallback."
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
                    error = "VibeDeck HTTPS certificate is not configured. Reinstall or restart the Host so it can mint a local certificate."
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

        /// <summary>
        /// Read JSON body without crashing Kestrel on empty/malformed payloads
        /// (e.g. PowerShell backtick mangling: {"name":`}).
        /// </summary>
        private static async Task<T> ReadJsonBodyOrDefaultAsync<T>(HttpContext context) where T : class, new()
        {
            try
            {
                if (context.Request.ContentLength == 0)
                {
                    return new T();
                }

                // Allow re-read if a previous middleware already consumed the body.
                context.Request.EnableBuffering();
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                var text = await reader.ReadToEndAsync();
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    return new T();
                }

                text = text.Trim().TrimStart('\uFEFF');
                try
                {
                    return JsonSerializer.Deserialize<T>(text, SocketJsonOptions) ?? new T();
                }
                catch (JsonException)
                {
                    // Malformed body (empty, HTML, PowerShell backtick mangling, etc.) → defaults.
                    return new T();
                }
            }
            catch (JsonException)
            {
                return new T();
            }
            catch (IOException)
            {
                return new T();
            }
        }

        private static async Task<bool> RequireActionTokenAsync(HttpContext context)
        {
            var tokens = context.RequestServices.GetRequiredService<ActionTokenService>();
            var supplied = context.Request.Headers[ActionTokenService.HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(supplied))
            {
                supplied = context.Request.Headers[ActionTokenService.LegacyHeaderName].FirstOrDefault();
            }

            if (tokens.IsValid(supplied))
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "VibeDeck action token is missing or invalid."
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

        private static async Task<bool> RequirePairingTransportAsync(HttpContext context)
        {
            if (IsPrivateLanRequest(context) ||
                context.RequestServices.GetRequiredService<PublicEndpointService>().IsTrustedPublicRequest(context))
            {
                return await RequireHttpsPairingAsync(context);
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Pairing is only available on the local network or through this PC's configured VibeDeck secure URL."
            }));
            return false;
        }

        private static bool IsPrivateLanRequest(HttpContext context)
        {
            var address = context.Connection.RemoteIpAddress;
            if (address != null && address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return address != null &&
                (IPAddress.IsLoopback(address) || IsLocalMachineAddress(address) || IsPrivateIpv4(address));
        }

        private static async Task<bool> RequireHttpsPairingAsync(HttpContext context)
        {
            if (context.Request.IsHttps)
            {
                return true;
            }

            context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "手機配對必須使用 HTTPS。請掃描 PC 顯示的 QR Code；若瀏覽器顯示警告，請按「進階」並繼續前往。"
            }));
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
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                headerValue = context.Request.Headers[DeviceTrustService.LegacyHeaderName].FirstOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }

            var queryValue = context.Request.Query["deviceToken"].ToString();
            if (!string.IsNullOrWhiteSpace(queryValue))
            {
                return queryValue;
            }

            var cookieValue = context.Request.Cookies[DeviceTrustService.CookieName];
            if (!string.IsNullOrWhiteSpace(cookieValue))
            {
                return cookieValue;
            }

            return context.Request.Cookies[DeviceTrustService.LegacyCookieName];
        }

        private static string GetRemoteAddress(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? "";
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

        private static string BuildAgyOAuthCallbackHtml(AgyQuotaService.AgyOAuthCallbackResult result)
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

    }

    public sealed class InputEvent
    {
        public string Type { get; set; }
        public string DeviceName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public int Buttons { get; set; }
        public string Text { get; set; }
        public string Key { get; set; }
        public string Code { get; set; }
        public bool CtrlKey { get; set; }
        public bool AltKey { get; set; }
        public bool ShiftKey { get; set; }
        public bool MetaKey { get; set; }
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

    public sealed class CodexAccountRequest
    {
        public string AccountId { get; set; }
        public string Email { get; set; }
    }

    public sealed class PublicEndpointRequest
    {
        public string PublicUrl { get; set; }
    }
}
