using System;

namespace PhoneMonitor.Host.WindowsNotifications
{
    public sealed class WindowsNotificationStatusResponse
    {
        public bool Supported { get; set; }
        public bool Packaged { get; set; }
        public bool Enabled { get; set; }
        public bool Listening { get; set; }
        public string AccessStatus { get; set; }
        public string Message { get; set; }
        public string SourceKey { get; set; }
        public string CardId { get; set; }
        public string LastCapturedAt { get; set; }
        public int CapturedCount { get; set; }
    }
}
