using System;
using System.IO;
using System.Linq;
using PhoneMonitor.Host.Dashboard;
using Xunit;

namespace PhoneMonitor.Host.Tests
{
    public sealed class DashboardLayoutServiceTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "VibeDeckLayoutTests", Guid.NewGuid().ToString("N"));

        [Fact]
        public void EinkLandscapeDefaultFitsOneSixRowCanvas()
        {
            var service = CreateService();
            var layout = service.Get("eink-landscape");

            Assert.Equal("eink-landscape", layout.Profile);
            Assert.Contains(layout.Items, item => item.Key == "activity-feed" && item.Visible && item.Width == 8 && item.Height == 2);
            Assert.Contains(layout.Items, item => item.Key == "quota-mini" && item.Visible && item.Height == 2);
            Assert.All(layout.Items.Where(item => new[] { "cpu", "ram", "gpu", "vram", "disk", "network" }.Contains(item.Key)), item => Assert.Equal(1, item.Height));
            Assert.All(layout.Items.Where(item => item.Visible), item => Assert.True(item.Row + item.Height <= 6));
        }

        [Fact]
        public void SavedLayoutPersistsAcrossServiceInstances()
        {
            var path = Path.Combine(directory, "layouts.json");
            Directory.CreateDirectory(directory);
            var service = new DashboardLayoutService(path);
            var initial = service.Get("eink-landscape");
            var moved = initial.Items.Select(item => new DashboardLayoutItem
            {
                Key = item.Key,
                Visible = item.Key != "activity-feed",
                Column = item.Key == "system-load" ? 2 : item.Column,
                Row = item.Row,
                Width = item.Width,
                Height = item.Height
            }).ToArray();

            service.Save(new DashboardLayoutUpdateRequest { Profile = "eink-landscape", Items = moved });
            var reloaded = new DashboardLayoutService(path).Get("eink-landscape");

            Assert.Equal(1, reloaded.Revision);
            Assert.Equal(2, reloaded.Items.Single(item => item.Key == "system-load").Column);
            Assert.False(reloaded.Items.Single(item => item.Key == "activity-feed").Visible);
        }

        [Fact]
        public void InvalidCardGeometryIsRejected()
        {
            var service = CreateService();
            Assert.Throws<DashboardLayoutException>(() => service.Save(new DashboardLayoutUpdateRequest
            {
                Profile = "default",
                Items = new[]
                {
                    new DashboardLayoutItem { Key = "cpu", Visible = true, Column = 11, Row = 0, Width = 2, Height = 2 }
                }
            }));
        }

        private DashboardLayoutService CreateService()
        {
            Directory.CreateDirectory(directory);
            return new DashboardLayoutService(Path.Combine(directory, "layouts.json"));
        }

        public void Dispose()
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }
}
