using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhoneMonitor.Host.Security
{
    public sealed class DeviceTrustService
    {
        public const string HeaderName = "X-VibeDeck-Device-Token";
        public const string LegacyHeaderName = "X-PhoneMonitor-Device-Token";
        public const string CookieName = "VibeDeck-Device-Token";
        public const string LegacyCookieName = "PhoneMonitor-Device-Token";
        public const string ClientInstanceHeaderName = "X-VibeDeck-Client-Instance";
        public const string DeviceModelHeaderName = "X-VibeDeck-Device-Model";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly TimeSpan LastSeenSaveDebounce = TimeSpan.FromSeconds(30);

        private readonly object sync = new object();
        private readonly string storePath;
        private readonly Dictionary<string, PendingApprovalPairing> pendingApprovals = new Dictionary<string, PendingApprovalPairing>(StringComparer.Ordinal);
        private List<TrustedDeviceRecord> devices;
        private bool devicesDirty;
        private DateTimeOffset lastDevicesFlushAt = DateTimeOffset.MinValue;

        public DeviceTrustService()
            : this(AppPaths.DevicesDirectory)
        {
        }

        public DeviceTrustService(string devicesDirectory)
        {
            var root = AppPaths.EnsureDirectory(devicesDirectory);
            storePath = Path.Combine(root, "trusted-devices.json");
            devices = LoadDevices();
            if (NormalizeDeviceRecords(devices))
            {
                devicesDirty = true;
                try
                {
                    SaveDevices(force: true);
                }
                catch (IOException)
                {
                    // Keep valid in-memory trust even when an old install has bad ACLs.
                }
                catch (UnauthorizedAccessException)
                {
                    // Setup repairs ProgramData permissions; retry after that repair.
                }
            }
        }

        public PairingApprovalRequestResult RequestApproval(
            string name,
            string platform,
            string model,
            string clientInstanceId,
            string userAgent,
            string remoteAddress)
        {
            lock (sync)
            {
                RemoveExpiredPairings(DateTimeOffset.UtcNow);
                var normalizedClientId = NormalizeClientInstanceId(clientInstanceId);
                var existing = pendingApprovals.Values.FirstOrDefault(item =>
                    item.ExpiresAt > DateTimeOffset.UtcNow &&
                    item.Status == "pending" &&
                    ((!string.IsNullOrWhiteSpace(normalizedClientId) &&
                      string.Equals(item.ClientInstanceId, normalizedClientId, StringComparison.Ordinal)) ||
                     (string.IsNullOrWhiteSpace(normalizedClientId) &&
                      string.Equals(item.RemoteAddress, remoteAddress, StringComparison.Ordinal) &&
                      string.Equals(item.UserAgent, userAgent, StringComparison.Ordinal))));
                if (existing != null)
                {
                    return PairingApprovalRequestResult.From(existing, existing.RequestSecret);
                }

                var secret = CreateToken(32);
                var request = new PendingApprovalPairing
                {
                    RequestId = CreateToken(18),
                    RequestSecretHash = HashToken(secret),
                    RequestSecret = secret,
                    Name = ResolveDeviceName(name, model, userAgent),
                    Platform = string.IsNullOrWhiteSpace(platform) ? "web" : platform.Trim(),
                    Model = NormalizeDeviceModel(model),
                    ClientInstanceId = normalizedClientId,
                    RemoteAddress = remoteAddress,
                    UserAgent = userAgent,
                    VerificationCode = CreateNumericCode(),
                    Status = "pending",
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                };
                pendingApprovals[request.RequestId] = request;
                return PairingApprovalRequestResult.From(request, secret);
            }
        }

        public List<PairingApprovalSummary> GetPendingApprovals()
        {
            lock (sync)
            {
                RemoveExpiredPairings(DateTimeOffset.UtcNow);
                return pendingApprovals.Values
                    .Where(item => item.Status == "pending")
                    .OrderBy(item => item.CreatedAt)
                    .Select(PairingApprovalSummary.From)
                    .ToList();
            }
        }

        public DeviceTrustActionResult ApproveRequest(string requestId)
        {
            lock (sync)
            {
                RemoveExpiredPairings(DateTimeOffset.UtcNow);
                if (!pendingApprovals.TryGetValue(requestId ?? "", out var request) || request.Status != "pending")
                    return DeviceTrustActionResult.Fail("Pairing request expired or was not found.");

                var originalDevices = devices.Select(CloneDevice).ToList();
                var originalDirty = devicesDirty;
                var token = CreateToken(40);
                var device = FindPairingContinuation(request);
                var continued = device != null;
                if (device == null)
                {
                    device = new TrustedDeviceRecord
                    {
                        DeviceId = CreateToken(16),
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    devices.Add(device);
                }

                device.Name = request.Name;
                device.Model = request.Model;
                device.ClientInstanceId = request.ClientInstanceId;
                device.TokenHash = HashToken(token);
                device.LastSeenAt = DateTimeOffset.UtcNow;
                device.LastRemoteAddress = request.RemoteAddress;
                device.LastUserAgent = request.UserAgent;

                if (!string.IsNullOrWhiteSpace(request.ClientInstanceId))
                {
                    devices.RemoveAll(item =>
                        !ReferenceEquals(item, device) &&
                        string.Equals(item.ClientInstanceId, request.ClientInstanceId, StringComparison.Ordinal));
                }
                try
                {
                    SaveDevices(force: true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    devices = originalDevices;
                    devicesDirty = originalDirty;
                    return DeviceTrustActionResult.Fail("Pairing could not be saved. Repair the VibeDeck ProgramData permissions and try again.");
                }

                request.Status = "approved";
                request.DeviceId = device.DeviceId;
                request.DeviceToken = token;
                request.Continued = continued;
                return new DeviceTrustActionResult
                {
                    Success = true,
                    Message = continued ? $"{device.Name} pairing continued." : $"{device.Name} approved."
                };
            }
        }

        public DeviceTrustActionResult DenyRequest(string requestId)
        {
            lock (sync)
            {
                if (!pendingApprovals.TryGetValue(requestId ?? "", out var request))
                    return DeviceTrustActionResult.Fail("Pairing request expired or was not found.");
                request.Status = "denied";
                return new DeviceTrustActionResult { Success = true, Message = "Pairing request denied." };
            }
        }

        public PairingApprovalPollResult PollApproval(string requestId, string requestSecret)
        {
            lock (sync)
            {
                RemoveExpiredPairings(DateTimeOffset.UtcNow);
                if (!pendingApprovals.TryGetValue(requestId ?? "", out var request) ||
                    !string.Equals(request.RequestSecretHash, HashToken(requestSecret), StringComparison.Ordinal))
                    return PairingApprovalPollResult.Fail("Pairing request expired or invalid.");

                var result = new PairingApprovalPollResult
                {
                    Success = true,
                    Status = request.Status,
                    DeviceId = request.Status == "approved" ? request.DeviceId : null,
                    DeviceToken = request.Status == "approved" ? request.DeviceToken : null,
                    DeviceName = request.Name,
                    Continued = request.Continued
                };
                if (request.Status == "approved" || request.Status == "denied") pendingApprovals.Remove(request.RequestId);
                return result;
            }
        }

        public DeviceTrustStatus GetStatus(string deviceToken, string remoteAddress, string userAgent, bool isLocalRequest, bool hostAuthenticated)
        {
            return GetStatus(deviceToken, remoteAddress, userAgent, isLocalRequest, hostAuthenticated, "", "");
        }

        public DeviceTrustStatus GetStatus(
            string deviceToken,
            string remoteAddress,
            string userAgent,
            bool isLocalRequest,
            bool hostAuthenticated,
            string model,
            string clientInstanceId)
        {
            lock (sync)
            {
                var now = DateTimeOffset.UtcNow;
                var device = FindTrustedDevice(deviceToken);
                if (device != null)
                {
                    UpdateDeviceIdentity(device, model, clientInstanceId, userAgent);
                    TouchDevice(device, remoteAddress, userAgent);
                }

                return new DeviceTrustStatus
                {
                    Trusted = isLocalRequest || hostAuthenticated || device != null,
                    LocalRequest = isLocalRequest,
                    DeviceHeader = HeaderName,
                    PairedDeviceCount = isLocalRequest || hostAuthenticated ? devices.Count : device == null ? 0 : 1,
                    CurrentDevice = device == null ? null : DeviceSummary.From(device, now),
                    Devices = isLocalRequest || hostAuthenticated
                        ? devices.Select(item => DeviceSummary.From(item, now)).ToList()
                        : new List<DeviceSummary>()
                };
            }
        }

        public bool IsTrusted(string deviceToken, string remoteAddress, string userAgent)
        {
            lock (sync)
            {
                var device = FindTrustedDevice(deviceToken);
                if (device == null)
                {
                    return false;
                }

                TouchDevice(device, remoteAddress, userAgent);
                return true;
            }
        }

        public DeviceTrustActionResult RevokeDevice(string deviceId)
        {
            lock (sync)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    return DeviceTrustActionResult.Fail("Device id is required.");
                }

                var removed = devices.RemoveAll(device => string.Equals(device.DeviceId, deviceId, StringComparison.Ordinal));
                if (removed <= 0)
                {
                    return DeviceTrustActionResult.Fail("Device was not found.");
                }

                SaveDevices(force: true);
                return new DeviceTrustActionResult
                {
                    Success = true,
                    Message = "Device revoked."
                };
            }
        }

        public DeviceTrustActionResult ClearDevices()
        {
            lock (sync)
            {
                var removed = devices.Count;
                devices.Clear();
                pendingApprovals.Clear();
                SaveDevices(force: true);
                return new DeviceTrustActionResult
                {
                    Success = true,
                    Message = removed <= 0 ? "No paired devices." : $"{removed} paired device(s) removed."
                };
            }
        }

        private TrustedDeviceRecord FindTrustedDevice(string deviceToken)
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                return null;
            }

            var hash = HashToken(deviceToken);
            return devices.FirstOrDefault(device => string.Equals(device.TokenHash, hash, StringComparison.Ordinal));
        }

        private static TrustedDeviceRecord CloneDevice(TrustedDeviceRecord device)
        {
            return new TrustedDeviceRecord
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                Model = device.Model,
                ClientInstanceId = device.ClientInstanceId,
                TokenHash = device.TokenHash,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastRemoteAddress = device.LastRemoteAddress,
                LastUserAgent = device.LastUserAgent
            };
        }

        private List<TrustedDeviceRecord> LoadDevices()
        {
            foreach (var candidate in new[] { storePath, storePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    var text = File.ReadAllText(candidate);
                    var loaded = JsonSerializer.Deserialize<List<TrustedDeviceRecord>>(text, JsonOptions);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
                catch
                {
                    // Try the last known-good backup before treating this as a new install.
                }
            }

            return new List<TrustedDeviceRecord>();
        }

        private void TouchDevice(TrustedDeviceRecord device, string remoteAddress, string userAgent)
        {
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.LastRemoteAddress = remoteAddress;
            device.LastUserAgent = userAgent;
            devicesDirty = true;
            try
            {
                // Last-seen persistence is telemetry, not authorization. A
                // damaged ProgramData ACL must never turn a valid device-token
                // check into HTTP 500 and lock every paired phone out.
                SaveDevices(force: false);
            }
            catch (IOException)
            {
                // Keep devicesDirty=true so a later request can retry.
            }
            catch (UnauthorizedAccessException)
            {
                // Setup repairs the ACL; trust remains valid in the meantime.
            }
        }

        private void UpdateDeviceIdentity(TrustedDeviceRecord device, string model, string clientInstanceId, string userAgent)
        {
            var normalizedModel = NormalizeDeviceModel(model);
            var normalizedClientId = NormalizeClientInstanceId(clientInstanceId);
            var changed = false;

            if (!string.IsNullOrWhiteSpace(normalizedModel) &&
                !string.Equals(device.Model, normalizedModel, StringComparison.Ordinal))
            {
                device.Model = normalizedModel;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(normalizedClientId) &&
                !string.Equals(device.ClientInstanceId, normalizedClientId, StringComparison.Ordinal))
            {
                device.ClientInstanceId = normalizedClientId;
                changed = true;
            }

            var resolvedName = ResolveDeviceName(device.Name, device.Model, userAgent);
            if (!string.Equals(device.Name, resolvedName, StringComparison.Ordinal))
            {
                device.Name = resolvedName;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(device.ClientInstanceId))
            {
                changed |= devices.RemoveAll(item =>
                    !ReferenceEquals(item, device) &&
                    string.Equals(item.ClientInstanceId, device.ClientInstanceId, StringComparison.Ordinal)) > 0;
            }

            if (!changed)
            {
                return;
            }

            devicesDirty = true;
            try
            {
                SaveDevices(force: true);
            }
            catch (IOException)
            {
                // Identity enrichment must never invalidate an otherwise trusted device.
            }
            catch (UnauthorizedAccessException)
            {
                // Setup repairs ProgramData ACLs; retry through a later status request.
            }
        }

        private TrustedDeviceRecord FindPairingContinuation(PendingApprovalPairing request)
        {
            if (!string.IsNullOrWhiteSpace(request.ClientInstanceId))
            {
                var stable = devices
                    .Where(item => string.Equals(item.ClientInstanceId, request.ClientInstanceId, StringComparison.Ordinal))
                    .OrderByDescending(item => item.LastSeenAt)
                    .FirstOrDefault();
                if (stable != null)
                {
                    return stable;
                }
            }

            // Adopt one legacy record on the first pairing after this upgrade.
            // Exact UA + address avoids merging two different phones on one LAN.
            return devices
                .Where(item =>
                    string.IsNullOrWhiteSpace(item.ClientInstanceId) &&
                    string.Equals(item.LastRemoteAddress, request.RemoteAddress, StringComparison.Ordinal) &&
                    string.Equals(item.LastUserAgent, request.UserAgent, StringComparison.Ordinal))
                .OrderByDescending(item => item.LastSeenAt)
                .FirstOrDefault();
        }

        private void SaveDevices(bool force)
        {
            if (!force && !devicesDirty)
            {
                return;
            }

            if (!force && DateTimeOffset.UtcNow - lastDevicesFlushAt < LastSeenSaveDebounce)
            {
                return;
            }

            var directory = Path.GetDirectoryName(storePath);
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(storePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(devices, JsonOptions));
                if (File.Exists(storePath))
                {
                    File.Replace(temporaryPath, storePath, storePath + ".bak", true);
                }
                else
                {
                    File.Move(temporaryPath, storePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            devicesDirty = false;
            lastDevicesFlushAt = DateTimeOffset.UtcNow;
        }

        private void RemoveExpiredPairings(DateTimeOffset now)
        {
            foreach (var expired in pendingApprovals.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToList())
                pendingApprovals.Remove(expired);
        }

        private static string CreateNumericCode()
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("D6");
        }

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty)));
        }

        private static string CreateToken(int byteCount)
        {
            var bytes = new byte[byteCount];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string ResolveDeviceName(string requestedName, string model, string userAgent)
        {
            var name = string.IsNullOrWhiteSpace(requestedName) ? "" : requestedName.Trim();
            var normalizedModel = NormalizeDeviceModel(model);
            if (!string.IsNullOrWhiteSpace(normalizedModel))
            {
                if (normalizedModel.Equals("GoColor7", StringComparison.OrdinalIgnoreCase) ||
                    normalizedModel.Replace(" ", "").Equals("BOOXGoColor7", StringComparison.OrdinalIgnoreCase))
                {
                    return "BOOX Go Color 7";
                }

                if (normalizedModel.StartsWith("SM-", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Samsung {normalizedModel.ToUpperInvariant()}";
                }

                return normalizedModel;
            }

            if (!string.IsNullOrWhiteSpace(name) && !IsGenericPairingName(name))
            {
                return name;
            }

            var agent = userAgent ?? "";
            if (agent.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Android Phone";
            }

            if (agent.IndexOf("iPhone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                agent.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                agent.IndexOf("iPod", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "iPhone";
            }

            return string.IsNullOrWhiteSpace(name) ? "Phone" : name;
        }

        private static string NormalizeDeviceModel(string model)
        {
            var value = string.IsNullOrWhiteSpace(model) ? "" : model.Trim();
            if (value.Length > 80 || value.Equals("K", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            return value;
        }

        private static string NormalizeClientInstanceId(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
            return normalized.Length >= 16 && normalized.Length <= 100 ? normalized : "";
        }

        private static bool IsGenericPairingName(string name)
        {
            return string.Equals(name, "Phone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Win32", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Android Phone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Android 裝置", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "iPhone", StringComparison.OrdinalIgnoreCase);
        }

        private static bool NormalizeDeviceRecords(List<TrustedDeviceRecord> records)
        {
            var changed = false;
            foreach (var record in records)
            {
                var resolvedName = ResolveDeviceName(record.Name, record.Model, record.LastUserAgent);
                if (!string.Equals(record.Name, resolvedName, StringComparison.Ordinal))
                {
                    record.Name = resolvedName;
                    changed = true;
                }
            }

            foreach (var group in records
                .Where(record => !string.IsNullOrWhiteSpace(record.ClientInstanceId))
                .GroupBy(record => record.ClientInstanceId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                var keep = group.OrderByDescending(record => record.LastSeenAt).First();
                changed |= records.RemoveAll(record => group.Contains(record) && !ReferenceEquals(record, keep)) > 0;
            }

            // Clean records created by older builds, which had no browser ID.
            // Exact address + full user-agent is intentionally conservative.
            foreach (var group in records
                .Where(record => string.IsNullOrWhiteSpace(record.ClientInstanceId) &&
                    !string.IsNullOrWhiteSpace(record.LastRemoteAddress) &&
                    !string.IsNullOrWhiteSpace(record.LastUserAgent))
                .GroupBy(record => $"{record.LastRemoteAddress}\n{record.LastUserAgent}", StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList())
            {
                var keep = group.OrderByDescending(record => record.LastSeenAt).First();
                changed |= records.RemoveAll(record => group.Contains(record) && !ReferenceEquals(record, keep)) > 0;
            }

            return changed;
        }
    }

    public sealed class PairingApprovalStartRequest
    {
        public string Name { get; set; }
        public string Platform { get; set; }
        public string Model { get; set; }
        public string ClientInstanceId { get; set; }
    }

    public sealed class PairingApprovalActionRequest { public string RequestId { get; set; } }
    public sealed class PairingApprovalPollRequest { public string RequestId { get; set; } public string RequestSecret { get; set; } }

    public sealed class PendingApprovalPairing
    {
        public string RequestId { get; set; }
        public string RequestSecretHash { get; set; }
        public string RequestSecret { get; set; }
        public string Name { get; set; }
        public string Platform { get; set; }
        public string Model { get; set; }
        public string ClientInstanceId { get; set; }
        public string RemoteAddress { get; set; }
        public string UserAgent { get; set; }
        public string VerificationCode { get; set; }
        public string Status { get; set; }
        public string DeviceId { get; set; }
        public string DeviceToken { get; set; }
        public bool Continued { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    public sealed class PairingApprovalRequestResult
    {
        public bool Success { get; set; } = true;
        public string RequestId { get; set; }
        public string RequestSecret { get; set; }
        public string VerificationCode { get; set; }
        public string Status { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public static PairingApprovalRequestResult From(PendingApprovalPairing value, string secret) => new PairingApprovalRequestResult
        { RequestId = value.RequestId, RequestSecret = secret, VerificationCode = value.VerificationCode, Status = value.Status, ExpiresAt = value.ExpiresAt };
    }

    public sealed class PairingApprovalSummary
    {
        public string RequestId { get; set; }
        public string Name { get; set; }
        public string Platform { get; set; }
        public string RemoteAddress { get; set; }
        public string VerificationCode { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public static PairingApprovalSummary From(PendingApprovalPairing value) => new PairingApprovalSummary
        { RequestId = value.RequestId, Name = value.Name, Platform = value.Platform, RemoteAddress = value.RemoteAddress, VerificationCode = value.VerificationCode, ExpiresAt = value.ExpiresAt };
    }

    public sealed class PairingApprovalPollResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string DeviceId { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceName { get; set; }
        public bool Continued { get; set; }
        public static PairingApprovalPollResult Fail(string message) => new PairingApprovalPollResult { Success = false, Status = "expired", Message = message };
    }

    public sealed class DeviceRevokeRequest
    {
        public string DeviceId { get; set; }
    }

    public sealed class DeviceTrustActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static DeviceTrustActionResult Fail(string message)
        {
            return new DeviceTrustActionResult
            {
                Success = false,
                Message = message
            };
        }
    }

    public sealed class DeviceTrustStatus
    {
        public bool Trusted { get; set; }
        public bool LocalRequest { get; set; }
        public string DeviceHeader { get; set; }
        public int PairedDeviceCount { get; set; }
        public DeviceSummary CurrentDevice { get; set; }
        public List<DeviceSummary> Devices { get; set; }
    }

    public sealed class DeviceSummary
    {
        private static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(25);

        public string DeviceId { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public string LastRemoteAddress { get; set; }
        public bool Connected { get; set; }

        public static DeviceSummary From(TrustedDeviceRecord device, DateTimeOffset? now = null)
        {
            var observedAt = now ?? DateTimeOffset.UtcNow;
            return new DeviceSummary
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastRemoteAddress = device.LastRemoteAddress,
                Connected = observedAt - device.LastSeenAt <= ConnectedWindow
            };
        }
    }

    public sealed class TrustedDeviceRecord
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public string ClientInstanceId { get; set; }
        public string TokenHash { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public string LastRemoteAddress { get; set; }
        public string LastUserAgent { get; set; }
    }
}
