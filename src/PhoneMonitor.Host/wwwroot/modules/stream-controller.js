import { tLegacy } from "./i18n.js?v=3";

export function createStreamController({
  elements,
  getWsBase,
  appendDeviceToken,
  getSelectedDisplayName,
  getStreamSettings,
  canUseProtectedConnection,
  loadPhoneDisplay,
  prefersWebRtcDisplay,
  isLoopbackHost,
  setStatus,
  applyRotation,
  resetJpegStats,
  recordJpegFrame,
  fetchJsonOrThrow,
  tuneVideoReceiver,
}) {
  let videoSocket = null;
  let rtcPeer = null;
  let rtcActive = false;
  let connectGeneration = 0;
  let fallbackReason = "";
  let disconnectTimer = null;
  let statsTimer = null;
  let pendingJpegFrame = null;
  let activeJpegObjectUrl = null;
  let jpegFrameDecoding = false;

  function getActiveStreamElement() {
    return rtcActive ? elements.rtcScreen : elements.screen;
  }

  function closeJpegStream() {
    if (videoSocket) videoSocket.close();
    videoSocket = null;
    clearJpegFrameQueue();
  }

  function closeRtcStream(invalidate = true) {
    if (invalidate) connectGeneration += 1;
    const peer = rtcPeer;
    rtcPeer = null;
    rtcActive = false;
    if (peer) {
      try { peer.close(); } catch { }
    }
    if (disconnectTimer) {
      clearTimeout(disconnectTimer);
      disconnectTimer = null;
    }
    if (statsTimer) {
      clearInterval(statsTimer);
      statsTimer = null;
    }
    elements.rtcScreen.srcObject = null;
    elements.rtcScreen.hidden = true;
    elements.screen.hidden = false;
  }

  function clearJpegFrameQueue() {
    pendingJpegFrame = null;
    if (activeJpegObjectUrl) {
      URL.revokeObjectURL(activeJpegObjectUrl);
      activeJpegObjectUrl = null;
    }
    jpegFrameDecoding = false;
  }

  function presentPendingJpegFrame() {
    if (jpegFrameDecoding || !pendingJpegFrame) return;
    const frame = pendingJpegFrame;
    pendingJpegFrame = null;
    jpegFrameDecoding = true;
    const url = URL.createObjectURL(frame);
    activeJpegObjectUrl = url;

    if (typeof elements.screen.decode === "function") {
      elements.screen.src = url;
      elements.screen.decode().then(() => finishJpegFrameDecode(url), () => finishJpegFrameDecode(url));
      return;
    }

    const complete = () => {
      elements.screen.removeEventListener("load", complete);
      elements.screen.removeEventListener("error", complete);
      finishJpegFrameDecode(url);
    };
    elements.screen.addEventListener("load", complete, { once: true });
    elements.screen.addEventListener("error", complete, { once: true });
    elements.screen.src = url;
  }

  function finishJpegFrameDecode(url) {
    if (!jpegFrameDecoding || url !== activeJpegObjectUrl) return;
    if (activeJpegObjectUrl) {
      URL.revokeObjectURL(activeJpegObjectUrl);
      activeJpegObjectUrl = null;
    }
    jpegFrameDecoding = false;
    requestAnimationFrame(presentPendingJpegFrame);
  }

  async function connect() {
    const generation = ++connectGeneration;
    fallbackReason = "";
    closeJpegStream();
    closeRtcStream(false);

    if (!canUseProtectedConnection()) {
      setStatus(tLegacy("請先配對手機"), false);
      return;
    }
    if (!getSelectedDisplayName()) await loadPhoneDisplay();
    if (generation !== connectGeneration || !getSelectedDisplayName()) return;

    if (prefersWebRtcDisplay() && !window.RTCPeerConnection) {
      fallbackReason = tLegacy("WebRTC API 不可用");
    } else if (prefersWebRtcDisplay() && window.RTCPeerConnection) {
      try {
        const connected = await connectRtcVideo(generation);
        if (connected && generation === connectGeneration) return;
      } catch (error) {
        console.warn("WebRTC negotiation failed; using JPEG fallback", error);
        if (generation === connectGeneration) setStatus(`${tLegacy("WebRTC 無法連線，切回 JPEG：")}${error.message || tLegacy("未知錯誤")}`, false);
        fallbackReason = `WebRTC fallback: ${error.message || tLegacy("未知錯誤")}`;
        closeRtcStream(false);
      }
    }
    if (generation === connectGeneration) connectJpegVideo();
  }

  function waitForIceGatheringComplete(peer, timeoutMs = 1800) {
    if (peer.iceGatheringState === "complete") return Promise.resolve();
    return new Promise(resolve => {
      let finished = false;
      const finish = () => {
        if (finished) return;
        finished = true;
        clearTimeout(timer);
        peer.removeEventListener("icegatheringstatechange", onStateChange);
        resolve();
      };
      const onStateChange = () => {
        if (peer.iceGatheringState === "complete") finish();
      };
      const timer = setTimeout(finish, timeoutMs);
      peer.addEventListener("icegatheringstatechange", onStateChange);
    });
  }

  async function connectRtcVideo(generation) {
    if (generation !== connectGeneration) return false;
    if (!window.isSecureContext && !isLoopbackHost()) throw new Error(tLegacy("WebRTC 需要 HTTPS"));

    const peer = new RTCPeerConnection({ iceServers: [] });
    rtcPeer = peer;
    rtcActive = true;
    elements.screen.hidden = true;
    elements.rtcScreen.hidden = false;
    elements.rtcScreen.onloadedmetadata = () => {
      applyRotation();
      elements.rtcScreen.play().catch(() => {});
      setStatus(tLegacy("WebRTC H.264 已連線"), true);
    };
    peer.ontrack = event => {
      if (generation !== connectGeneration || rtcPeer !== peer) return;
      const stream = event.streams?.[0] || new MediaStream([event.track]);
      elements.rtcScreen.srcObject = stream;
      elements.rtcScreen.play().catch(() => {});
      tuneVideoReceiver(event.receiver);
      startRtcStats(peer, generation);
    };
    peer.onconnectionstatechange = () => {
      if (generation !== connectGeneration || rtcPeer !== peer) return;
      if (peer.connectionState === "disconnected") {
        if (disconnectTimer) clearTimeout(disconnectTimer);
        disconnectTimer = setTimeout(() => {
          disconnectTimer = null;
          if (generation !== connectGeneration || rtcPeer !== peer || peer.connectionState !== "disconnected") return;
          closeRtcStream(false);
          setStatus(tLegacy("WebRTC 中斷（ICE disconnected），切回 JPEG"), false);
          connectJpegVideo("WebRTC ICE disconnected");
        }, 3000);
        return;
      }
      if (["failed", "closed"].includes(peer.connectionState)) {
        closeRtcStream(false);
        setStatus(`${tLegacy("WebRTC 中斷（")}${peer.connectionState}${tLegacy("），切回 JPEG")}`, false);
        connectJpegVideo(`WebRTC ${peer.connectionState}`);
      }
    };
    peer.oniceconnectionstatechange = () => {
      if (generation !== connectGeneration || rtcPeer !== peer) return;
      if (peer.iceConnectionState === "failed") setStatus(tLegacy("WebRTC ICE failed，切回 JPEG"), false);
    };

    peer.addTransceiver("video", { direction: "recvonly" });
    const offer = await peer.createOffer();
    if (generation !== connectGeneration || rtcPeer !== peer) {
      try { peer.close(); } catch { }
      return false;
    }
    await peer.setLocalDescription(offer);
    await waitForIceGatheringComplete(peer);
    if (generation !== connectGeneration || rtcPeer !== peer) return false;

    const settings = getStreamSettings();
    const answer = await fetchJsonOrThrow("/api/stream/webrtc/offer", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        sdp: peer.localDescription?.sdp || offer.sdp,
        deviceName: getSelectedDisplayName(),
        fps: settings.fps,
        quality: settings.quality
      })
    });
    if (generation !== connectGeneration || rtcPeer !== peer || peer.signalingState === "closed") {
      try { peer.close(); } catch { }
      return false;
    }
    await peer.setRemoteDescription({
      type: answer.Type || answer.type || "answer",
      sdp: answer.Sdp || answer.sdp || ""
    });
    return true;
  }

  function startRtcStats(peer, generation) {
    if (statsTimer) clearInterval(statsTimer);
    let previous = null;
    statsTimer = setInterval(async () => {
      if (generation !== connectGeneration || rtcPeer !== peer || peer.connectionState === "closed") return;
      try {
        const reports = await peer.getStats();
        let inbound = null;
        reports.forEach(report => {
          if (report.type === "inbound-rtp" && (report.kind === "video" || report.mediaType === "video")) inbound = report;
        });
        if (!inbound) return;
        const now = Number(inbound.timestamp || performance.now());
        if (previous) {
          const seconds = Math.max(.001, (now - previous.time) / 1000);
          const fps = Math.max(0, (Number(inbound.framesDecoded || 0) - previous.frames) / seconds);
          const mbps = Math.max(0, (Number(inbound.bytesReceived || 0) - previous.bytes) * 8 / seconds / 1e6);
          const dropped = Math.max(0, Number(inbound.framesDropped || 0) - previous.dropped);
          const jitterMs = Math.max(0, Number(inbound.jitter || 0) * 1000);
          const emitted = Number(inbound.jitterBufferEmittedCount || 0);
          const bufferMs = emitted > 0 ? Number(inbound.jitterBufferDelay || 0) / emitted * 1000 : 0;
          const decoded = Number(inbound.framesDecoded || 0);
          const decodeMs = decoded > 0 ? Number(inbound.totalDecodeTime || 0) / decoded * 1000 : 0;
          setStatus(`H.264 ${fps.toFixed(0)}fps ${mbps.toFixed(1)}Mbps · jitter ${jitterMs.toFixed(0)}ms · buffer ${bufferMs.toFixed(0)}ms · decode ${decodeMs.toFixed(1)}ms${dropped ? ` · drop ${dropped}` : ""}`, fps >= 20 && bufferMs < 250);
        }
        previous = {
          time: now,
          frames: Number(inbound.framesDecoded || 0),
          bytes: Number(inbound.bytesReceived || 0),
          dropped: Number(inbound.framesDropped || 0)
        };
      } catch { }
    }, 1000);
  }

  function connectJpegVideo(reason = "") {
    fallbackReason = reason || fallbackReason;
    elements.screen.hidden = false;
    applyRotation();
    const params = appendDeviceToken(new URLSearchParams({
      deviceName: getSelectedDisplayName(),
      fps: getStreamSettings().fps,
      quality: getStreamSettings().quality
    }));
    const socket = new WebSocket(`${getWsBase()}/ws/display?${params.toString()}`);
    videoSocket = socket;
    socket.binaryType = "blob";
    resetJpegStats();
    socket.onopen = () => setStatus(fallbackReason ? `${fallbackReason} · JPEG` : tLegacy("影像已連線"), true);
    socket.onclose = () => {
      if (videoSocket !== socket) return;
      setStatus(tLegacy("重新連線中"), false);
      setTimeout(connect, 1000);
    };
    socket.onerror = () => setStatus(tLegacy("影像連線錯誤"), false);
    socket.onmessage = event => {
      pendingJpegFrame = event.data;
      presentPendingJpegFrame();
      recordJpegFrame(event.data.size, fallbackReason);
      if (getStreamSettings().rotationIsAuto) requestAnimationFrame(applyRotation);
    };
  }

  return {
    connect,
    closeJpegStream,
    closeRtcStream,
    getActiveStreamElement,
  };
}
