using System;
using System.Threading.Tasks;
using PhoneMonitor.Host.Windows;

namespace PhoneMonitor.Host.Streaming
{
    public sealed class DisplayViewerTracker
    {
        private static readonly TimeSpan ReturnDelay = TimeSpan.FromSeconds(12);

        private readonly object sync = new object();
        private readonly DeckWindowLauncher deckWindows;
        private int activeRemoteViewers;
        private long generation;

        public DisplayViewerTracker(DeckWindowLauncher deckWindows)
        {
            this.deckWindows = deckWindows;
        }

        public IDisposable TrackRemoteViewer()
        {
            lock (sync)
            {
                activeRemoteViewers++;
                generation++;
            }

            return new Lease(this);
        }

        public void ScheduleReturnIfIdle()
        {
            ScheduleReturnIfNoRemoteViewers();
        }

        private void ReleaseRemoteViewer()
        {
            lock (sync)
            {
                if (activeRemoteViewers > 0)
                {
                    activeRemoteViewers--;
                }

                if (activeRemoteViewers != 0)
                {
                    return;
                }
            }

            ScheduleReturnIfNoRemoteViewers();
        }

        private void ScheduleReturnIfNoRemoteViewers()
        {
            long idleGeneration;
            lock (sync)
            {
                if (activeRemoteViewers != 0)
                {
                    return;
                }

                idleGeneration = ++generation;
            }

            _ = ReturnDeckAfterIdleAsync(idleGeneration);
        }

        private async Task ReturnDeckAfterIdleAsync(long idleGeneration)
        {
            await Task.Delay(ReturnDelay).ConfigureAwait(false);

            lock (sync)
            {
                if (activeRemoteViewers != 0 || generation != idleGeneration)
                {
                    return;
                }
            }

            deckWindows.ReturnToPrimary();
        }

        private sealed class Lease : IDisposable
        {
            private DisplayViewerTracker owner;

            public Lease(DisplayViewerTracker owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                var tracker = owner;
                if (tracker == null)
                {
                    return;
                }

                owner = null;
                tracker.ReleaseRemoteViewer();
            }
        }
    }
}
