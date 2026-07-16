using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using PhoneMonitor.Host.Security;
using PhoneMonitor.Host.WindowsNotifications;

namespace PhoneMonitor.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (WindowsNotificationCompanion.ShouldRun(args))
            {
                RunNotificationCompanionOnStaThread();
                return;
            }

            if (!Environment.UserInteractive || Process.GetCurrentProcess().SessionId == 0)
            {
                Console.Error.WriteLine(
                    "VibeDeck Host must run in the signed-in Windows desktop session. " +
                    "Windows Service / Session 0 hosting cannot enumerate or capture the virtual display.");
                Environment.ExitCode = 2;
                return;
            }

            AppPaths.EnsureDirectory(AppPaths.DataRoot);
            AppPaths.EnsureDirectory(AppPaths.LogsDirectory);
            var migrationMessage = AppPaths.TryMigrateLegacyData();
            if (!string.IsNullOrWhiteSpace(migrationMessage))
            {
                Console.WriteLine(migrationMessage);
            }

            using var singleInstance = new Mutex(true, "VibeDeck.Host.SingleInstance", out var ownsMutex);
            if (!ownsMutex)
            {
                Console.Error.WriteLine("VibeDeck Host is already running; this launch was ignored.");
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        private static void RunNotificationCompanionOnStaThread()
        {
            Exception companionError = null;
            var companionThread = new Thread(() =>
            {
                try
                {
                    WindowsNotificationCompanion.RunAsync().GetAwaiter().GetResult();
                }
                catch (Exception error)
                {
                    companionError = error;
                }
            });
            companionThread.SetApartmentState(ApartmentState.STA);
            companionThread.Start();
            companionThread.Join();

            if (companionError != null)
            {
                ExceptionDispatchInfo.Capture(companionError).Throw();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            global::Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .UseContentRoot(ResolveContentRoot())
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
