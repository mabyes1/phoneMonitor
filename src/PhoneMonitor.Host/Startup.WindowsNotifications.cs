using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PhoneMonitor.Host.CustomSources;
using PhoneMonitor.Host.WindowsNotifications;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        private static void MapWindowsNotificationEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/windows-notifications/status", async context =>
            {
                if (!await RequireLocalRequestAsync(context)) return;
                var service = context.RequestServices.GetRequiredService<WindowsNotificationListenerService>();
                await WriteWindowsNotificationJsonAsync(context, service.GetStatus());
            });

            endpoints.MapPost("/api/windows-notifications/enable", async context =>
            {
                if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                var service = context.RequestServices.GetRequiredService<WindowsNotificationListenerService>();
                await HandleWindowsNotificationAsync(context, () => service.EnableAsync());
            });

            endpoints.MapPost("/api/windows-notifications/disable", async context =>
            {
                if (!await RequireActionTokenAsync(context) || !await RequireLocalRequestAsync(context)) return;
                var service = context.RequestServices.GetRequiredService<WindowsNotificationListenerService>();
                await HandleWindowsNotificationAsync(context, () => service.DisableAsync());
            });
        }

        private static async Task HandleWindowsNotificationAsync(
            HttpContext context,
            Func<Task<WindowsNotificationStatusResponse>> action)
        {
            try
            {
                await WriteWindowsNotificationJsonAsync(context, await action());
            }
            catch (Exception error)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await WriteWindowsNotificationJsonAsync(context, new
                {
                    error = "windows_notifications_unavailable",
                    message = error.Message
                });
            }
        }

        private static async Task WriteWindowsNotificationJsonAsync(HttpContext context, object value)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["Cache-Control"] = "no-store";
            await context.Response.WriteAsync(JsonSerializer.Serialize(value, CustomSourceJson.Options), context.RequestAborted);
        }
    }
}
