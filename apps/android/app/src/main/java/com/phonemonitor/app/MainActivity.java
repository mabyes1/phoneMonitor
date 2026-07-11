package com.phonemonitor.app;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.DownloadManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ActivityInfo;
import android.graphics.Color;
import android.net.DhcpInfo;
import android.net.Uri;
import android.net.wifi.WifiManager;
import android.os.Bundle;
import android.os.Build;
import android.os.Environment;
import android.os.PowerManager;
import android.provider.Settings;
import android.text.InputType;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.webkit.WebChromeClient;
import android.webkit.JavascriptInterface;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

public final class MainActivity extends Activity {
    private static final String PREFS = "phone-monitor";
    private static final String PREF_HOST_URL = "host-url";
    private static final String PREF_DEVICE_TOKEN = "device-token";
    private static final String PREF_DEVICE_ID = "device-id";
    private static final String PREF_STREAM_MODE = "stream-mode";
    private static final String STREAM_MODE_H264 = "h264";
    private static final String STREAM_MODE_JPEG = "jpeg";
    private static final String DEFAULT_HOST_URL = "http://10.0.2.2:5000/index.html";
    private static final String ROOT_CERTIFICATE_FILE_NAME = "phone-monitor-root.cer";

    private EditText hostInput;
    private TextView statusText;
    private WebView webView;
    private View chromeView;
    private SharedPreferences prefs;
    private ExecutorService discoveryExecutor;
    private ExecutorService updateExecutor;
    private ExecutorService commandExecutor;
    private Button h264StreamButton;
    private Button jpegStreamButton;
    private Button updateButton;
    private String updateDownloadUrl = "";
    private String updateInstallPageUrl = "";
    private String currentAppMode = "display";
    private boolean einkDevice;
    private boolean autoDiscoveryAttempted;
    private PowerManager.WakeLock einkScreenWakeLock;
    private boolean viewerChromeHidden;
    private final Runnable viewerSyncRunnable = new Runnable() {
        @Override
        public void run() {
            syncViewerChromeFromPage();
            if (webView != null) {
                webView.postDelayed(this, 500);
            }
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        prefs = getSharedPreferences(PREFS, MODE_PRIVATE);
        einkDevice = isEinkReader();
        if (einkDevice) currentAppMode = "sideboard";
        configureWindow();
        buildUi();
        configureWebView();

        if (!loadFromIntent(getIntent())) {
            String savedHost = prefs.getString(PREF_HOST_URL, "");
            if (TextUtils.isEmpty(savedHost)) {
                statusText.setText("第一次使用：正在自動尋找 VibeDeck Host...");
                discoverHost();
            } else {
                loadHost(savedHost);
            }
        }
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        loadFromIntent(intent);
    }

    @Override
    protected void onResume() {
        super.onResume();
        acquireEinkScreenWakeLock();
        if (webView != null) {
            webView.post(viewerSyncRunnable);
        }
    }

    @Override
    protected void onPause() {
        if (webView != null) {
            webView.removeCallbacks(viewerSyncRunnable);
        }

        // Keep the BOOX panel wake lock across transient pauses. Go Color can
        // briefly pause the Activity while changing focus or entering its
        // reader overlay; releasing here lets the system lock the device and
        // the WebView cannot recover without another user gesture.
        if (!einkDevice) {
            releaseEinkScreenWakeLock();
        }

        super.onPause();
    }

    @Override
    public void onBackPressed() {
        if (viewerChromeHidden) {
            exitViewerFromShell();
            return;
        }

        if (webView != null && webView.canGoBack()) {
            webView.goBack();
            return;
        }

        super.onBackPressed();
    }

    @Override
    protected void onDestroy() {
        releaseEinkScreenWakeLock();
        if (discoveryExecutor != null) {
            discoveryExecutor.shutdownNow();
            discoveryExecutor = null;
        }

        if (updateExecutor != null) {
            updateExecutor.shutdownNow();
            updateExecutor = null;
        }

        if (commandExecutor != null) {
            commandExecutor.shutdownNow();
            commandExecutor = null;
        }

        if (webView != null) {
            webView.removeCallbacks(viewerSyncRunnable);
        }

        super.onDestroy();
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus && viewerChromeHidden) {
            enterImmersiveMode();
        }
    }

    private void configureWindow() {
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR);
        Window window = getWindow();
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        int chromeColor = einkDevice ? Color.rgb(242, 240, 232) : Color.rgb(17, 24, 32);
        window.setStatusBarColor(chromeColor);
        window.setNavigationBarColor(chromeColor);
    }

    @SuppressWarnings("deprecation")
    private void acquireEinkScreenWakeLock() {
        if (!einkDevice) return;
        if (einkScreenWakeLock == null) {
            PowerManager powerManager = (PowerManager) getSystemService(Context.POWER_SERVICE);
            if (powerManager == null) return;
            einkScreenWakeLock = powerManager.newWakeLock(
                PowerManager.FULL_WAKE_LOCK | PowerManager.ACQUIRE_CAUSES_WAKEUP | PowerManager.ON_AFTER_RELEASE,
                "VibeDeck:EInkScreen");
            einkScreenWakeLock.setReferenceCounted(false);
        }
        if (!einkScreenWakeLock.isHeld()) einkScreenWakeLock.acquire();
    }

    private void releaseEinkScreenWakeLock() {
        if (einkScreenWakeLock != null && einkScreenWakeLock.isHeld()) einkScreenWakeLock.release();
    }

    private void buildUi() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(einkDevice ? Color.rgb(242, 240, 232) : Color.rgb(17, 24, 32));

        LinearLayout chrome = new LinearLayout(this);
        chrome.setOrientation(LinearLayout.VERTICAL);
        chrome.setBackgroundColor(einkDevice ? Color.rgb(242, 240, 232) : Color.rgb(17, 24, 32));
        chromeView = chrome;

        LinearLayout topRow = new LinearLayout(this);
        topRow.setOrientation(LinearLayout.HORIZONTAL);
        topRow.setGravity(Gravity.CENTER_VERTICAL);
        topRow.setPadding(dp(10), dp(10), dp(10), dp(6));

        hostInput = new EditText(this);
        hostInput.setSingleLine(true);
        hostInput.setSelectAllOnFocus(false);
        hostInput.setInputType(InputType.TYPE_TEXT_VARIATION_URI);
        hostInput.setTextColor(einkDevice ? Color.rgb(20, 20, 18) : Color.rgb(245, 247, 251));
        hostInput.setHintTextColor(einkDevice ? Color.rgb(86, 86, 76) : Color.rgb(145, 160, 176));
        hostInput.setTextSize(13f);
        hostInput.setText(prefs.getString(PREF_HOST_URL, ""));
        hostInput.setHint("http://PC-LAN-IP:5000");
        topRow.addView(hostInput, new LinearLayout.LayoutParams(0, dp(46), 1f));

        Button openButton = button("連線");
        openButton.setOnClickListener(view -> loadHost(hostInput.getText().toString()));
        topRow.addView(openButton, new LinearLayout.LayoutParams(dp(72), dp(46)));

        Button findButton = button("找 Host");
        findButton.setOnClickListener(view -> discoverHost());
        topRow.addView(findButton, new LinearLayout.LayoutParams(dp(84), dp(46)));
        chrome.addView(topRow, new LinearLayout.LayoutParams(-1, -2));

        // Primary product modes only — web page hides its own duplicate tabs when native-shell is present.
        LinearLayout modeRow = new LinearLayout(this);
        modeRow.setOrientation(LinearLayout.HORIZONTAL);
        modeRow.setPadding(dp(10), 0, dp(10), dp(6));
        if (!einkDevice) addModeButton(modeRow, "顯示器", "display");
        addModeButton(modeRow, "資訊板", "sideboard");
        addModeButton(modeRow, "額度", "quota");
        chrome.addView(modeRow, new LinearLayout.LayoutParams(-1, -2));

        // Stream + utility on one compact row (was two full-width rows).
        LinearLayout actionRow = new LinearLayout(this);
        actionRow.setOrientation(LinearLayout.HORIZONTAL);
        actionRow.setPadding(dp(10), 0, dp(10), dp(6));

        if (!einkDevice) {
            h264StreamButton = button("H.264");
            h264StreamButton.setOnClickListener(view -> setStreamMode(STREAM_MODE_H264));
            actionRow.addView(h264StreamButton, new LinearLayout.LayoutParams(0, dp(40), 1f));

            jpegStreamButton = button("JPEG");
            jpegStreamButton.setOnClickListener(view -> setStreamMode(STREAM_MODE_JPEG));
            actionRow.addView(jpegStreamButton, new LinearLayout.LayoutParams(0, dp(40), 1f));
        }

        Button reloadButton = button("重整");
        reloadButton.setOnClickListener(view -> webView.reload());
        actionRow.addView(reloadButton, new LinearLayout.LayoutParams(0, dp(40), 1f));

        Button trustButton = button("憑證");
        trustButton.setOnClickListener(view -> installRootCertificate());
        actionRow.addView(trustButton, new LinearLayout.LayoutParams(0, dp(40), 1f));
        chrome.addView(actionRow, new LinearLayout.LayoutParams(-1, -2));

        updateButton = button("更新 App");
        updateButton.setVisibility(View.GONE);
        updateButton.setOnClickListener(view -> openUpdateDownload());
        LinearLayout.LayoutParams updateParams = new LinearLayout.LayoutParams(-1, dp(42));
        updateParams.setMargins(dp(10), 0, dp(10), dp(6));
        chrome.addView(updateButton, updateParams);

        statusText = new TextView(this);
        statusText.setTextColor(einkDevice ? Color.rgb(52, 52, 46) : Color.rgb(185, 196, 208));
        statusText.setTextSize(12f);
        statusText.setPadding(dp(12), 0, dp(12), dp(6));
        statusText.setSingleLine(false);
        statusText.setMaxLines(2);
        statusText.setEllipsize(TextUtils.TruncateAt.END);
        statusText.setText("第一次使用：在 PC 按「開始配對手機」，再用手機相機掃配對 QR。");
        chrome.addView(statusText, new LinearLayout.LayoutParams(-1, -2));
        updateStreamModeButtons();

        root.addView(chromeView, new LinearLayout.LayoutParams(-1, -2));

        webView = new WebView(this);
        root.addView(webView, new LinearLayout.LayoutParams(-1, 0, 1f));

        setContentView(root);
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void configureWebView() {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setSupportZoom(false);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        if (einkDevice) {
            // Go Color 7 ships with a 0.85 system font scale. Bring WebView text
            // back to roughly 1.0 without changing the user's device-wide setting.
            settings.setTextZoom(118);
            settings.setUserAgentString(settings.getUserAgentString() + " VibeDeck-EInk BOOX-Go-Color");
        }

        webView.addJavascriptInterface(new ShellBridge(), "PhoneMonitorShell");
        webView.setKeepScreenOn(true);
        webView.setWebChromeClient(new WebChromeClient());
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                statusText.setText("已連線：" + url);
                syncViewerChromeFromPage();
                checkForAppUpdate(url);
            }

            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request != null && request.isForMainFrame()) {
                    statusText.setText("連不到 Host。可按「找 Host」，或回 PC 按「開始配對手機」後用相機掃 QR。");
                    if (!autoDiscoveryAttempted) {
                        autoDiscoveryAttempted = true;
                        discoverHost();
                    }
                }
            }
        });
    }

    private void syncViewerChromeFromPage() {
        if (webView == null) {
            return;
        }

        webView.evaluateJavascript(
            "(function(){" +
                "return !!(document.body&&(document.body.classList.contains('viewer-fullscreen')||document.body.classList.contains('dashboard-viewer')));" +
            "})()",
            value -> setViewerChromeHidden("true".equals(value)));
    }

    private void exitViewerFromShell() {
        setViewerChromeHidden(false);
        if (webView != null) {
            webView.evaluateJavascript(
                "(function(){" +
                    "if(window.PhoneMonitorExitViewer){window.PhoneMonitorExitViewer();return;}" +
                    "if(document.body){document.body.classList.remove('viewer-fullscreen','dashboard-viewer');}" +
                    "if(window.PhoneMonitorShell){window.PhoneMonitorShell.setViewerMode(false);}" +
                "})()",
                null);
        }
    }

    private void setViewerChromeHidden(boolean hidden) {
        if (viewerChromeHidden == hidden) {
            return;
        }

        viewerChromeHidden = hidden;
        if (chromeView != null) {
            chromeView.setVisibility(hidden ? View.GONE : View.VISIBLE);
        }

        if (hidden) {
            enterImmersiveMode();
        } else {
            exitImmersiveMode();
        }
    }

    private void enterImmersiveMode() {
        getWindow().getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_FULLSCREEN |
                View.SYSTEM_UI_FLAG_HIDE_NAVIGATION |
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
                View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
                View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION |
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
    }

    private void exitImmersiveMode() {
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
    }

    public final class ShellBridge {
        @JavascriptInterface
        public void setViewerMode(boolean enabled) {
            runOnUiThread(() -> setViewerChromeHidden(enabled));
        }

        @JavascriptInterface
        public void setDeviceToken(String token, String deviceId) {
            prefs.edit()
                .putString(PREF_DEVICE_TOKEN, token == null ? "" : token)
                .putString(PREF_DEVICE_ID, deviceId == null ? "" : deviceId)
                .apply();
        }
    }

    private void addModeButton(LinearLayout row, String label, String mode) {
        Button button = button(label);
        button.setOnClickListener(view -> openMode(mode));
        row.addView(button, new LinearLayout.LayoutParams(0, dp(42), 1f));
    }

    private Button button(String label) {
        Button button = new Button(this);
        button.setText(label);
        button.setAllCaps(false);
        button.setTextSize(12f);
        button.setPadding(dp(4), 0, dp(4), 0);
        button.setTextColor(einkDevice ? Color.rgb(20, 20, 18) : Color.rgb(245, 247, 251));
        button.setBackgroundColor(einkDevice ? Color.rgb(224, 221, 210) : Color.rgb(34, 48, 64));
        return button;
    }

    private boolean isEinkReader() {
        String identity = (Build.MANUFACTURER + " " + Build.BRAND + " " + Build.MODEL + " " + Build.DEVICE)
            .toLowerCase(java.util.Locale.ROOT);
        return identity.contains("boox") || identity.contains("onyx") || identity.contains("go color");
    }

    private String currentStreamMode() {
        String mode = prefs.getString(PREF_STREAM_MODE, STREAM_MODE_H264);
        return STREAM_MODE_JPEG.equals(mode) ? STREAM_MODE_JPEG : STREAM_MODE_H264;
    }

    private void setStreamMode(String mode) {
        String normalized = STREAM_MODE_JPEG.equals(mode) ? STREAM_MODE_JPEG : STREAM_MODE_H264;
        prefs.edit().putString(PREF_STREAM_MODE, normalized).apply();
        updateStreamModeButtons();
        if (statusText != null) {
            statusText.setText(STREAM_MODE_JPEG.equals(normalized)
                ? "串流模式：JPEG Web fallback。資訊板與額度仍會在 PC 開 Deck，再用 Web 顯示器觀看。"
                : "串流模式：H.264 原生。資訊板與額度會在 PC 開 Deck，再進原生顯示器。");
        }
        if (ensureConfiguredHost()) {
            openMode(currentAppMode);
        }
    }

    private void updateStreamModeButtons() {
        String mode = currentStreamMode();
        styleStreamModeButton(h264StreamButton, STREAM_MODE_H264.equals(mode));
        styleStreamModeButton(jpegStreamButton, STREAM_MODE_JPEG.equals(mode));
    }

    private void styleStreamModeButton(Button button, boolean selected) {
        if (button == null) {
            return;
        }

        button.setTextColor(selected ? Color.rgb(10, 19, 17) : Color.rgb(245, 247, 251));
        button.setBackgroundColor(selected ? Color.rgb(112, 217, 139) : Color.rgb(34, 48, 64));
    }

    private void loadHost(String rawUrl) {
        String url = normalizeUrl(rawUrl);
        currentAppMode = modeFromUrl(url);
        hostInput.setText(url);
        prefs.edit().putString(PREF_HOST_URL, url).apply();
        statusText.setText("開啟中：" + url);
        webView.loadUrl(url);
    }

    private void loadMode(String mode) {
        if (!ensureConfiguredHost()) {
            return;
        }

        currentAppMode = normalizeAppMode(mode);
        loadHost(modeUrl(normalizeUrl(currentHostText()), mode));
    }

    private void openMode(String mode) {
        currentAppMode = normalizeAppMode(mode);
        if (einkDevice) {
            loadMode("display".equals(currentAppMode) ? "sideboard" : currentAppMode);
            return;
        }
        if ("display".equals(currentAppMode)) {
            openConfiguredDisplay();
        } else {
            openNativeDeck(mode);
        }
    }

    private void openConfiguredDisplay() {
        if (STREAM_MODE_JPEG.equals(currentStreamMode())) {
            loadMode("display");
        } else {
            openNativeDisplay();
        }
    }

    private void openNativeDisplay() {
        if (!ensureConfiguredHost()) {
            return;
        }

        currentAppMode = "display";
        String url = normalizeUrl(currentHostText());
        prefs.edit().putString(PREF_HOST_URL, url).apply();
        hostInput.setText(url);
        statusText.setText("開啟原生 H.264 顯示器。若尚未配對，請先回 PC 產生配對 QR。");
        Intent intent = new Intent(this, NativeDisplayActivity.class);
        intent.putExtra(NativeDisplayActivity.EXTRA_HOST_URL, url);
        intent.putExtra(NativeDisplayActivity.EXTRA_DEVICE_TOKEN, prefs.getString(PREF_DEVICE_TOKEN, ""));
        startActivity(intent);
    }

    private void openNativeDeck(String mode) {
        if (!ensureConfiguredHost()) {
            return;
        }

        currentAppMode = normalizeDeckMode(mode);
        String token = prefs.getString(PREF_DEVICE_TOKEN, "");
        if (TextUtils.isEmpty(token)) {
            statusText.setText("尚未配對。先回 PC 按「配對手機」，掃 QR 後才能從 App 開 PC Deck。");
            return;
        }

        String hostUrl = normalizeUrl(currentHostText());
        hostInput.setText(hostUrl);
        prefs.edit().putString(PREF_HOST_URL, hostUrl).apply();
        statusText.setText("正在請 PC 開啟 " + deckModeLabel(mode) + " Deck...");

        if (commandExecutor == null || commandExecutor.isShutdown()) {
            commandExecutor = Executors.newSingleThreadExecutor();
        }

        commandExecutor.submit(() -> {
            try {
                JSONObject session = fetchJson(apiUrl(hostUrl, "/api/session"));
                String actionHeader = optJsonString(session, "ActionHeader", "actionHeader");
                String actionToken = optJsonString(session, "ActionToken", "actionToken");
                if (TextUtils.isEmpty(actionHeader)) {
                    actionHeader = "X-PhoneMonitor-Action-Token";
                }

                JSONObject body = new JSONObject();
                body.put("mode", normalizeDeckMode(mode));
                JSONObject result = postJson(
                    apiUrl(hostUrl, "/api/deck/launch"),
                    body,
                    actionHeader,
                    actionToken,
                    token);
                String message = optJsonString(result, "Message", "message");
                runOnUiThread(() -> {
                    statusText.setText(TextUtils.isEmpty(message) ? "Deck 已開啟，切換顯示器串流。" : message);
                    openConfiguredDisplay();
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    statusText.setText("Deck 開啟失敗：" + cleanMessage(ex));
                });
            }
        });
    }

    private void installRootCertificate() {
        if (!ensureConfiguredHost()) {
            return;
        }

        String url = normalizeUrl(currentHostText());
        hostInput.setText(url);
        prefs.edit().putString(PREF_HOST_URL, url).apply();
        String certificateUrl = rootCertificateUrl(url);
        statusText.setText("正在下載憑證，接著請在系統設定安裝 CA 憑證。");

        try {
            DownloadManager downloadManager = (DownloadManager) getSystemService(DOWNLOAD_SERVICE);
            if (downloadManager == null) {
                throw new IllegalStateException("DownloadManager unavailable");
            }

            DownloadManager.Request request = new DownloadManager.Request(Uri.parse(certificateUrl));
            request.setTitle("VibeDeck Root Certificate");
            request.setDescription("請從 Android 設定把它安裝為 CA 憑證。");
            request.setMimeType("application/x-x509-ca-cert");
            request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
            request.setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, ROOT_CERTIFICATE_FILE_NAME);
            downloadManager.enqueue(request);
            openSecuritySettings();
        } catch (Exception ex) {
            statusText.setText("改用瀏覽器開啟憑證：" + certificateUrl);
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(certificateUrl)));
        }
    }

    private void discoverHost() {
        autoDiscoveryAttempted = true;
        if (discoveryExecutor != null) {
            discoveryExecutor.shutdownNow();
        }

        List<String> prefixes = discoveryPrefixes();
        if (prefixes.isEmpty()) {
            statusText.setText("請先輸入 LAN IP，例如 192.168.1.20。");
            return;
        }

        statusText.setText("正在搜尋 VibeDeck Host...");
        discoveryExecutor = Executors.newFixedThreadPool(20);
        AtomicBoolean found = new AtomicBoolean(false);
        CountDownLatch latch = new CountDownLatch(prefixes.size() * 254);

        for (String prefix : prefixes) {
            for (int host = 1; host <= 254; host++) {
                final String candidate = "http://" + prefix + "." + host + ":5000";
                discoveryExecutor.submit(() -> {
                    try {
                        if (!found.get() && isPhoneMonitorHost(candidate + "/health") && found.compareAndSet(false, true)) {
                            runOnUiThread(() -> loadHost(candidate + "/index.html"));
                        }
                    } finally {
                        latch.countDown();
                    }
                });
            }
        }

        discoveryExecutor.submit(() -> {
            try {
                latch.await(9, TimeUnit.SECONDS);
            } catch (InterruptedException ignored) {
                Thread.currentThread().interrupt();
            }

            if (!found.get()) {
                runOnUiThread(() -> statusText.setText("掃描的 LAN 裡找不到 VibeDeck Host。"));
            }
        });
    }

    private List<String> discoveryPrefixes() {
        Set<String> prefixes = new LinkedHashSet<>();
        String manualPrefix = prefixFromHost(normalizeUrl(hostInput.getText().toString()));
        if (manualPrefix != null) {
            prefixes.add(manualPrefix);
        }

        String gatewayPrefix = gatewayPrefix();
        if (gatewayPrefix != null) {
            prefixes.add(gatewayPrefix);
        }

        return new ArrayList<>(prefixes);
    }

    private String gatewayPrefix() {
        try {
            WifiManager wifiManager = (WifiManager) getApplicationContext().getSystemService(Context.WIFI_SERVICE);
            if (wifiManager == null) {
                return null;
            }

            DhcpInfo dhcpInfo = wifiManager.getDhcpInfo();
            if (dhcpInfo == null || dhcpInfo.gateway == 0) {
                return null;
            }

            return prefixFromIpv4(intToIpv4(dhcpInfo.gateway));
        } catch (RuntimeException ex) {
            return null;
        }
    }

    private String intToIpv4(int value) {
        return (value & 0xff) + "." +
            ((value >> 8) & 0xff) + "." +
            ((value >> 16) & 0xff) + "." +
            ((value >> 24) & 0xff);
    }

    private String prefixFromHost(String url) {
        Uri uri = Uri.parse(url);
        return prefixFromIpv4(uri.getHost());
    }

    private String prefixFromIpv4(String host) {
        if (host == null) {
            return null;
        }

        String[] parts = host.split("\\.");
        if (parts.length != 4) {
            return null;
        }

        for (String part : parts) {
            try {
                int value = Integer.parseInt(part);
                if (value < 0 || value > 255) {
                    return null;
                }
            } catch (NumberFormatException ex) {
                return null;
            }
        }

        return parts[0] + "." + parts[1] + "." + parts[2];
    }

    private boolean isPhoneMonitorHost(String healthUrl) {
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(healthUrl).openConnection();
            connection.setConnectTimeout(450);
            connection.setReadTimeout(450);
            connection.setUseCaches(false);
            connection.setRequestMethod("GET");
            if (connection.getResponseCode() != 200) {
                return false;
            }

            BufferedReader reader = new BufferedReader(new InputStreamReader(connection.getInputStream()));
            StringBuilder body = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null && body.length() < 512) {
                body.append(line);
            }

            return body.toString().contains("PhoneMonitor.Host");
        } catch (Exception ex) {
            return false;
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private String rootCertificateUrl(String hostUrl) {
        Uri uri = Uri.parse(normalizeUrl(hostUrl));
        String host = uri.getHost();
        if (TextUtils.isEmpty(host)) {
            return "http://10.0.2.2:5000/cert/phone-monitor-root.cer";
        }

        int port = "https".equalsIgnoreCase(uri.getScheme()) ? 5000 : uri.getPort();
        if (port <= 0) {
            port = 5000;
        }

        return new Uri.Builder()
            .scheme("http")
            .encodedAuthority(host + ":" + port)
            .path("/cert/phone-monitor-root.cer")
            .build()
            .toString();
    }

    private void openSecuritySettings() {
        try {
            startActivity(new Intent(Settings.ACTION_SECURITY_SETTINGS));
        } catch (Exception ex) {
            statusText.setText("請開啟 Android 設定，將 " + ROOT_CERTIFICATE_FILE_NAME + " 安裝為 CA 憑證。");
        }
    }

    private boolean loadFromIntent(Intent intent) {
        if (intent == null || intent.getData() == null) {
            return false;
        }

        Uri data = intent.getData();
        String hostUrl = data.getQueryParameter("host");
        String requestedMode = data.getQueryParameter("mode");
        String mode = normalizeMode(requestedMode);
        if (!TextUtils.isEmpty(hostUrl)) {
            String normalizedHost = normalizeUrl(hostUrl);
            hostInput.setText(normalizedHost);
            prefs.edit().putString(PREF_HOST_URL, normalizedHost).apply();
        }

        if ("cert".equals(requestedMode)) {
            installRootCertificate();
            return true;
        }

        if (mode != null) {
            if (hasPairingCredentials(hostUrl)) {
                loadMode(mode);
            } else {
                openMode(mode);
            }
            return true;
        }

        if (!TextUtils.isEmpty(hostUrl)) {
            loadHost(hostUrl);
            return true;
        }

        return false;
    }

    private boolean hasPairingCredentials(String hostUrl) {
        return !TextUtils.isEmpty(hostUrl) &&
            hostUrl.contains("pairingId=") &&
            hostUrl.contains("pairingSecret=");
    }

    private JSONObject fetchJson(String urlText) throws Exception {
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(urlText).openConnection();
            connection.setConnectTimeout(1500);
            connection.setReadTimeout(2500);
            connection.setUseCaches(false);
            connection.setRequestMethod("GET");
            String body = readResponseBody(connection);
            int code = connection.getResponseCode();
            if (code < 200 || code >= 300) {
                throw new IllegalStateException(errorMessage(body, code));
            }

            return new JSONObject(body);
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private JSONObject postJson(
        String urlText,
        JSONObject body,
        String actionHeader,
        String actionToken,
        String deviceToken) throws Exception {
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(urlText).openConnection();
            connection.setConnectTimeout(1600);
            connection.setReadTimeout(6000);
            connection.setUseCaches(false);
            connection.setRequestMethod("POST");
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/json");
            if (!TextUtils.isEmpty(actionHeader) && !TextUtils.isEmpty(actionToken)) {
                connection.setRequestProperty(actionHeader, actionToken);
            }
            if (!TextUtils.isEmpty(deviceToken)) {
                connection.setRequestProperty("X-PhoneMonitor-Device-Token", deviceToken);
            }

            byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
            connection.setFixedLengthStreamingMode(bytes.length);
            OutputStream stream = connection.getOutputStream();
            stream.write(bytes);
            stream.close();

            String responseBody = readResponseBody(connection);
            int code = connection.getResponseCode();
            if (code < 200 || code >= 300) {
                throw new IllegalStateException(errorMessage(responseBody, code));
            }

            return new JSONObject(responseBody);
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private String readResponseBody(HttpURLConnection connection) throws Exception {
        InputStream stream = connection.getResponseCode() >= 400
            ? connection.getErrorStream()
            : connection.getInputStream();
        if (stream == null) {
            return "";
        }

        BufferedReader reader = new BufferedReader(new InputStreamReader(stream));
        StringBuilder body = new StringBuilder();
        String line;
        while ((line = reader.readLine()) != null && body.length() < 8192) {
            body.append(line);
        }
        return body.toString();
    }

    private String errorMessage(String body, int code) {
        if (!TextUtils.isEmpty(body)) {
            try {
                JSONObject json = new JSONObject(body);
                String message = optJsonString(json, "Message", "message");
                if (TextUtils.isEmpty(message)) {
                    message = json.optString("error", "");
                }
                if (!TextUtils.isEmpty(message)) {
                    return message;
                }
            } catch (Exception ignored) {
            }
        }

        return "HTTP " + code;
    }

    private String apiUrl(String hostUrl, String path) {
        Uri uri = Uri.parse(normalizeUrl(hostUrl));
        String host = uri.getHost();
        if (TextUtils.isEmpty(host)) {
            return "";
        }

        String scheme = TextUtils.isEmpty(uri.getScheme()) ? "http" : uri.getScheme();
        String authority = host + (uri.getPort() > 0 ? ":" + uri.getPort() : "");
        return new Uri.Builder()
            .scheme(scheme)
            .encodedAuthority(authority)
            .path(path)
            .build()
            .toString();
    }

    private String normalizeDeckMode(String mode) {
        return "quota".equals(mode) ? "quota" : "sideboard";
    }

    private String deckModeLabel(String mode) {
        return "quota".equals(mode) ? "額度" : "資訊板";
    }

    private String cleanMessage(Throwable throwable) {
        String message = throwable == null ? "" : throwable.getMessage();
        if (TextUtils.isEmpty(message)) {
            return "未知錯誤";
        }

        return message.length() > 90 ? message.substring(0, 90) + "..." : message;
    }

    private void checkForAppUpdate(String loadedUrl) {
        String releaseUrl = releaseInfoUrl(loadedUrl);
        if (TextUtils.isEmpty(releaseUrl)) {
            return;
        }

        if (updateExecutor == null || updateExecutor.isShutdown()) {
            updateExecutor = Executors.newSingleThreadExecutor();
        }

        updateExecutor.submit(() -> {
            HttpURLConnection connection = null;
            try {
                connection = (HttpURLConnection) new URL(releaseUrl).openConnection();
                connection.setConnectTimeout(1500);
                connection.setReadTimeout(2500);
                connection.setUseCaches(false);
                connection.setRequestMethod("GET");
                if (connection.getResponseCode() != 200) {
                    hideUpdateButton();
                    return;
                }

                BufferedReader reader = new BufferedReader(new InputStreamReader(connection.getInputStream()));
                StringBuilder body = new StringBuilder();
                String line;
                while ((line = reader.readLine()) != null && body.length() < 8192) {
                    body.append(line);
                }

                JSONObject json = new JSONObject(body.toString());
                boolean available = json.optBoolean("Available", json.optBoolean("available", false));
                int remoteVersionCode = optJsonInt(json, "VersionCode", "versionCode");
                String versionName = optJsonString(json, "VersionName", "versionName");
                String downloadUrl = optJsonString(json, "DownloadUrl", "downloadUrl");
                String installPageUrl = optJsonString(json, "InstallPageUrl", "installPageUrl");
                if (available &&
                    remoteVersionCode > BuildConfig.VERSION_CODE &&
                    (!TextUtils.isEmpty(downloadUrl) || !TextUtils.isEmpty(installPageUrl))) {
                    showUpdateButton(versionName, remoteVersionCode, downloadUrl, installPageUrl);
                } else {
                    hideUpdateButton();
                }
            } catch (Exception ex) {
                hideUpdateButton();
            } finally {
                if (connection != null) {
                    connection.disconnect();
                }
            }
        });
    }

    private void showUpdateButton(String versionName, int versionCode, String downloadUrl, String installPageUrl) {
        runOnUiThread(() -> {
            updateDownloadUrl = downloadUrl == null ? "" : downloadUrl;
            updateInstallPageUrl = installPageUrl == null ? "" : installPageUrl;
            String versionLabel = TextUtils.isEmpty(versionName) ? String.valueOf(versionCode) : versionName;
            updateButton.setText("更新 App " + versionLabel);
            updateButton.setVisibility(View.VISIBLE);
            statusText.setText("有新版 VibeDeck Android 可安裝：" + versionLabel + "。");
        });
    }

    private void hideUpdateButton() {
        runOnUiThread(() -> {
            updateDownloadUrl = "";
            updateInstallPageUrl = "";
            if (updateButton != null) {
                updateButton.setVisibility(View.GONE);
            }
        });
    }

    private void openUpdateDownload() {
        String target = TextUtils.isEmpty(updateInstallPageUrl) ? updateDownloadUrl : updateInstallPageUrl;
        if (TextUtils.isEmpty(target)) {
            statusText.setText("目前沒有可用的 App 更新。");
            return;
        }

        Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(target));
        intent.addCategory(Intent.CATEGORY_BROWSABLE);
        startActivity(intent);
    }

    private String releaseInfoUrl(String hostUrl) {
        Uri uri = Uri.parse(normalizeUrl(hostUrl));
        String host = uri.getHost();
        if (TextUtils.isEmpty(host)) {
            return "";
        }

        String scheme = TextUtils.isEmpty(uri.getScheme()) ? "http" : uri.getScheme();
        String authority = host + (uri.getPort() > 0 ? ":" + uri.getPort() : "");
        return new Uri.Builder()
            .scheme(scheme)
            .encodedAuthority(authority)
            .path("/api/android/release")
            .build()
            .toString();
    }

    private String optJsonString(JSONObject json, String pascalName, String camelName) {
        String value = json.optString(pascalName, "");
        return TextUtils.isEmpty(value) ? json.optString(camelName, "") : value;
    }

    private int optJsonInt(JSONObject json, String pascalName, String camelName) {
        if (json.has(pascalName)) {
            return json.optInt(pascalName, 0);
        }

        return json.optInt(camelName, 0);
    }

    private boolean ensureConfiguredHost() {
        if (!TextUtils.isEmpty(currentHostText())) {
            return true;
        }

        statusText.setText("還沒有 Host。請先按「找 Host」，或從 PC 掃下載/配對 QR。");
        return false;
    }

    private String currentHostText() {
        String value = hostInput == null ? "" : hostInput.getText().toString().trim();
        if (!TextUtils.isEmpty(value)) {
            return value;
        }

        return prefs.getString(PREF_HOST_URL, "");
    }

    private String normalizeAppMode(String mode) {
        String normalized = normalizeMode(mode);
        return normalized == null ? "display" : normalized;
    }

    private String modeFromUrl(String url) {
        try {
            return normalizeAppMode(Uri.parse(url).getQueryParameter("mode"));
        } catch (Exception ex) {
            return "display";
        }
    }

    private String modeUrl(String url, String mode) {
        Uri uri = Uri.parse(url);
        Uri.Builder builder = uri.buildUpon();
        String path = uri.getPath();
        if (TextUtils.isEmpty(path) || "/".equals(path)) {
            builder.path("/index.html");
        }

        builder.query(null);
        builder.appendQueryParameter("mode", mode);
        return builder.build().toString();
    }

    private String normalizeUrl(String rawUrl) {
        String value = rawUrl == null ? "" : rawUrl.trim();
        if (value.length() == 0) {
            value = DEFAULT_HOST_URL;
        }

        if (!value.contains("://")) {
            value = "http://" + value;
        }

        Uri uri = Uri.parse(value);
        if (TextUtils.isEmpty(uri.getHost())) {
            return DEFAULT_HOST_URL;
        }

        if (TextUtils.isEmpty(uri.getPath())) {
            return uri.buildUpon().path("/index.html").build().toString();
        }

        return value;
    }

    private String normalizeMode(String mode) {
        if ("display".equals(mode) || "sideboard".equals(mode) || "quota".equals(mode)) {
            return mode;
        }

        return null;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
