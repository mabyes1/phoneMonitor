using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PhoneMonitor.Host.Quotas;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        // AI quota snapshot + Codex/AGY account management endpoints, all backed by
        // AiQuotaService. Extracted verbatim from Startup.cs (they were interleaved
        // with pairing endpoints in the original file); shared helpers
        // (SocketJsonOptions, WriteAudit, IsFalseValue, BuildAgyOAuthRedirectUri,
        // WriteAgyOAuthCallbackAsync, Require*Async) remain on the Startup partial
        // class. Request DTOs CodexAccountRequest/AgyAccountRequest live in the same
        // namespace.
        private static void MapQuotaEndpoints(IEndpointRouteBuilder endpoints)
        {
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
        }
    }
}
