using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using PhoneMonitor.Host.Security;

namespace PhoneMonitor.Host.Connect
{
    public sealed class ConnectInfoProvider
    {
        private const int HttpPort = 5000;
        private const int HttpsPort = 5443;
        public ConnectInfo Get(HttpRequest request)
        {
            var addresses = GetLanAddresses().ToList();
            var primaryAddress = addresses.FirstOrDefault() ?? "127.0.0.1";
            var localNameHost = $"{Dns.GetHostName()}.local";
            var httpsUrl = $"https://{primaryAddress}:{HttpsPort}/";
            var httpUrl = $"http://{primaryAddress}:{HttpPort}/";
            var localNameHttpsUrl = $"https://{localNameHost}:{HttpsPort}/";
            var localNameHttpUrl = $"http://{localNameHost}:{HttpPort}/";
            var rootCertificateUrl = new Uri(new Uri(httpUrl), "cert/vibedeck-root.cer").ToString();
            var hostCertificateUrl = new Uri(new Uri(httpUrl), "cert/vibedeck-host.cer").ToString();
            var httpsAvailable = LocalHttpsCertificate.IsConfigured;
            var preferredUrl = httpsAvailable ? httpsUrl : httpUrl;
            return new ConnectInfo
            {
                HostName = Dns.GetHostName(),
                PrimaryAddress = primaryAddress,
                PrettyHostName = localNameHost,
                PrettyHttpsUrl = localNameHttpsUrl,
                PrettyHttpUrl = localNameHttpUrl,
                LocalNameHost = localNameHost,
                LocalNameHttpsUrl = localNameHttpsUrl,
                LocalNameHttpUrl = localNameHttpUrl,
                HttpsUrl = httpsUrl,
                HttpUrl = httpUrl,
                PreferredUrl = preferredUrl,
                HttpsAvailable = httpsAvailable,
                RootCertificateUrl = rootCertificateUrl,
                HostCertificateUrl = hostCertificateUrl,
                HttpsSetupHint = httpsAvailable
                    ? "Install and trust the VibeDeck root certificate on the phone, then use the HTTPS URL."
                    : "Restart VibeDeck Host so it can mint a local HTTPS certificate on port 5443.",
                IsHttpsRequest = request.IsHttps,
                WakeLockNeedsHttps = !request.IsHttps,
                Addresses = addresses
            };
        }

        private static IEnumerable<string> GetLanAddresses()
        {
            var candidates = new List<LanAddressCandidate>();
            var order = 0;
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                var hasGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.Any.Equals(gateway.Address) &&
                    !IPAddress.None.Equals(gateway.Address));
                var virtualLike = IsVirtualLike(networkInterface);
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

                    candidates.Add(new LanAddressCandidate
                    {
                        Address = text,
                        Score = ScoreAddress(networkInterface, hasGateway, virtualLike),
                        Order = order++
                    });
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Order)
                .Select(candidate => candidate.Address);
        }

        private static int ScoreAddress(NetworkInterface networkInterface, bool hasGateway, bool virtualLike)
        {
            var score = 0;
            if (hasGateway)
            {
                score += 100;
            }

            if (virtualLike)
            {
                score -= 80;
            }
            else
            {
                score += 40;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                score += 20;
            }

            return score;
        }

        private static bool IsVirtualLike(NetworkInterface networkInterface)
        {
            var text = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
            return text.Contains("virtual") ||
                text.Contains("hyper-v") ||
                text.Contains("wsl") ||
                text.Contains("vmware") ||
                text.Contains("virtualbox") ||
                text.Contains("hamachi") ||
                text.Contains("loopback");
        }

        private sealed class LanAddressCandidate
        {
            public string Address { get; set; }
            public int Score { get; set; }
            public int Order { get; set; }
        }
    }
}
