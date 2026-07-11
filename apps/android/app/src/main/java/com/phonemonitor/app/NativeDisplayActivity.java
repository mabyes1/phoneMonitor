package com.phonemonitor.app;

import android.app.Activity;
import android.content.SharedPreferences;
import android.content.pm.ActivityInfo;
import android.graphics.Color;
import android.media.MediaCodec;
import android.media.MediaFormat;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.Surface;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.TimeUnit;

import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.Response;
import okhttp3.WebSocket;
import okhttp3.WebSocketListener;
import okio.ByteString;

public final class NativeDisplayActivity extends Activity implements SurfaceHolder.Callback {
    public static final String EXTRA_HOST_URL = "com.phonemonitor.app.HOST_URL";
    public static final String EXTRA_DEVICE_TOKEN = "com.phonemonitor.app.DEVICE_TOKEN";

    private static final String DEFAULT_HOST_URL = "http://10.0.2.2:5000/index.html";
    private static final String PREFS = "phone-monitor";
    private static final String PREF_H264_PRESET = "h264-preset";
    private static final int TOUCH_DRAG_THRESHOLD_DP = 7;
    private static final long TOUCH_LONG_PRESS_MS = 560L;
    private static final StreamPreset[] STREAM_PRESETS = new StreamPreset[] {
        new StreamPreset("battery", "省電", 30, 46),
        new StreamPreset("balanced", "平衡", 45, 54),
        new StreamPreset("smooth", "順暢", 60, 58)
    };

    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final Runnable reconnectRunnable = this::connectSockets;

    private SurfaceView surfaceView;
    private TextView statusText;
    private View topBarView;
    private Button presetButton;
    private OkHttpClient client;
    private SharedPreferences prefs;
    private WebSocket videoSocket;
    private WebSocket inputSocket;
    private H264Decoder decoder;
    private String hostUrl;
    private String deviceToken;
    private StreamPreset streamPreset;
    private boolean surfaceReady;
    private boolean stopped = true;
    private TouchState touchState;
    private final Runnable hideControlsRunnable = () -> setControlsVisible(false);

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        prefs = getSharedPreferences(PREFS, MODE_PRIVATE);
        hostUrl = normalizeUrl(getIntent().getStringExtra(EXTRA_HOST_URL));
        deviceToken = normalizeToken(getIntent().getStringExtra(EXTRA_DEVICE_TOKEN));
        streamPreset = StreamPreset.find(prefs.getString(PREF_H264_PRESET, "smooth"));
        client = new OkHttpClient.Builder()
            .connectTimeout(3, TimeUnit.SECONDS)
            .readTimeout(0, TimeUnit.MILLISECONDS)
            .build();

        configureWindow();
        buildUi();
    }

    @Override
    protected void onResume() {
        super.onResume();
        stopped = false;
        enterImmersiveMode();
        showControlsTemporarily();
        if (surfaceReady) {
            connectSockets();
        }
    }

    @Override
    protected void onPause() {
        stopped = true;
        mainHandler.removeCallbacks(hideControlsRunnable);
        disconnectSockets();
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        mainHandler.removeCallbacksAndMessages(null);
        if (decoder != null) {
            decoder.release();
            decoder = null;
        }

        if (client != null) {
            client.dispatcher().executorService().shutdown();
            client.connectionPool().evictAll();
        }

        super.onDestroy();
    }

    @Override
    public void surfaceCreated(SurfaceHolder holder) {
        surfaceReady = true;
        decoder = new H264Decoder(holder.getSurface(), this::setStatus, streamPreset.fps);
        if (!stopped) {
            connectSockets();
        }
    }

    @Override
    public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) {
    }

    @Override
    public void surfaceDestroyed(SurfaceHolder holder) {
        surfaceReady = false;
        disconnectSockets();
        if (decoder != null) {
            decoder.release();
            decoder = null;
        }
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            enterImmersiveMode();
        }
    }

    private void configureWindow() {
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR);
        Window window = getWindow();
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        window.setStatusBarColor(Color.BLACK);
        window.setNavigationBarColor(Color.BLACK);
    }

    private void buildUi() {
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);

        surfaceView = new SurfaceView(this);
        surfaceView.getHolder().addCallback(this);
        surfaceView.setOnTouchListener(this::handleTouch);
        root.addView(surfaceView, new FrameLayout.LayoutParams(-1, -1));

        LinearLayout topBar = new LinearLayout(this);
        topBarView = topBar;
        topBar.setOrientation(LinearLayout.HORIZONTAL);
        topBar.setGravity(Gravity.CENTER_VERTICAL);
        topBar.setPadding(dp(10), dp(8), dp(10), dp(8));
        topBar.setBackgroundColor(Color.argb(150, 10, 14, 20));

        Button backButton = button("返回");
        backButton.setOnClickListener(view -> finish());
        topBar.addView(backButton, new LinearLayout.LayoutParams(dp(70), dp(40)));

        Button reconnectButton = button("重連");
        reconnectButton.setOnClickListener(view -> {
            disconnectSockets();
            connectSockets();
        });
        topBar.addView(reconnectButton, new LinearLayout.LayoutParams(dp(106), dp(40)));

        presetButton = button("");
        presetButton.setOnClickListener(view -> cycleStreamPreset());
        updatePresetButton();
        topBar.addView(presetButton, new LinearLayout.LayoutParams(dp(88), dp(40)));

        statusText = new TextView(this);
        statusText.setTextColor(Color.rgb(232, 238, 246));
        statusText.setTextSize(12f);
        statusText.setGravity(Gravity.CENTER_VERTICAL);
        statusText.setSingleLine(true);
        statusText.setEllipsize(TextUtils.TruncateAt.END);
        statusText.setText("原生 H.264 等待畫面");
        topBar.addView(statusText, new LinearLayout.LayoutParams(0, dp(40), 1f));

        FrameLayout.LayoutParams topParams = new FrameLayout.LayoutParams(-1, -2);
        topParams.gravity = Gravity.TOP;
        root.addView(topBar, topParams);

        setContentView(root);
    }

    private Button button(String label) {
        Button button = new Button(this);
        button.setText(label);
        button.setAllCaps(false);
        button.setTextColor(Color.WHITE);
        button.setBackgroundColor(Color.rgb(36, 47, 61));
        return button;
    }

    private void connectSockets() {
        if (stopped || !surfaceReady || videoSocket != null) {
            return;
        }

        if (decoder != null) {
            decoder.setStreamFps(streamPreset.fps);
            decoder.reset();
        }

        StreamPreset preset = streamPreset;
        setStatus("H.264 " + preset.label + " 連線中");
        Request videoRequest = new Request.Builder().url(webSocketUrl("/ws/h264-annexb", true, preset)).build();
        videoSocket = client.newWebSocket(videoRequest, new WebSocketListener() {
            @Override
            public void onOpen(WebSocket webSocket, Response response) {
                setStatus("H.264 " + preset.label + " 接收中");
            }

            @Override
            public void onMessage(WebSocket webSocket, ByteString bytes) {
                H264Decoder activeDecoder = decoder;
                if (activeDecoder != null) {
                    activeDecoder.push(bytes.toByteArray());
                }
            }

            @Override
            public void onClosing(WebSocket webSocket, int code, String reason) {
                webSocket.close(code, reason);
            }

            @Override
            public void onClosed(WebSocket webSocket, int code, String reason) {
                if (videoSocket == webSocket) {
                    videoSocket = null;
                    setStatus("原生 H.264 已中斷");
                    scheduleReconnect();
                }
            }

            @Override
            public void onFailure(WebSocket webSocket, Throwable t, Response response) {
                if (videoSocket == webSocket) {
                    videoSocket = null;
                    if (isUnsupportedH264(response)) {
                        setStatus("Host 尚未提供 H.264 編碼器，請先使用 Web 顯示器。");
                    } else {
                        setStatus("原生 H.264 失敗：" + cleanMessage(t));
                        scheduleReconnect();
                    }
                }
            }
        });

        Request inputRequest = new Request.Builder().url(webSocketUrl("/ws/input", false, null)).build();
        inputSocket = client.newWebSocket(inputRequest, new WebSocketListener() {
            @Override
            public void onFailure(WebSocket webSocket, Throwable t, Response response) {
                if (inputSocket == webSocket) {
                    inputSocket = null;
                }
            }

            @Override
            public void onClosed(WebSocket webSocket, int code, String reason) {
                if (inputSocket == webSocket) {
                    inputSocket = null;
                }
            }
        });
    }

    private void disconnectSockets() {
        mainHandler.removeCallbacks(reconnectRunnable);
        WebSocket closingVideo = videoSocket;
        WebSocket closingInput = inputSocket;
        videoSocket = null;
        inputSocket = null;
        if (closingVideo != null) {
            closingVideo.cancel();
        }

        if (closingInput != null) {
            closingInput.cancel();
        }

        if (decoder != null) {
            decoder.reset();
        }
    }

    private void scheduleReconnect() {
        if (!stopped) {
            mainHandler.removeCallbacks(reconnectRunnable);
            mainHandler.postDelayed(reconnectRunnable, 1400L);
        }
    }

    private void cycleStreamPreset() {
        streamPreset = streamPreset.next();
        prefs.edit().putString(PREF_H264_PRESET, streamPreset.id).apply();
        updatePresetButton();
        setStatus("H.264 切換：" + streamPreset.label + " " + streamPreset.fps + "fps / Q" + streamPreset.quality);
        if (!stopped && surfaceReady) {
            disconnectSockets();
            connectSockets();
        }
        showControlsTemporarily();
    }

    private void updatePresetButton() {
        if (presetButton != null) {
            presetButton.setText(streamPreset.label);
        }
    }

    private boolean handleTouch(View view, MotionEvent event) {
        if (event.getActionMasked() == MotionEvent.ACTION_DOWN &&
            topBarView != null &&
            topBarView.getVisibility() != View.VISIBLE &&
            event.getY() <= dp(44)) {
            showControlsTemporarily();
            return true;
        }

        int width = Math.max(1, view.getWidth());
        int height = Math.max(1, view.getHeight());
        float x = clamp(event.getX() / width);
        float y = clamp(event.getY() / height);

        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                beginTouch(x, y, event.getX(), event.getY());
                return true;
            case MotionEvent.ACTION_MOVE:
                updateTouch(x, y, event.getX(), event.getY());
                return true;
            case MotionEvent.ACTION_UP:
                endTouch(x, y, false);
                return true;
            case MotionEvent.ACTION_CANCEL:
                endTouch(x, y, true);
                return true;
            default:
                return true;
        }
    }

    private void beginTouch(float x, float y, float rawX, float rawY) {
        clearTouchTimer();
        touchState = new TouchState(x, y, rawX, rawY);
        touchState.longPressRunnable = () -> {
            TouchState state = touchState;
            if (state != null && !state.dragging) {
                state.longPressed = true;
                sendInput("rightclick", state.lastX, state.lastY, 2);
            }
        };
        mainHandler.postDelayed(touchState.longPressRunnable, TOUCH_LONG_PRESS_MS);
    }

    private void updateTouch(float x, float y, float rawX, float rawY) {
        TouchState state = touchState;
        if (state == null || state.longPressed) {
            return;
        }

        state.lastX = x;
        state.lastY = y;

        float distance = (float)Math.hypot(rawX - state.startRawX, rawY - state.startRawY);
        if (!state.dragging && distance >= dp(TOUCH_DRAG_THRESHOLD_DP)) {
            clearTouchTimer();
            state.dragging = true;
            sendInput("pointerdown", state.startX, state.startY, 1);
        }

        if (state.dragging) {
            sendInput("pointermove", x, y, 1);
        }
    }

    private void endTouch(float x, float y, boolean cancelled) {
        TouchState state = touchState;
        if (state == null) {
            return;
        }

        clearTouchTimer();
        if (state.dragging) {
            sendInput(cancelled ? "pointercancel" : "pointerup", x, y, 0);
        } else if (!state.longPressed && !cancelled) {
            sendInput("pointerdown", x, y, 1);
            sendInput("pointerup", x, y, 0);
        }

        touchState = null;
    }

    private void clearTouchTimer() {
        if (touchState != null && touchState.longPressRunnable != null) {
            mainHandler.removeCallbacks(touchState.longPressRunnable);
            touchState.longPressRunnable = null;
        }
    }

    private void sendInput(String type, float x, float y, int buttons) {
        WebSocket socket = inputSocket;
        if (socket == null) {
            return;
        }

        try {
            JSONObject payload = new JSONObject();
            payload.put("type", type);
            payload.put("deviceName", "");
            payload.put("x", x);
            payload.put("y", y);
            payload.put("buttons", buttons);
            socket.send(payload.toString());
        } catch (Exception ignored) {
        }
    }

    private String webSocketUrl(String path, boolean stream, StreamPreset preset) {
        Uri uri = Uri.parse(hostUrl);
        String scheme = "https".equalsIgnoreCase(uri.getScheme()) ? "wss" : "ws";
        Uri.Builder builder = new Uri.Builder()
            .scheme(scheme)
            .encodedAuthority(uri.getEncodedAuthority())
            .path(path);

        if (stream) {
            StreamPreset activePreset = preset == null ? streamPreset : preset;
            builder.appendQueryParameter("fps", String.valueOf(activePreset.fps));
            builder.appendQueryParameter("quality", String.valueOf(activePreset.quality));
        }

        if (!TextUtils.isEmpty(deviceToken)) {
            builder.appendQueryParameter("deviceToken", deviceToken);
        }

        return builder.build().toString();
    }

    private String normalizeToken(String token) {
        return token == null ? "" : token.trim();
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

    private void setStatus(String status) {
        mainHandler.post(() -> statusText.setText(status));
    }

    private void showControlsTemporarily() {
        setControlsVisible(true);
        mainHandler.removeCallbacks(hideControlsRunnable);
        mainHandler.postDelayed(hideControlsRunnable, 1800L);
    }

    private void setControlsVisible(boolean visible) {
        if (topBarView != null) {
            topBarView.setVisibility(visible ? View.VISIBLE : View.GONE);
        }

        if (!visible) {
            enterImmersiveMode();
        }
    }

    private void enterImmersiveMode() {
        getWindow().getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
            View.SYSTEM_UI_FLAG_FULLSCREEN |
            View.SYSTEM_UI_FLAG_HIDE_NAVIGATION |
            View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
            View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION |
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
    }

    private static String cleanMessage(Throwable throwable) {
        String message = throwable == null ? null : throwable.getMessage();
        return TextUtils.isEmpty(message) ? "unknown" : message;
    }

    private static boolean isUnsupportedH264(Response response) {
        return response != null && response.code() == 501;
    }

    private float clamp(float value) {
        return Math.max(0f, Math.min(1f, value));
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private interface StatusSink {
        void update(String status);
    }

    private static final class TouchState {
        final float startX;
        final float startY;
        final float startRawX;
        final float startRawY;
        float lastX;
        float lastY;
        boolean dragging;
        boolean longPressed;
        Runnable longPressRunnable;

        TouchState(float x, float y, float rawX, float rawY) {
            startX = x;
            startY = y;
            startRawX = rawX;
            startRawY = rawY;
            lastX = x;
            lastY = y;
        }
    }

    private static final class StreamPreset {
        final String id;
        final String label;
        final int fps;
        final int quality;

        StreamPreset(String id, String label, int fps, int quality) {
            this.id = id;
            this.label = label;
            this.fps = fps;
            this.quality = quality;
        }

        StreamPreset next() {
            for (int index = 0; index < STREAM_PRESETS.length; index++) {
                if (STREAM_PRESETS[index] == this || STREAM_PRESETS[index].id.equals(id)) {
                    return STREAM_PRESETS[(index + 1) % STREAM_PRESETS.length];
                }
            }

            return STREAM_PRESETS[0];
        }

        static StreamPreset find(String id) {
            for (StreamPreset preset : STREAM_PRESETS) {
                if (preset.id.equals(id)) {
                    return preset;
                }
            }

            return STREAM_PRESETS[STREAM_PRESETS.length - 1];
        }
    }

    private static final class H264Decoder {
        private static final int MAX_ACCESS_UNIT_BYTES = 8 * 1024 * 1024;

        private final Surface surface;
        private final StatusSink statusSink;
        private final AnnexBParser parser = new AnnexBParser();
        private final ByteArrayOutputStream accessUnit = new ByteArrayOutputStream(128 * 1024);

        private MediaCodec codec;
        private byte[] sps;
        private byte[] pps;
        private boolean configured;
        private boolean accessUnitHasVcl;
        private boolean accessUnitIsKeyFrame;
        private long frameIndex;
        private long statsBytes;
        private long statsFrames;
        private long statsLastAtMs;
        private int statsDroppedFrames;
        private int totalDroppedFrames;
        private int streamFps;

        H264Decoder(Surface surface, StatusSink statusSink, int streamFps) {
            this.surface = surface;
            this.statusSink = statusSink;
            this.streamFps = Math.max(1, streamFps);
        }

        synchronized void setStreamFps(int streamFps) {
            this.streamFps = Math.max(1, streamFps);
        }

        synchronized void push(byte[] data) {
            try {
                statsBytes += data.length;
                for (NalUnit nal : parser.push(data)) {
                    processNal(nal);
                }
            } catch (Exception ex) {
                statusSink.update("原生 H.264 解碼失敗：" + cleanMessage(ex));
                reset();
            }
        }

        synchronized void reset() {
            releaseCodec();
            parser.reset();
            accessUnit.reset();
            sps = null;
            pps = null;
            configured = false;
            accessUnitHasVcl = false;
            accessUnitIsKeyFrame = false;
            frameIndex = 0;
            statsBytes = 0;
            statsFrames = 0;
            statsLastAtMs = 0;
            statsDroppedFrames = 0;
            totalDroppedFrames = 0;
        }

        synchronized void release() {
            reset();
        }

        private void processNal(NalUnit nal) throws IOException {
            int type = nal.type();
            if (type == 0) {
                return;
            }

            if (type == 9) {
                submitAccessUnit();
                appendNal(nal);
                return;
            }

            if (type == 7) {
                sps = nal.bytes();
                if (pps != null) {
                    configureCodec();
                }
                if (configured) {
                    appendNal(nal);
                }
                return;
            }

            if (type == 8) {
                pps = nal.bytes();
                if (sps != null) {
                    configureCodec();
                }
                if (configured) {
                    appendNal(nal);
                }
                return;
            }

            if (!configured) {
                return;
            }

            appendNal(nal);
            if (type == 1 || type == 5) {
                accessUnitHasVcl = true;
                accessUnitIsKeyFrame = accessUnitIsKeyFrame || type == 5;
            }
        }

        private void appendNal(NalUnit nal) throws IOException {
            byte[] bytes = nal.bytes();
            accessUnit.write(bytes);
            if (accessUnit.size() > MAX_ACCESS_UNIT_BYTES) {
                accessUnit.reset();
                accessUnitHasVcl = false;
                accessUnitIsKeyFrame = false;
                statusSink.update("原生 H.264 略過過大的影格");
            }
        }

        private void configureCodec() throws IOException {
            if (configured || sps == null || pps == null) {
                return;
            }

            SpsInfo info = SpsParser.parse(sps);
            MediaFormat format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, info.width, info.height);
            format.setByteBuffer("csd-0", ByteBuffer.wrap(sps));
            format.setByteBuffer("csd-1", ByteBuffer.wrap(pps));
            format.setInteger(MediaFormat.KEY_PRIORITY, 0);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1);
            }

            codec = MediaCodec.createDecoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            codec.configure(format, surface, null, 0);
            codec.start();
            configured = true;
            statusSink.update("原生 H.264 解碼器就緒 " + info.width + "x" + info.height);
        }

        private void submitAccessUnit() {
            if (!configured || !accessUnitHasVcl || codec == null) {
                accessUnit.reset();
                accessUnitHasVcl = false;
                accessUnitIsKeyFrame = false;
                return;
            }

            byte[] frame = accessUnit.toByteArray();
            boolean keyFrame = accessUnitIsKeyFrame;
            accessUnit.reset();
            accessUnitHasVcl = false;
            accessUnitIsKeyFrame = false;

            try {
                queueFrame(frame, keyFrame);
            } catch (Exception ex) {
                statsDroppedFrames++;
                totalDroppedFrames++;
                if (totalDroppedFrames == 1 || totalDroppedFrames % 30 == 0) {
                    statusSink.update("原生 H.264 掉幀：" + totalDroppedFrames);
                }
            }
        }

        private void queueFrame(byte[] frame, boolean keyFrame) {
            drainOutput(0);
            int inputIndex = codec.dequeueInputBuffer(0);
            if (inputIndex < 0) {
                drainOutput(1000);
                inputIndex = codec.dequeueInputBuffer(0);
            }

            if (inputIndex < 0) {
                throw new IllegalStateException("decoder input busy");
            }

            ByteBuffer input = codec.getInputBuffer(inputIndex);
            if (input == null || frame.length > input.capacity()) {
                throw new IllegalStateException("frame too large for decoder input");
            }

            input.clear();
            input.put(frame);
            int flags = keyFrame ? MediaCodec.BUFFER_FLAG_KEY_FRAME : 0;
            codec.queueInputBuffer(inputIndex, 0, frame.length, presentationTimeUs(), flags);
            drainOutput(0);
            statsFrames++;
            if (frameIndex == 1) {
                statusSink.update("原生 H.264 播放中");
            }
            reportStatsIfDue();
        }

        private long presentationTimeUs() {
            return (frameIndex++ * 1_000_000L) / streamFps;
        }

        private void reportStatsIfDue() {
            long now = SystemClock.elapsedRealtime();
            if (statsLastAtMs == 0) {
                statsLastAtMs = now;
                return;
            }

            long elapsed = now - statsLastAtMs;
            if (elapsed < 1000) {
                return;
            }

            double fps = statsFrames * 1000.0 / elapsed;
            double mbps = statsBytes * 8.0 / elapsed / 1000.0;
            statusSink.update(String.format(
                Locale.US,
                "H.264 %.0ffps %.1fMbps 掉幀%d",
                fps,
                mbps,
                statsDroppedFrames));
            statsFrames = 0;
            statsBytes = 0;
            statsDroppedFrames = 0;
            statsLastAtMs = now;
        }

        private void drainOutput(long timeoutUs) {
            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
            while (codec != null) {
                int outputIndex = codec.dequeueOutputBuffer(info, timeoutUs);
                if (outputIndex >= 0) {
                    codec.releaseOutputBuffer(outputIndex, info.size > 0);
                    timeoutUs = 0;
                } else if (outputIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED ||
                    outputIndex == MediaCodec.INFO_OUTPUT_BUFFERS_CHANGED) {
                    timeoutUs = 0;
                } else {
                    break;
                }
            }
        }

        private void releaseCodec() {
            if (codec == null) {
                return;
            }

            try {
                codec.stop();
            } catch (Exception ignored) {
            }

            try {
                codec.release();
            } catch (Exception ignored) {
            }

            codec = null;
        }
    }

    private static final class AnnexBParser {
        private final ByteArrayOutputStream buffer = new ByteArrayOutputStream(128 * 1024);

        List<NalUnit> push(byte[] data) throws IOException {
            buffer.write(data);
            List<NalUnit> units = new ArrayList<>();

            while (true) {
                byte[] bytes = buffer.toByteArray();
                int first = findStartCode(bytes, 0);
                if (first < 0) {
                    keepTrailing(bytes, Math.min(bytes.length, 3));
                    break;
                }

                if (first > 0) {
                    rewrite(bytes, first, bytes.length);
                    continue;
                }

                int next = findStartCode(bytes, startCodeEnd(bytes, first));
                if (next < 0) {
                    break;
                }

                units.add(new NalUnit(Arrays.copyOfRange(bytes, first, next)));
                rewrite(bytes, next, bytes.length);
            }

            return units;
        }

        void reset() {
            buffer.reset();
        }

        private void keepTrailing(byte[] bytes, int count) throws IOException {
            buffer.reset();
            if (count > 0) {
                buffer.write(bytes, bytes.length - count, count);
            }
        }

        private void rewrite(byte[] bytes, int start, int end) throws IOException {
            buffer.reset();
            if (end > start) {
                buffer.write(bytes, start, end - start);
            }
        }

        private static int findStartCode(byte[] bytes, int offset) {
            for (int i = Math.max(0, offset); i + 3 < bytes.length; i++) {
                if (bytes[i] == 0 && bytes[i + 1] == 0) {
                    if (bytes[i + 2] == 1) {
                        return i;
                    }

                    if (i + 4 < bytes.length && bytes[i + 2] == 0 && bytes[i + 3] == 1) {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int startCodeEnd(byte[] bytes, int start) {
            return bytes[start + 2] == 1 ? start + 3 : start + 4;
        }
    }

    private static final class NalUnit {
        private final byte[] bytes;

        NalUnit(byte[] bytes) {
            this.bytes = bytes;
        }

        byte[] bytes() {
            return bytes;
        }

        int type() {
            int offset = payloadOffset(bytes);
            if (offset < 0 || offset >= bytes.length) {
                return 0;
            }

            return bytes[offset] & 0x1f;
        }

        static int payloadOffset(byte[] bytes) {
            if (bytes.length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1) {
                return 3;
            }

            if (bytes.length >= 5 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 1) {
                return 4;
            }

            return -1;
        }
    }

    private static final class SpsInfo {
        final int width;
        final int height;

        SpsInfo(int width, int height) {
            this.width = width;
            this.height = height;
        }
    }

    private static final class SpsParser {
        static SpsInfo parse(byte[] annexBNal) {
            int offset = NalUnit.payloadOffset(annexBNal);
            if (offset < 0 || offset >= annexBNal.length - 1) {
                throw new IllegalArgumentException("invalid SPS");
            }

            byte[] rbsp = removeEmulationPrevention(Arrays.copyOfRange(annexBNal, offset + 1, annexBNal.length));
            BitReader bits = new BitReader(rbsp);

            int profileIdc = bits.readBits(8);
            bits.readBits(8);
            bits.readBits(8);
            bits.readUnsignedExpGolomb();

            int chromaFormatIdc = 1;
            boolean separateColourPlane = false;
            if (isHighProfile(profileIdc)) {
                chromaFormatIdc = bits.readUnsignedExpGolomb();
                if (chromaFormatIdc == 3) {
                    separateColourPlane = bits.readBool();
                }

                bits.readUnsignedExpGolomb();
                bits.readUnsignedExpGolomb();
                bits.readBool();
                if (bits.readBool()) {
                    int scalingListCount = chromaFormatIdc != 3 ? 8 : 12;
                    for (int i = 0; i < scalingListCount; i++) {
                        if (bits.readBool()) {
                            skipScalingList(bits, i < 6 ? 16 : 64);
                        }
                    }
                }
            }

            bits.readUnsignedExpGolomb();
            int picOrderCntType = bits.readUnsignedExpGolomb();
            if (picOrderCntType == 0) {
                bits.readUnsignedExpGolomb();
            } else if (picOrderCntType == 1) {
                bits.readBool();
                bits.readSignedExpGolomb();
                bits.readSignedExpGolomb();
                int cycle = bits.readUnsignedExpGolomb();
                for (int i = 0; i < cycle; i++) {
                    bits.readSignedExpGolomb();
                }
            }

            bits.readUnsignedExpGolomb();
            bits.readBool();
            int widthMbsMinus1 = bits.readUnsignedExpGolomb();
            int heightMapUnitsMinus1 = bits.readUnsignedExpGolomb();
            boolean frameMbsOnly = bits.readBool();
            if (!frameMbsOnly) {
                bits.readBool();
            }

            bits.readBool();
            int cropLeft = 0;
            int cropRight = 0;
            int cropTop = 0;
            int cropBottom = 0;
            if (bits.readBool()) {
                cropLeft = bits.readUnsignedExpGolomb();
                cropRight = bits.readUnsignedExpGolomb();
                cropTop = bits.readUnsignedExpGolomb();
                cropBottom = bits.readUnsignedExpGolomb();
            }

            int width = (widthMbsMinus1 + 1) * 16;
            int height = (2 - (frameMbsOnly ? 1 : 0)) * (heightMapUnitsMinus1 + 1) * 16;
            int cropUnitX = 1;
            int cropUnitY = 2 - (frameMbsOnly ? 1 : 0);

            if (!separateColourPlane) {
                if (chromaFormatIdc == 1) {
                    cropUnitX = 2;
                    cropUnitY = 2 * (2 - (frameMbsOnly ? 1 : 0));
                } else if (chromaFormatIdc == 2) {
                    cropUnitX = 2;
                    cropUnitY = 2 - (frameMbsOnly ? 1 : 0);
                } else if (chromaFormatIdc == 3) {
                    cropUnitX = 1;
                    cropUnitY = 2 - (frameMbsOnly ? 1 : 0);
                }
            }

            width -= (cropLeft + cropRight) * cropUnitX;
            height -= (cropTop + cropBottom) * cropUnitY;
            if (width <= 0 || height <= 0) {
                throw new IllegalArgumentException("invalid SPS dimensions");
            }

            return new SpsInfo(width, height);
        }

        private static boolean isHighProfile(int profileIdc) {
            return profileIdc == 100 || profileIdc == 110 || profileIdc == 122 || profileIdc == 244 ||
                profileIdc == 44 || profileIdc == 83 || profileIdc == 86 || profileIdc == 118 ||
                profileIdc == 128 || profileIdc == 138 || profileIdc == 139 || profileIdc == 134 ||
                profileIdc == 135;
        }

        private static void skipScalingList(BitReader bits, int size) {
            int lastScale = 8;
            int nextScale = 8;
            for (int i = 0; i < size; i++) {
                if (nextScale != 0) {
                    int deltaScale = bits.readSignedExpGolomb();
                    nextScale = (lastScale + deltaScale + 256) % 256;
                }
                lastScale = nextScale == 0 ? lastScale : nextScale;
            }
        }

        private static byte[] removeEmulationPrevention(byte[] data) {
            ByteArrayOutputStream output = new ByteArrayOutputStream(data.length);
            for (int i = 0; i < data.length; i++) {
                if (i + 2 < data.length && data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 3) {
                    output.write(0);
                    output.write(0);
                    i += 2;
                } else {
                    output.write(data[i]);
                }
            }

            return output.toByteArray();
        }
    }

    private static final class BitReader {
        private final byte[] data;
        private int bitOffset;

        BitReader(byte[] data) {
            this.data = data;
        }

        boolean readBool() {
            return readBits(1) == 1;
        }

        int readBits(int count) {
            int value = 0;
            for (int i = 0; i < count; i++) {
                value <<= 1;
                if (bitOffset / 8 < data.length) {
                    value |= (data[bitOffset / 8] >> (7 - (bitOffset % 8))) & 1;
                }
                bitOffset++;
            }

            return value;
        }

        int readUnsignedExpGolomb() {
            int zeros = 0;
            while (bitOffset / 8 < data.length && readBits(1) == 0) {
                zeros++;
            }

            int suffix = zeros == 0 ? 0 : readBits(zeros);
            return (1 << zeros) - 1 + suffix;
        }

        int readSignedExpGolomb() {
            int value = readUnsignedExpGolomb();
            int signed = (value + 1) / 2;
            return value % 2 == 0 ? -signed : signed;
        }
    }
}
