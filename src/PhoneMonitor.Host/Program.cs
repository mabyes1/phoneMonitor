using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneMonitor.Host.Security;

namespace PhoneMonitor.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppPaths.EnsureDirectory(AppPaths.DataRoot);
            AppPaths.EnsureDirectory(AppPaths.LogsDirectory);
            var migrationMessage = AppPaths.TryMigrateLegacyData();
            if (!string.IsNullOrWhiteSpace(migrationMessage))
            {
                Console.WriteLine(migrationMessage);
            }

            using var singleInstance = new Mutex(true, "PhoneMonitor.Host.SingleInstance", out var ownsMutex);
            if (!ownsMutex)
            {
                Console.Error.WriteLine("VibeDeck Host is already running; this launch was ignored.");
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            global::Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = AppPaths.ServiceName;
                })
                .UseContentRoot(ResolveContentRoot())
                .ConfigureLogging((context, logging) =>
                {
                    if (AppPaths.IsWindowsService)
                    {
                        logging.AddEventLog(settings =>
                        {
                            // Source is created by Setup when possible; Application log is the fallback.
                            settings.SourceName = AppPaths.ServiceDisplayName;
                            settings.LogName = "Application";
                        });
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(5000);
                        var httpsStatus = LocalHttpsCertificate.EnsureCurrent();
                        if (!httpsStatus.Success)
                        {
                            Console.Error.WriteLine($"VibeDeck HTTPS certificate check failed: {httpsStatus.Error}");
                        }
                        else if (httpsStatus.RootCreated || httpsStatus.HostCreated)
                        {
                            var action = httpsStatus.RootCreated
                                ? "created"
                                : "updated";
                            Console.WriteLine($"VibeDeck HTTPS certificate {action} for {string.Join(", ", httpsStatus.IpAddresses)}.");
                        }

                        if (LocalHttpsCertificate.IsConfigured)
                        {
                            if (LocalHttpsCertificate.TryLoadServerCertificate(out var certificate, out var certificateError))
                            {
                                options.ListenAnyIP(LocalHttpsCertificate.HttpsPort, listenOptions =>
                                {
                                    listenOptions.UseHttps(certificate);
                                });
                            }
                            else
                            {
                                Console.Error.WriteLine($"VibeDeck HTTPS disabled: {certificateError}");
                            }
                        }
                    });
                    webBuilder.UseStartup<Startup>();
                });

        private static string ResolveContentRoot()
        {
            var current = Directory.GetCurrentDirectory();
            if (Directory.Exists(Path.Combine(current, "wwwroot")))
            {
                return current;
            }

            var projectFromRepositoryRoot = Path.Combine(current, "src", "PhoneMonitor.Host");
            if (Directory.Exists(Path.Combine(projectFromRepositoryRoot, "wwwroot")))
            {
                return projectFromRepositoryRoot;
            }

            var baseDirectory = AppContext.BaseDirectory;
            if (Directory.Exists(Path.Combine(baseDirectory, "wwwroot")))
            {
                return baseDirectory;
            }

            return current;
        }
    }
}
