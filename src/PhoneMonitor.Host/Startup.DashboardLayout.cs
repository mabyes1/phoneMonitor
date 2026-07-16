using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PhoneMonitor.Host.Dashboard;

namespace PhoneMonitor.Host
{
    public partial class Startup
    {
        private static void MapDashboardLayoutEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/dashboard/layout", async context =>
            {
                if (!await RequireTrustedDeviceAsync(context)) return;
                var profile = context.Request.Query["profile"].ToString();
                await WriteDashboardLayoutJsonAsync(context,
                    context.RequestServices.GetRequiredService<DashboardLayoutService>().Get(profile));
            });

            endpoints.MapPut("/api/dashboard/layout", async context =>
            {
                if (!await RequireProtectedActionAsync(context)) return;
                try
                {
                    var request = await JsonSerializer.DeserializeAsync<DashboardLayoutUpdateRequest>(
                        context.Request.Body,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var result = context.RequestServices.GetRequiredService<DashboardLayoutService>().Save(request);
                    await WriteDashboardLayoutJsonAsync(context, result);
                }
                catch (DashboardLayoutException error)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await WriteDashboardLayoutJsonAsync(context, new { error = error.Message });
                }
            });

            endpoints.MapPost("/api/dashboard/layout/reset", async context =>
            {
                if (!await RequireProtectedActionAsync(context)) return;
                var profile = context.Request.Query["profile"].ToString();
                await WriteDashboardLayoutJsonAsync(context,
                    context.RequestServices.GetRequiredService<DashboardLayoutService>().Reset(profile));
            });
        }

        private static async Task WriteDashboardLayoutJsonAsync(HttpContext context, object value)
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers["Cache-Control"] = "no-store";
            await context.Response.WriteAsync(JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }), context.RequestAborted);
        }
    }
}
