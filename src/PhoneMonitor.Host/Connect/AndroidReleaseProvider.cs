using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace PhoneMonitor.Host.Connect
{
    public sealed class AndroidReleaseProvider
    {
        public const string ApkFileName = "vibedeck-android.apk";
        public const string MetadataFileName = "vibedeck-android.json";
        public const string Sha256FileName = ApkFileName + ".sha256";

        private readonly string downloadDirectory;

        public AndroidReleaseProvider(IWebHostEnvironment environment)
        {
            var webRootPath = environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            }

            downloadDirectory = Path.Combine(webRootPath, "downloads");
        }

        public AndroidReleaseInfo Get(string hostUrl)
        {
            var apkPath = GetApkPath();
            var baseUri = new Uri(hostUrl);
            var info = new AndroidReleaseInfo
            {
                Available = File.Exists(apkPath),
                FileName = ApkFileName,
                InstallPageUrl = new Uri(baseUri, "install/android").ToString(),
                DownloadUrl = new Uri(baseUri, "download/vibedeck-android.apk").ToString(),
                QrUrl = new Uri(baseUri, "qr/apk.svg").ToString(),
                Sha256Url = new Uri(baseUri, "download/vibedeck-android.apk.sha256").ToString()
            };

            if (!info.Available)
            {
                return info;
            }

            var file = new FileInfo(apkPath);
            info.SizeBytes = file.Length;

            var metadata = TryReadMetadata();
            if (metadata != null)
            {
                info.FileName = string.IsNullOrWhiteSpace(metadata.FileName) ? ApkFileName : metadata.FileName;
                info.VersionName = metadata.VersionName;
                info.VersionCode = metadata.VersionCode;
                info.BuiltAt = metadata.BuiltAt;
                info.SizeBytes = metadata.SizeBytes ?? info.SizeBytes;
                info.Sha256 = metadata.Sha256;
            }

            if (string.IsNullOrWhiteSpace(info.Sha256))
            {
                info.Sha256 = ComputeSha256(apkPath);
            }

            return info;
        }

        public bool TryGetApk(out string path)
        {
            path = GetApkPath();
            return File.Exists(path);
        }

        public bool TryGetSha256Text(out string text)
        {
            text = "";
            var shaPath = Path.Combine(downloadDirectory, Sha256FileName);
            if (File.Exists(shaPath))
            {
                text = File.ReadAllText(shaPath);
                return true;
            }

            if (!TryGetApk(out var apkPath))
            {
                return false;
            }

            text = $"{ComputeSha256(apkPath)}  {ApkFileName}";
            return true;
        }

        private string GetApkPath()
        {
            return Path.Combine(downloadDirectory, ApkFileName);
        }

        private AndroidReleaseMetadata TryReadMetadata()
        {
            var metadataPath = Path.Combine(downloadDirectory, MetadataFileName);
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(metadataPath);
                return JsonSerializer.Deserialize<AndroidReleaseMetadata>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private sealed class AndroidReleaseMetadata
        {
            public string FileName { get; set; }
            public string VersionName { get; set; }
            public int? VersionCode { get; set; }
            public long? SizeBytes { get; set; }
            public string Sha256 { get; set; }
            public string BuiltAt { get; set; }
        }
    }
}
