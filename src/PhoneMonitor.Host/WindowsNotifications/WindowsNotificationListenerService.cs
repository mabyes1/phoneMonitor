using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneMonitor.Host.CustomSources;
using Windows.ApplicationModel;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace PhoneMonitor.Host.WindowsNotifications
{
    public sealed class WindowsNotificationListenerService : BackgroundService
    {
        private readonly object stateGate = new object();
        private readonly SemaphoreSlim syncGate = new SemaphoreSlim(1, 1);
        private readonly CustomSourceService customSources;
        private readonly ILogger<WindowsNotificationListenerService> logger;
        private readonly string settingsPath;
        private readonly HashSet<string> seenNotificationKeys = new HashSet<string>(StringComparer.Ordinal);

        private UserNotificationListener listener;
        private bool listenerAttached;
        private bool baselineReady;
        private bool enabled;
        private string accessStatus = "NotChecked";
        private string message = "尚未啟用 Windows 通知。";
        private string lastCapturedAt;
        private int capturedCount;

        public WindowsNotificationListenerService(
            CustomSourceService customSources,
            ILogger<WindowsNotificationListenerService> logger)
        {
            this.customSources = customSources;
            this.logger = logger;
            var directory = AppPaths.EnsureDirectory(AppPaths.WindowsNotificationsDirectory);
            settingsPath = Path.Combine(directory, "settings.json");
            enabled = LoadEnabled();
        }

        public WindowsNotificationStatusResponse GetStatus()
        {
            CustomSourceRecord source = null;
            try
            {
                source = customSources.EnsureSystemSource(DateTimeOffset.UtcNow);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Windows notification source could not be initialized.");
            }

            lock (stateGate)
            {
                return new WindowsNotificationStatusResponse
                {
                    Supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393),
                    Packaged = HasPackageIdentity(),
                    Enabled = enabled,
                    Listening = listener != null && listenerAttached,
                    AccessStatus = accessStatus,
                    Message = message,
                    SourceKey = CustomSourceKeys.WindowsNotifications,
                    CardId = source?.Card?.Id,
                    LastCapturedAt = lastCapturedAt,
                    CapturedCount = capturedCount
                };
            }
        }

        public async Task<WindowsNotificationStatusResponse> EnableAsync()
        {
            lock (stateGate)
            {
                enabled = true;
                message = "正在要求 Windows 通知存取權限…";
                SaveEnabled(true);
            }

            try
            {
                var currentListener = GetListener();
                var status = await RequestAccessOnUiThreadAsync(currentListener).ConfigureAwait(false);
                lock (stateGate) accessStatus = status.ToString();

                if (status == UserNotificationListenerAccessStatus.Allowed)
                {
                    await StartListeningAsync(currentListener, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    lock (stateGate)
                    {
                        message = status == UserNotificationListenerAccessStatus.Denied
                            ? "Windows 拒絕了通知存取，請到 Windows 設定手動允許。"
                            : "尚未取得通知存取權限，可以再次按啟用。";
                    }
                }
            }
            catch (Exception error)
            {
                lock (stateGate)
                {
                    accessStatus = "Unavailable";
                    message = DescribeUnavailable(error);
                }
                logger.LogWarning(error, "Windows notification listener could not be enabled.");
            }

            return GetStatus();
        }

        public Task<WindowsNotificationStatusResponse> DisableAsync()
        {
            lock (stateGate)
            {
                enabled = false;
                message = "Windows 通知已停用。";
                SaveEnabled(false);
            }
            DetachListener();
            return Task.FromResult(GetStatus());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                customSources.EnsureSystemSource(DateTimeOffset.UtcNow);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Windows notification card could not be initialized.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (enabled)
                    {
                        await TryStartWithoutPromptAsync(stoppingToken).ConfigureAwait(false);
                        if (listener != null && listenerAttached)
                        {
                            await SyncNotificationsAsync(stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception error)
                {
                    logger.LogDebug(error, "Windows notification sync failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            DetachListener();
        }

        private async Task TryStartWithoutPromptAsync(CancellationToken cancellationToken)
        {
            if (listener != null && listenerAttached) return;

            var currentListener = GetListener();
            var status = currentListener.GetAccessStatus();
            lock (stateGate) accessStatus = status.ToString();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                lock (stateGate)
                {
                    message = status == UserNotificationListenerAccessStatus.Denied
                        ? "Windows 拒絕了通知存取，請到 Windows 設定手動允許。"
                        : "請在資訊面板按啟用 Windows 通知。";
                }
                return;
            }

            await StartListeningAsync(currentListener, cancellationToken).ConfigureAwait(false);
        }

        private async Task StartListeningAsync(UserNotificationListener currentListener, CancellationToken cancellationToken)
        {
            listener = currentListener;
            if (!listenerAttached)
            {
                listener.NotificationChanged += Listener_NotificationChanged;
                listenerAttached = true;
            }

            baselineReady = false;
            await SyncNotificationsAsync(cancellationToken).ConfigureAwait(false);
            lock (stateGate) message = "Windows 通知監聽中；新通知會即時進入卡片。";
        }

        private void DetachListener()
        {
            var currentListener = listener;
            if (currentListener != null && listenerAttached)
            {
                try { currentListener.NotificationChanged -= Listener_NotificationChanged; }
                catch { }
            }
            listenerAttached = false;
            listener = null;
            baselineReady = false;
        }

        private void Listener_NotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            _ = SyncNotificationsSafelyAsync();
        }

        private async Task SyncNotificationsSafelyAsync()
        {
            try
            {
                await SyncNotificationsAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "Windows notification change event could not be processed.");
            }
        }

        private async Task SyncNotificationsAsync(CancellationToken cancellationToken)
        {
            if (listener == null) return;
            await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
                if (!baselineReady)
                {
                    foreach (var notification in notifications)
                    {
                        var key = GetNotificationKey(notification);
                        if (key != null) seenNotificationKeys.Add(key);
                    }
                    baselineReady = true;
                    return;
                }

                foreach (var notification in notifications)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = GetNotificationKey(notification);
                    if (key == null || !seenNotificationKeys.Add(key)) continue;
                    if (seenNotificationKeys.Count > 5000)
                    {
                        var keyToEvict = seenNotificationKeys.First();
                        seenNotificationKeys.Remove(keyToEvict);
                    }
                    CaptureNotification(notification, key);
                }
            }
            finally
            {
                syncGate.Release();
            }
        }

        private void CaptureNotification(UserNotification notification, string key)
        {
            try
            {
                var appName = notification.AppInfo?.DisplayInfo?.DisplayName;
                if (string.IsNullOrWhiteSpace(appName)) appName = "Windows";

                var title = string.Empty;
                var body = string.Empty;
                var binding = notification.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                if (binding != null)
                {
                    var textElements = binding.GetTextElements();
                    title = textElements.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
                    body = string.Join("\n", textElements.Skip(1)
                        .Select(element => element.Text?.Trim())
                        .Where(text => !string.IsNullOrWhiteSpace(text)));
                }

                var text = string.Join("\n", new[] { title, body }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                if (string.IsNullOrWhiteSpace(text)) return;

                var payload = new
                {
                    id = key,
                    from = appName,
                    text,
                    timestamp = notification.CreationTime.ToUniversalTime().ToString("O"),
                    severity = "info"
                };
                using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
                customSources.IngestSystem(CustomSourceKeys.WindowsNotifications, document.RootElement, DateTimeOffset.UtcNow);
                lock (stateGate)
                {
                    lastCapturedAt = CustomSourceDateTime.ToText(DateTimeOffset.UtcNow);
                    capturedCount++;
                }
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "A Windows notification could not be converted to a PhoneMonitor card item.");
            }
        }

        private UserNotificationListener GetListener()
        {
            return listener ??= UserNotificationListener.Current;
        }

        private static string GetNotificationKey(UserNotification notification)
        {
            try
            {
                var appName = notification.AppInfo?.DisplayInfo?.DisplayName ?? "Windows";
                var raw = $"{appName}|{notification.Id}|{notification.CreationTime.ToUniversalTime():O}";
                using var sha = SHA256.Create();
                return "win-" + Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static bool HasPackageIdentity()
        {
            try { return Package.Current?.Id != null; }
            catch { return false; }
        }

        private static string DescribeUnavailable(Exception error)
        {
            if (!HasPackageIdentity())
            {
                return "Windows 通知監聽需要以含 userNotificationListener 權限的 MSIX 版本啟動。";
            }
            return error.Message ?? "Windows 通知監聽目前無法使用。";
        }

        private async Task<UserNotificationListenerAccessStatus> RequestAccessOnUiThreadAsync(UserNotificationListener currentListener)
        {
            return await StaUiInvoker.InvokeAsync(() => currentListener.RequestAccessAsync().AsTask()).ConfigureAwait(false);
        }

        private bool LoadEnabled()
        {
            try
            {
                if (!File.Exists(settingsPath)) return false;
                using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                return document.RootElement.TryGetProperty("enabled", out var value) && value.GetBoolean();
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "Windows notification settings could not be loaded.");
                return false;
            }
        }

        private void SaveEnabled(bool value)
        {
            try
            {
                File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { enabled = value }));
            }
            catch (Exception error)
            {
                logger.LogDebug(error, "Windows notification settings could not be saved.");
            }
        }

        private static class StaUiInvoker
        {
            public static Task<T> InvokeAsync<T>(Func<Task<T>> action)
            {
                var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                var ready = new ManualResetEventSlim(false);
                SynchronizationContext context = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        context = new WindowsFormsSynchronizationContext();
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                        ready.Set();
                        return;
                    }

                    SynchronizationContext.SetSynchronizationContext(context);
                    ready.Set();
                    Application.Run(new ApplicationContext());
                })
                {
                    IsBackground = true,
                    Name = "PhoneMonitor.WindowsNotificationPermission"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.Wait();
                if (completion.Task.IsCompleted) return completion.Task;

                context.Post(async _ =>
                {
                    try
                    {
                        completion.TrySetResult(await action().ConfigureAwait(true));
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                    }
                    finally
                    {
                        Application.ExitThread();
                    }
                }, null);
                return completion.Task;
            }
        }
    }
}
