using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PhoneMonitor.Host.Diagnostics;

namespace PhoneMonitor.Host.Updates
{
    public sealed class ProductUpdateService
    {
        private const string LatestReleaseApi = "https://api.github.com/repos/mabyes1/phoneMonitor/releases/latest";
        private static readonly Regex Sha256Pattern = new Regex(@"\b[a-fA-F0-9]{64}\b", RegexOptions.Compiled);
        private static readonly HttpClient Client = CreateClient();
        private readonly AuditTrailService audit;
        private readonly object gate = new object();
        private ProductUpdateStatus status = new ProductUpdateStatus
        {
            State = "idle",
            Code = "idle",
            CurrentVersion = ProductVersion.Current
        };
        private ProductUpdateRelease latestRelease;
        private bool operationActive;

        public ProductUpdateService(AuditTrailService audit)
        {
            this.audit = audit;
        }

        public ProductUpdateStatus GetStatus()
        {
            lock (gate)
            {
                return status.Copy();
            }
        }

        public async Task<ProductUpdateStatus> CheckAsync(string traceId)
        {
            if (!AppPaths.IsInstalledLayout)
            {
                return Publish("unavailable", "installed_product_required");
            }

            if (!TryBeginOperation())
            {
                return GetStatus();
            }

            try
            {
                return await CheckCoreAsync(traceId);
            }
            finally
            {
                EndOperation();
            }
        }

        public ProductUpdateStatus Start(string traceId)
        {
            if (!AppPaths.IsInstalledLayout)
            {
                return Publish("unavailable", "installed_product_required");
            }

            if (!TryBeginOperation())
            {
                return GetStatus();
            }

            var result = Publish("checking", "checking");
            _ = Task.Run(() => DownloadAndLaunchAsync(traceId));
            return result;
        }

        private async Task<ProductUpdateStatus> CheckCoreAsync(string traceId)
        {
            Publish("checking", "checking");
            try
            {
                var release = await FetchLatestReleaseAsync();
                if (!release.UpdateAvailable)
                {
                    audit.Record(
                        "information",
                        "product-update",
                        "check",
                        "current",
                        traceId,
                        details: ReleaseDetails(release));
                    return Publish("current", "current", release);
                }

                audit.Record(
                    "information",
                    "product-update",
                    "check",
                    "available",
                    traceId,
                    details: ReleaseDetails(release));
                return Publish("available", "available", release, canStart: true);
            }
            catch (ProductUpdateException error)
            {
                audit.RecordException("product-update", "check", error, traceId);
                return Publish(error.Code == "not_published" ? "unavailable" : "failed", error.Code);
            }
            catch (Exception error)
            {
                audit.RecordException("product-update", "check", error, traceId);
                return Publish("failed", "network_error");
            }
        }

        private async Task DownloadAndLaunchAsync(string traceId)
        {
            string partialPath = null;
            try
            {
                var release = await FetchLatestReleaseAsync();
                if (!release.UpdateAvailable)
                {
                    Publish("current", "current", release);
                    audit.Record("information", "product-update", "start", "current", traceId, details: ReleaseDetails(release));
                    return;
                }

                Publish("downloading", "downloading", release, downloadPercent: 0);
                var expectedHash = await FetchExpectedHashAsync(release);
                var updatesDirectory = AppPaths.EnsureDirectory(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppPaths.ProductName,
                    "updates"));
                var installerPath = Path.Combine(updatesDirectory, release.InstallerFileName);
                partialPath = installerPath + ".downloading";
                if (File.Exists(partialPath))
                {
                    File.Delete(partialPath);
                }

                await DownloadInstallerAsync(release, partialPath);
                var actualHash = CalculateSha256(partialPath);
                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProductUpdateException("checksum_mismatch");
                }

                File.Move(partialPath, installerPath, overwrite: true);
                partialPath = null;
                Publish("ready", "verified", release, downloadPercent: 100);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/SP- /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /NORESTART",
                    WorkingDirectory = updatesDirectory,
                    UseShellExecute = true
                });
                if (process == null)
                {
                    throw new ProductUpdateException("installer_launch_failed");
                }

                audit.Record(
                    "information",
                    "product-update",
                    "start",
                    "installer-started",
                    traceId,
                    details: ReleaseDetails(release));
                Publish("launching", "installer_started", release, downloadPercent: 100);
            }
            catch (ProductUpdateException error)
            {
                DeletePartialFile(partialPath);
                audit.RecordException("product-update", "start", error, traceId);
                Publish("failed", error.Code);
            }
            catch (Exception error)
            {
                DeletePartialFile(partialPath);
                audit.RecordException("product-update", "start", error, traceId);
                Publish("failed", "update_failed");
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task<ProductUpdateRelease> FetchLatestReleaseAsync()
        {
            using var response = await SendAsync(new Uri(LatestReleaseApi), HttpCompletionOption.ResponseContentRead);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ProductUpdateException("not_published");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new ProductUpdateException("release_lookup_failed");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);
            if (!ProductUpdateRelease.TryParse(document.RootElement, ProductVersion.Current, out var release, out var errorCode))
            {
                throw new ProductUpdateException(string.IsNullOrWhiteSpace(errorCode) ? "release_invalid" : errorCode);
            }

            return release;
        }

        private async Task<string> FetchExpectedHashAsync(ProductUpdateRelease release)
        {
            using var response = await SendAsync(release.ChecksumUrl, HttpCompletionOption.ResponseContentRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new ProductUpdateException("checksum_download_failed");
            }

            var content = await response.Content.ReadAsStringAsync();
            var match = Sha256Pattern.Match(content);
            if (!match.Success)
            {
                throw new ProductUpdateException("checksum_invalid");
            }

            return match.Value;
        }

        private async Task DownloadInstallerAsync(ProductUpdateRelease release, string destinationPath)
        {
            using var response = await SendAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new ProductUpdateException("installer_download_failed");
            }

            var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
            var downloaded = 0L;
            var buffer = new byte[81920];
            using var source = await response.Content.ReadAsStreamAsync();
            using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                var read = await source.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer, 0, read);
                downloaded += read;
                if (contentLength > 0)
                {
                    var percent = (int)Math.Min(99, downloaded * 100 / contentLength);
                    Publish("downloading", "downloading", release, downloadPercent: percent);
                }
            }

            await destination.FlushAsync();
        }

        private static async Task<HttpResponseMessage> SendAsync(Uri uri, HttpCompletionOption completionOption)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd($"VibeDeck/{ProductVersion.Current}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            return await Client.SendAsync(request, completionOption);
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            })
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private static string CalculateSha256(string path)
        {
            using var hash = SHA256.Create();
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(hash.ComputeHash(file));
        }

        private static Dictionary<string, string> ReleaseDetails(ProductUpdateRelease release)
        {
            return new Dictionary<string, string>
            {
                ["currentVersion"] = ProductVersion.Current,
                ["latestVersion"] = release?.Version ?? "",
                ["releaseUrl"] = release?.ReleaseUrl ?? ""
            };
        }

        private bool TryBeginOperation()
        {
            lock (gate)
            {
                if (operationActive)
                {
                    return false;
                }

                operationActive = true;
                return true;
            }
        }

        private void EndOperation()
        {
            lock (gate)
            {
                operationActive = false;
            }
        }

        private ProductUpdateStatus Publish(
            string state,
            string code,
            ProductUpdateRelease release = null,
            int downloadPercent = 0,
            bool canStart = false)
        {
            lock (gate)
            {
                if (release != null)
                {
                    latestRelease = release;
                }

                var selectedRelease = release ?? latestRelease;
                status = new ProductUpdateStatus
                {
                    State = state,
                    Code = code,
                    CurrentVersion = ProductVersion.Current,
                    LatestVersion = selectedRelease?.Version ?? "",
                    ReleaseUrl = selectedRelease?.ReleaseUrl ?? "",
                    UpdateAvailable = selectedRelease?.UpdateAvailable == true,
                    CanStart = canStart,
                    DownloadPercent = Math.Max(0, Math.Min(100, downloadPercent))
                };
                return status.Copy();
            }
        }

        private static void DeletePartialFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private sealed class ProductUpdateException : Exception
        {
            public ProductUpdateException(string code)
                : base(code)
            {
                Code = code;
            }

            public string Code { get; }
        }
    }
}
