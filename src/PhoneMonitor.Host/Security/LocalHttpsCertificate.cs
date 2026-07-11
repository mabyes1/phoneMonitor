using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PhoneMonitor.Host.Security
{
    public static class LocalHttpsCertificate
    {
        public const int HttpsPort = 5443;
        public const string RootPfxFileName = "phone-monitor-root.pfx";
        public const string HostPfxFileName = "phone-monitor-host.pfx";
        public const string RootCerFileName = "phone-monitor-root.cer";
        public const string HostCerFileName = "phone-monitor-host.cer";
        public const string CertificateStateFileName = "phone-monitor-certificate-state.json";

        private static readonly TimeSpan RenewalWindow = TimeSpan.FromDays(30);

        public static string CertificateDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneMonitor",
            "certs");

        public static string RootPfxPath => Path.Combine(CertificateDirectory, RootPfxFileName);
        public static string HostPfxPath => Path.Combine(CertificateDirectory, HostPfxFileName);
        public static string RootCertificatePath => Path.Combine(CertificateDirectory, RootCerFileName);
        public static string HostCertificatePath => Path.Combine(CertificateDirectory, HostCerFileName);
        public static string CertificateStatePath => Path.Combine(CertificateDirectory, CertificateStateFileName);

        public static bool IsConfigured =>
            File.Exists(HostPfxPath) &&
            File.Exists(RootCertificatePath) &&
            File.Exists(HostCertificatePath);

        public static LocalHttpsCertificateStatus EnsureCurrent()
        {
            var dnsNames = GetDnsNames().ToList();
            var ipAddresses = GetCertificateIpAddresses().ToList();

            try
            {
                Directory.CreateDirectory(CertificateDirectory);

                var rootCreated = false;
                var hostCreated = false;
                var hostReissued = false;
                var rootCertificate = LoadRootCertificate();
                if (rootCertificate == null || ShouldRenew(rootCertificate, TimeSpan.FromDays(90)))
                {
                    rootCertificate?.Dispose();
                    rootCertificate = CreateRootCertificate();
                    rootCreated = true;
                }

                try
                {
                    if (!IsHostCertificateCurrent(rootCertificate, dnsNames, ipAddresses))
                    {
                        CreateHostCertificate(rootCertificate, dnsNames, ipAddresses);
                        hostCreated = true;
                        hostReissued = !rootCreated;
                    }
                }
                finally
                {
                    rootCertificate.Dispose();
                }

                return new LocalHttpsCertificateStatus
                {
                    Success = true,
                    RootCreated = rootCreated,
                    HostCreated = hostCreated,
                    HostReissued = hostReissued,
                    DnsNames = dnsNames,
                    IpAddresses = ipAddresses
                };
            }
            catch (Exception ex)
            {
                return new LocalHttpsCertificateStatus
                {
                    Success = false,
                    Error = ex.Message,
                    DnsNames = dnsNames,
                    IpAddresses = ipAddresses
                };
            }
        }

        public static bool TryLoadServerCertificate(out X509Certificate2 certificate, out string error)
        {
            certificate = null;
            error = null;

            try
            {
                if (!File.Exists(HostPfxPath))
                {
                    error = $"HTTPS certificate not found at {HostPfxPath}.";
                    return false;
                }

                certificate = LoadPfx(HostPfxPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static X509Certificate2 LoadRootCertificate()
        {
            if (!File.Exists(RootPfxPath))
            {
                return null;
            }

            var certificate = LoadPfx(RootPfxPath);
            if (certificate.HasPrivateKey)
            {
                return certificate;
            }

            certificate.Dispose();
            return null;
        }

        private static X509Certificate2 LoadPfx(string path)
        {
            return new X509Certificate2(
                path,
                string.Empty,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
        }

        private static X509Certificate2 CreateRootCertificate()
        {
            using (var rootKey = RSA.Create(4096))
            {
                var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
                var notAfter = notBefore.AddYears(10);
                var request = new CertificateRequest(
                    "CN=PhoneMonitor Local Root CA",
                    rootKey,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                        true));
                request.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (var certificate = request.CreateSelfSigned(notBefore, notAfter))
                {
                    File.WriteAllBytes(RootCertificatePath, certificate.Export(X509ContentType.Cert));
                    File.WriteAllBytes(RootPfxPath, certificate.Export(X509ContentType.Pfx, string.Empty));
                }
            }

            return LoadPfx(RootPfxPath);
        }

        private static bool IsHostCertificateCurrent(
            X509Certificate2 rootCertificate,
            IReadOnlyList<string> dnsNames,
            IReadOnlyList<string> ipAddresses)
        {
            if (!File.Exists(HostPfxPath) ||
                !File.Exists(HostCertificatePath) ||
                !File.Exists(RootCertificatePath) ||
                !File.Exists(CertificateStatePath))
            {
                return false;
            }

            CertificateState state;
            try
            {
                state = JsonSerializer.Deserialize<CertificateState>(File.ReadAllText(CertificateStatePath));
            }
            catch
            {
                return false;
            }

            if (state == null ||
                !StringSetsEqual(state.DnsNames, dnsNames) ||
                !StringSetsEqual(state.IpAddresses, ipAddresses) ||
                !string.Equals(state.RootThumbprint, rootCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using (var hostCertificate = LoadPfx(HostPfxPath))
                {
                    return hostCertificate.HasPrivateKey &&
                        !ShouldRenew(hostCertificate, RenewalWindow) &&
                        string.Equals(state.HostThumbprint, hostCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void CreateHostCertificate(
            X509Certificate2 rootCertificate,
            IReadOnlyList<string> dnsNames,
            IReadOnlyList<string> ipAddresses)
        {
            using (var hostKey = RSA.Create(2048))
            {
                var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
                var notAfter = notBefore.AddYears(2);
                var request = new CertificateRequest(
                    "CN=PhoneMonitor Local Host",
                    hostKey,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(false, false, 0, true));
                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                        true));

                var oids = new OidCollection();
                oids.Add(new Oid("1.3.6.1.5.5.7.3.1"));
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(oids, false));

                var san = new SubjectAlternativeNameBuilder();
                foreach (var dnsName in dnsNames)
                {
                    san.AddDnsName(dnsName);
                }

                foreach (var ipAddress in ipAddresses)
                {
                    san.AddIpAddress(IPAddress.Parse(ipAddress));
                }

                request.CertificateExtensions.Add(san.Build(false));
                request.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (var hostWithoutKey = request.Create(rootCertificate, notBefore, notAfter, NewRandomSerial()))
                using (var hostCertificate = hostWithoutKey.CopyWithPrivateKey(hostKey))
                {
                    File.WriteAllBytes(HostCertificatePath, hostCertificate.Export(X509ContentType.Cert));
                    File.WriteAllBytes(HostPfxPath, hostCertificate.Export(X509ContentType.Pfx, string.Empty));

                    var state = new CertificateState
                    {
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        DnsNames = dnsNames.ToList(),
                        IpAddresses = ipAddresses.ToList(),
                        RootThumbprint = rootCertificate.Thumbprint,
                        HostThumbprint = hostCertificate.Thumbprint,
                        HostNotAfterUtc = hostCertificate.NotAfter.ToUniversalTime()
                    };
                    File.WriteAllText(
                        CertificateStatePath,
                        JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }

        private static byte[] NewRandomSerial()
        {
            var serial = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(serial);
            }

            return serial;
        }

        private static bool ShouldRenew(X509Certificate2 certificate, TimeSpan window)
        {
            return DateTimeOffset.UtcNow.Add(window) >= new DateTimeOffset(certificate.NotAfter);
        }

        private static bool StringSetsEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            var leftSet = new HashSet<string>(left ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var rightSet = new HashSet<string>(right ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return leftSet.SetEquals(rightSet);
        }

        private static IEnumerable<string> GetDnsNames()
        {
            var hostName = Dns.GetHostName();
            return new[] { "localhost", hostName, $"{hostName}.local" }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetCertificateIpAddresses()
        {
            return new[] { "127.0.0.1" }
                .Concat(GetLanIpAddresses())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GetLanIpAddresses()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                foreach (var address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var text = address.Address.ToString();
                    if (text.StartsWith("169.254.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return text;
                }
            }
        }

        private sealed class CertificateState
        {
            public DateTimeOffset CreatedAtUtc { get; set; }
            public List<string> DnsNames { get; set; }
            public List<string> IpAddresses { get; set; }
            public string RootThumbprint { get; set; }
            public string HostThumbprint { get; set; }
            public DateTime HostNotAfterUtc { get; set; }
        }
    }

    public sealed class LocalHttpsCertificateStatus
    {
        public bool Success { get; set; }
        public bool RootCreated { get; set; }
        public bool HostCreated { get; set; }
        public bool HostReissued { get; set; }
        public string Error { get; set; }
        public IReadOnlyList<string> DnsNames { get; set; }
        public IReadOnlyList<string> IpAddresses { get; set; }
    }
}
