namespace PhoneMonitor.Host.Connect
{
    public sealed class AndroidReleaseInfo
    {
        public bool Available { get; set; }
        public string FileName { get; set; }
        public string InstallPageUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string QrUrl { get; set; }
        public string Sha256Url { get; set; }
        public string VersionName { get; set; }
        public int? VersionCode { get; set; }
        public long? SizeBytes { get; set; }
        public string Sha256 { get; set; }
        public string BuiltAt { get; set; }
    }
}
