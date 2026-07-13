export function tuneVideoReceiver(receiver) {
  if (!receiver) return;

  // These hints are optional and differ across Safari/Chromium releases.
  // Feature-detect each field so a newer browser can reduce latency without
  // breaking older WebRTC implementations.
  try {
    if ("jitterBufferTarget" in receiver) receiver.jitterBufferTarget = 0;
    if ("playoutDelayHint" in receiver) receiver.playoutDelayHint = 0;
  } catch {
    // Receiver hints are best-effort; media should continue with defaults.
  }
}
