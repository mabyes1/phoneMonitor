using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PhoneMonitor.Host.Dashboard
{
    public sealed class DashboardLayoutService
    {
        private const int ColumnCount = 12;
        private readonly object gate = new object();
        private readonly string storePath;
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        private DashboardLayoutStore store;

        public DashboardLayoutService() : this(null)
        {
        }

        public DashboardLayoutService(string storePathOverride)
        {
            var directory = AppPaths.EnsureDirectory(AppPaths.DashboardDirectory);
            storePath = string.IsNullOrWhiteSpace(storePathOverride)
                ? Path.Combine(directory, "layouts.json")
                : storePathOverride;
            store = Load();
        }

        public DashboardLayoutResponse Get(string profile)
        {
            var normalized = NormalizeProfile(profile);
            lock (gate)
            {
                if (!store.Profiles.TryGetValue(normalized, out var saved))
                {
                    return CreateDefault(normalized);
                }

                return Clone(saved);
            }
        }

        public DashboardLayoutResponse Save(DashboardLayoutUpdateRequest request)
        {
            if (request == null) throw new DashboardLayoutException("版面資料不能是空的。");
            var profile = NormalizeProfile(request.Profile);
            var items = ValidateAndClone(request.Items);

            lock (gate)
            {
                var revision = store.Profiles.TryGetValue(profile, out var previous)
                    ? previous.Revision + 1
                    : 1;
                var response = new DashboardLayoutResponse
                {
                    Profile = profile,
                    Revision = revision,
                    UpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
                    Items = items
                };
                store.Profiles[profile] = response;
                Persist();
                return Clone(response);
            }
        }

        public DashboardLayoutResponse Reset(string profile)
        {
            var normalized = NormalizeProfile(profile);
            lock (gate)
            {
                var response = CreateDefault(normalized);
                response.Revision = store.Profiles.TryGetValue(normalized, out var previous)
                    ? previous.Revision + 1
                    : 1;
                response.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
                store.Profiles[normalized] = response;
                Persist();
                return Clone(response);
            }
        }

        public static string NormalizeProfile(string profile)
        {
            var normalized = (profile ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "eink-landscape" => normalized,
                "eink-portrait" => normalized,
                "default" => normalized,
                _ => "default"
            };
        }

        private DashboardLayoutStore Load()
        {
            try
            {
                if (!File.Exists(storePath)) return new DashboardLayoutStore();
                var loaded = JsonSerializer.Deserialize<DashboardLayoutStore>(File.ReadAllText(storePath), jsonOptions);
                return loaded ?? new DashboardLayoutStore();
            }
            catch
            {
                return new DashboardLayoutStore();
            }
        }

        private void Persist()
        {
            var directory = Path.GetDirectoryName(storePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var tempPath = storePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(store, jsonOptions));
            File.Move(tempPath, storePath, true);
        }

        private static IReadOnlyList<DashboardLayoutItem> ValidateAndClone(IReadOnlyList<DashboardLayoutItem> items)
        {
            if (items == null) throw new DashboardLayoutException("版面缺少卡片資料。");
            if (items.Count > 80) throw new DashboardLayoutException("單一版面最多 80 張卡片。");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<DashboardLayoutItem>(items.Count);
            foreach (var item in items)
            {
                var key = (item?.Key ?? string.Empty).Trim();
                if (key.Length == 0 || key.Length > 160)
                    throw new DashboardLayoutException("卡片識別碼不正確。");
                if (!seen.Add(key)) throw new DashboardLayoutException($"卡片「{key}」重複出現。");
                if (item.Column < 0 || item.Column >= ColumnCount || item.Row < 0 || item.Row > 40)
                    throw new DashboardLayoutException($"卡片「{key}」的位置超出範圍。");
                if (item.Width < 1 || item.Width > ColumnCount || item.Height < 1 || item.Height > 8 || item.Column + item.Width > ColumnCount)
                    throw new DashboardLayoutException($"卡片「{key}」的尺寸超出範圍。");

                result.Add(new DashboardLayoutItem
                {
                    Key = key,
                    Visible = item.Visible,
                    Column = item.Column,
                    Row = item.Row,
                    Width = item.Width,
                    Height = item.Height
                });
            }
            return result;
        }

        private static DashboardLayoutResponse CreateDefault(string profile)
        {
            var items = new List<DashboardLayoutItem>
            {
                Item("system-load", 0, 0, 4, 2),
                Item("activity-feed", 4, 0, 8, 2),
                Item("quota-mini", 0, 2, 4, 2),
                Item("cpu", 4, 2, 2, 1),
                Item("ram", 6, 2, 2, 1),
                Item("gpu", 8, 2, 2, 1),
                Item("vram", 10, 2, 2, 1),
                Item("disk", 4, 3, 2, 1),
                Item("network", 6, 3, 2, 1),
                Item("weather-io", 0, 4, 6, 2),
                Item("processes", 6, 4, 6, 2)
            };

            if (profile == "eink-portrait")
            {
                items = new List<DashboardLayoutItem>
                {
                    Item("system-load", 0, 0, 6, 2),
                    Item("quota-mini", 6, 0, 6, 2),
                    Item("activity-feed", 0, 2, 12, 3),
                    Item("cpu", 0, 5, 4, 1), Item("ram", 4, 5, 4, 1), Item("gpu", 8, 5, 4, 1),
                    Item("vram", 0, 6, 4, 1), Item("disk", 4, 6, 4, 1), Item("network", 8, 6, 4, 1),
                    Item("weather-io", 0, 7, 6, 3), Item("processes", 6, 7, 6, 3)
                };
            }

            return new DashboardLayoutResponse
            {
                Profile = profile,
                Revision = 0,
                UpdatedAt = null,
                Items = items
            };
        }

        private static DashboardLayoutItem Item(string key, int column, int row, int width, int height) => new DashboardLayoutItem
        {
            Key = key,
            Visible = true,
            Column = column,
            Row = row,
            Width = width,
            Height = height
        };

        private static DashboardLayoutResponse Clone(DashboardLayoutResponse value) => new DashboardLayoutResponse
        {
            Profile = value.Profile,
            Revision = value.Revision,
            UpdatedAt = value.UpdatedAt,
            Items = value.Items.Select(item => new DashboardLayoutItem
            {
                Key = item.Key,
                Visible = item.Visible,
                Column = item.Column,
                Row = item.Row,
                Width = item.Width,
                Height = item.Height
            }).ToArray()
        };

        private sealed class DashboardLayoutStore
        {
            public Dictionary<string, DashboardLayoutResponse> Profiles { get; set; } =
                new Dictionary<string, DashboardLayoutResponse>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class DashboardLayoutException : Exception
    {
        public DashboardLayoutException(string message) : base(message) { }
    }
}
