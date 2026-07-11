using System.Collections.Generic;

namespace PhoneMonitor.Host.Connect
{
    public sealed class ConnectInfo
    {
        public string HostName { get; set; }
        public string PrimaryAddress { get; set; }
        public string PrettyHostName { get; set; }
        public string PrettyHttpsUrl { get; set; }
        public string PrettyHttpUrl { get; set; }
        public string LocalNameHost { get; set; }
        public string LocalNameHttpUrl { get; set; }
        public string LocalNameHttpsUrl { get; set; }
        public string HttpsUrl { get; set; }
        public string HttpUrl { get; set; }
        public string PreferredUrl { get; set; }
        public bool HttpsAvailable { get; set; }
        public string RootCertificateUrl { get; set; }
        public string HostCertificateUrl { get; set; }
        public string HttpsSetupHint { get; set; }
        public string NativeAppUrl { get; set; }
        public string NativeAppDisplayUrl { get; set; }
        public string NativeAppSideboardUrl { get; set; }
        public string NativeAppQuotaUrl { get; set; }
        public string NativeAppCertificateUrl { get; set; }
        public string AndroidAppUrl { get; set; }
        public string AndroidAppDisplayUrl { get; set; }
        public string AndroidAppSideboardUrl { get; set; }
        public string AndroidAppQuotaUrl { get; set; }
        public string AndroidAppCertificateUrl { get; set; }
        public AndroidReleaseInfo AndroidRelease { get; set; }
        public string IosAppUrl { get; set; }
        public string IosAppDisplayUrl { get; set; }
        public string IosAppSideboardUrl { get; set; }
        public string IosAppQuotaUrl { get; set; }
        public string IosAppCertificateUrl { get; set; }
        public bool IsHttpsRequest { get; set; }
        public bool WakeLockNeedsHttps { get; set; }
        public IReadOnlyList<string> Addresses { get; set; }
    }
}
