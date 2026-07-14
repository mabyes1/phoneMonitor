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
        public const string HeaderName = "X-PhoneMonitor-Device-Token";
        public const string CookieName = "PhoneMonitor-Device-Token";

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
        {
            var root = AppPaths.EnsureDirectory(AppPaths.DevicesDirectory);
            storePath = Path.Combine(root, "trusted-devices.json");
            devices = LoadDevices();
            if (NormalizeDeviceNames(devices))
            {
                SaveDevices(force: true);
            }
        }

        public PairingApprovalRequestResult RequestApproval(string name, string platform, string userAgent, string remoteAddress)
        {
            lock (sync)
            {
                RemoveExpiredPairings(DateTimeOffset.UtcNow);
                var existing = pendingApprovals.Values.FirstOrDefault(item =>
                    item.ExpiresAt > DateTimeOffset.UtcNow &&
                    item.Status == "pending" &&
                    string.Equals(item.RemoteAddress, remoteAddress, StringComparison.Ordinal) &&
                    string.Equals(item.UserAgent, userAgent, StringComparison.Ordinal));
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
                    Name = ResolveDeviceName(name, userAgent),
                    Platform = string.IsNullOrWhiteSpace(platform) ? "web" : platform.Trim(),
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

                var token = CreateToken(40);
                var device = new TrustedDeviceRecord
                {
                    DeviceId = CreateToken(16),
                    Name = request.Name,
                    TokenHash = HashToken(token),
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                    LastRemoteAddress = request.RemoteAddress,
                    LastUserAgent = request.UserAgent
                };
                devices.Add(device);
                request.Status = "approved";
                request.DeviceId = device.DeviceId;
                request.DeviceToken = token;
                SaveDevices(force: true);
                return new DeviceTrustActionResult { Success = true, Message = $"{device.Name} approved." };
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
                    DeviceName = request.Name
                };
                if (request.Status == "approved" || request.Status == "denied") pendingApprovals.Remove(request.RequestId);
                return result;
            }
        }

        public DeviceTrustStatus GetStatus(string deviceToken, string remoteAddress, string userAgent, bool isLocalRequest, bool hostAuthenticated)
        {
            lock (sync)
            {
                var device = FindTrustedDevice(deviceToken);
                if (device != null)
                {
                    TouchDevice(device, remoteAddress, userAgent);
                }

                return new DeviceTrustStatus
                {
                    Trusted = isLocalRequest || hostAuthenticated || device != null,
                    LocalRequest = isLocalRequest,
                    DeviceHeader = HeaderName,
                    PairedDeviceCount = isLocalRequest || hostAuthenticated ? devices.Count : device == null ? 0 : 1,
                    CurrentDevice = device == null ? null : DeviceSummary.From(device),
                    Devices = isLocalRequest || hostAuthenticated
                        ? devices.Select(DeviceSummary.From).ToList()
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

        private List<TrustedDeviceRecord> LoadDevices()
        {
            try
            {
                if (!File.Exists(storePath))
                {
                    return new List<TrustedDeviceRecord>();
                }

                var text = File.ReadAllText(storePath);
                return JsonSerializer.Deserialize<List<TrustedDeviceRecord>>(text, JsonOptions)
                    ?? new List<TrustedDeviceRecord>();
            }
            catch
            {
                return new List<TrustedDeviceRecord>();
            }
        }

        private void TouchDevice(TrustedDeviceRecord device, string remoteAddress, string userAgent)
        {
            device.LastSeenAt = DateTimeOffset.UtcNow;
            device.LastRemoteAddress = remoteAddress;
            device.LastUserAgent = userAgent;
            devicesDirty = true;
            SaveDevices(force: false);
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

            Directory.CreateDirectory(Path.GetDirectoryName(storePath));
            File.WriteAllText(storePath, JsonSerializer.Serialize(devices, JsonOptions));
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

        private static string ResolveDeviceName(string requestedName, string userAgent)
        {
            var name = string.IsNullOrWhiteSpace(requestedName) ? "" : requestedName.Trim();
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

        private static bool IsGenericPairingName(string name)
        {
            return string.Equals(name, "Phone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Win32", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Android Phone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "iPhone", StringComparison.OrdinalIgnoreCase);
        }

        private static bool NormalizeDeviceNames(List<TrustedDeviceRecord> records)
        {
            var changed = false;
            foreach (var record in records)
            {
                var resolvedName = ResolveDeviceName(record.Name, record.LastUserAgent);
                if (!string.Equals(record.Name, resolvedName, StringComparison.Ordinal))
                {
                    record.Name = resolvedName;
                    changed = true;
                }
            }

            return changed;
        }
    }

    public sealed class PairingApprovalStartRequest
    {
        public string Name { get; set; }
        public string Platform { get; set; }
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
        public string RemoteAddress { get; set; }
        public string UserAgent { get; set; }
        public string VerificationCode { get; set; }
        public string Status { get; set; }
        public string DeviceId { get; set; }
        public string DeviceToken { get; set; }
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
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public string LastRemoteAddress { get; set; }

        public static DeviceSummary From(TrustedDeviceRecord device)
        {
            return new DeviceSummary
            {
                DeviceId = device.DeviceId,
                Name = device.Name,
                CreatedAt = device.CreatedAt,
                LastSeenAt = device.LastSeenAt,
                LastRemoteAddress = device.LastRemoteAddress
            };
        }
    }

    public sealed class TrustedDeviceRecord
    {
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public string TokenHash { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public string LastRemoteAddress { get; set; }
        public string LastUserAgent { get; set; }
    }
}
