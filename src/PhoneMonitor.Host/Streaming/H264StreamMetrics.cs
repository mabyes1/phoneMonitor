using System;

namespace PhoneMonitor.Host.Streaming
{
    public sealed class H264StreamMetrics
    {
        private readonly object syncRoot = new object();
        private DateTimeOffset windowStartedAt = DateTimeOffset.UtcNow;
        private long windowBytes;
        private long windowFrames;
        private long windowSkippedFrames;
        private bool active;
        private DateTimeOffset? startedAt;
        private DateTimeOffset? endedAt;
        private int width;
        private int height;
        private int targetFps;
        private int targetQuality;
        private int targetBitrateKbps;
        private long encodedBytes;
        private long queuedFrames;
        private long skippedFrames;
        private double recentMbps;
        private double recentQueuedFps;
        private double recentSkippedFps;
        private string lastError;

        public void Start(int width, int height, int targetFps, int targetQuality, int targetBitrateKbps)
        {
            lock (syncRoot)
            {
                active = true;
                startedAt = DateTimeOffset.UtcNow;
                endedAt = null;
                this.width = width;
                this.height = height;
                this.targetFps = targetFps;
                this.targetQuality = targetQuality;
                this.targetBitrateKbps = targetBitrateKbps;
                encodedBytes = 0;
                queuedFrames = 0;
                skippedFrames = 0;
                recentMbps = 0;
                recentQueuedFps = 0;
                recentSkippedFps = 0;
                lastError = null;
                ResetWindow();
            }
        }

        public void RecordEncodedBytes(int bytes)
        {
            lock (syncRoot)
            {
                encodedBytes += Math.Max(0, bytes);
                windowBytes += Math.Max(0, bytes);
                UpdateWindowIfDue();
            }
        }

        public void RecordQueuedFrame()
        {
            lock (syncRoot)
            {
                queuedFrames++;
                windowFrames++;
                UpdateWindowIfDue();
            }
        }

        public void RecordSkippedFrame()
        {
            lock (syncRoot)
            {
                skippedFrames++;
                windowSkippedFrames++;
                UpdateWindowIfDue();
            }
        }

        public void Stop(string error = null)
        {
            lock (syncRoot)
            {
                UpdateWindowIfDue(force: true);
                active = false;
                endedAt = DateTimeOffset.UtcNow;
                lastError = error;
            }
        }

        public H264StreamMetricsSnapshot GetSnapshot()
        {
            lock (syncRoot)
            {
                UpdateWindowIfDue();
                return new H264StreamMetricsSnapshot
                {
                    Active = active,
                    StartedAt = startedAt,
                    EndedAt = endedAt,
                    Width = width,
                    Height = height,
                    TargetFps = targetFps,
                    TargetQuality = targetQuality,
                    TargetBitrateKbps = targetBitrateKbps,
                    EncodedBytes = encodedBytes,
                    QueuedFrames = queuedFrames,
                    SkippedFrames = skippedFrames,
                    RecentMbps = Math.Round(recentMbps, 2),
                    RecentQueuedFps = Math.Round(recentQueuedFps, 1),
                    RecentSkippedFps = Math.Round(recentSkippedFps, 1),
                    LastError = lastError
                };
            }
        }

        private void UpdateWindowIfDue(bool force = false)
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = now - windowStartedAt;
            if (!force && elapsed.TotalMilliseconds < 1000)
            {
                return;
            }

            var seconds = Math.Max(0.001, elapsed.TotalSeconds);
            recentMbps = windowBytes * 8.0 / seconds / 1000.0 / 1000.0;
            recentQueuedFps = windowFrames / seconds;
            recentSkippedFps = windowSkippedFrames / seconds;
            ResetWindow(now);
        }

        private void ResetWindow()
        {
            ResetWindow(DateTimeOffset.UtcNow);
        }

        private void ResetWindow(DateTimeOffset now)
        {
            windowStartedAt = now;
            windowBytes = 0;
            windowFrames = 0;
            windowSkippedFrames = 0;
        }
    }

    public sealed class H264StreamMetricsSnapshot
    {
        public bool Active { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TargetFps { get; set; }
        public int TargetQuality { get; set; }
        public int TargetBitrateKbps { get; set; }
        public long EncodedBytes { get; set; }
        public long QueuedFrames { get; set; }
        public long SkippedFrames { get; set; }
        public double RecentMbps { get; set; }
        public double RecentQueuedFps { get; set; }
        public double RecentSkippedFps { get; set; }
        public string LastError { get; set; }
    }
}
