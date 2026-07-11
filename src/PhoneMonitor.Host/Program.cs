using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneMonitor.Host.Security;

namespace PhoneMonitor.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var singleInstance = new Mutex(true, "PhoneMonitor.Host.SingleInstance", out var ownsMutex);
            if (!ownsMutex)
            {
                Console.Error.WriteLine("PhoneMonitor Host is already running; this launch was ignored.");
                return;
            }

            CreateHostBuilder(args).Build().Run();
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
                            Console.Error.WriteLine($"PhoneMonitor HTTPS certificate check failed: {httpsStatus.Error}");
                        }
                        else if (httpsStatus.RootCreated || httpsStatus.HostCreated)
                        {
                            var action = httpsStatus.RootCreated
                                ? "created"
                                : "updated";
                            Console.WriteLine($"PhoneMonitor HTTPS certificate {action} for {string.Join(", ", httpsStatus.IpAddresses)}.");
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
                                Console.Error.WriteLine($"PhoneMonitor HTTPS disabled: {certificateError}");
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
