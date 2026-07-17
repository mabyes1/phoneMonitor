const STRIP_COUNT = 24;
const FRAME_INTERVAL = 1000 / 45;

function clamp(value, minimum, maximum) {
  return Math.min(maximum, Math.max(minimum, value));
}

function watchMedia(media, listener) {
  if (typeof media.addEventListener === "function") {
    media.addEventListener("change", listener);
    return;
  }

  media.addListener(listener);
}

export function createEnergyWave() {
  const root = document.getElementById("energyWaveBackdrop");
  const canvas = document.getElementById("energyWaveCanvas");
  const strips = document.getElementById("energyWaveStrips");
  if (!root || !canvas || !strips || root.dataset.energyWaveReady === "true") return;

  root.dataset.energyWaveReady = "true";
  for (let index = 0; index < STRIP_COUNT; index += 1) {
    const strip = document.createElement("span");
    strip.className = "energy-wave-strip";
    strips.append(strip);
  }

  const context = canvas.getContext("2d", { alpha: true, desynchronized: true });
  if (!context) return;

  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const compactViewport = window.matchMedia("(max-width: 900px)");
  const motion = {
    x: 0.5,
    y: 0.5,
    targetX: 0.5,
    targetY: 0.5,
    directionX: 0,
    directionY: 0,
    targetDirectionX: 0,
    targetDirectionY: 0,
  };
  const lastPointer = { x: 0.5, y: 0.5, time: performance.now() };
  let bounds = { width: 0, height: 0, ratio: 1 };
  let animationFrame = 0;
  let lastFrame = 0;
  let running = false;
  let active = false;

  function canShow() {
    return document.body.classList.contains("pc-console") &&
      document.body.classList.contains("mode-sideboard") &&
      !document.body.classList.contains("phone-client") &&
      !document.body.classList.contains("eink-client") &&
      !compactViewport.matches;
  }

  function resize() {
    const rect = root.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width));
    const height = Math.max(1, Math.round(rect.height));
    const ratio = Math.min(window.devicePixelRatio || 1, 1.5);
    if (width === bounds.width && height === bounds.height && ratio === bounds.ratio) return;

    bounds = { width, height, ratio };
    canvas.width = Math.round(width * ratio);
    canvas.height = Math.round(height * ratio);
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  function trace(points, offsetX = 0, offsetY = 0) {
    context.beginPath();
    context.moveTo(points.start.x + offsetX, points.start.y + offsetY);
    context.bezierCurveTo(
      points.controlOne.x + offsetX,
      points.controlOne.y + offsetY,
      points.controlTwo.x + offsetX,
      points.controlTwo.y + offsetY,
      points.end.x + offsetX,
      points.end.y + offsetY,
    );
  }

  function paint(points, width, color, blur = 0, offsetX = 0, offsetY = 0) {
    context.save();
    context.lineCap = "round";
    context.lineJoin = "round";
    context.lineWidth = width;
    context.strokeStyle = color;
    context.filter = blur > 0 ? `blur(${blur}px)` : "none";
    context.shadowBlur = blur > 0 ? blur * 1.45 : 0;
    context.shadowColor = color;
    trace(points, offsetX, offsetY);
    context.stroke();
    context.restore();
  }

  function getPoints(time) {
    const { width, height } = bounds;
    const breathing = 0.58 + Math.sin(time * 0.82) * 0.42;
    const flow = time * 0.56;
    const focusX = motion.x - 0.5;
    const focusY = motion.y - 0.5;
    const responseX = motion.directionX;
    const responseY = motion.directionY;

    return {
      start: { x: -width * 0.08, y: height * 0.62 },
      controlOne: {
        x: width * (0.24 + Math.sin(flow * 1.05) * 0.035 + focusX * 0.06 + responseX * 0.05),
        y: height * (0.7 + Math.sin(flow * 0.74) * 0.13 * breathing + focusY * 0.16 + responseY * 0.08),
      },
      controlTwo: {
        x: width * (0.71 + Math.cos(flow * 0.92) * 0.035 + focusX * 0.08 + responseX * 0.065),
        y: height * (0.3 + Math.sin(flow * 1.1 + 1.15) * 0.14 * breathing + focusY * 0.18 - responseY * 0.1),
      },
      end: { x: width * 1.08, y: height * 0.49 },
    };
  }

  function render(now) {
    if (!bounds.width || !bounds.height) return;
    const { width, height, ratio } = bounds;
    const time = now / 1000;
    const glowScale = clamp(width / 1180, 0.72, 1.22);
    const pulse = 0.72 + Math.sin(time * 1.35) * 0.12;
    const points = getPoints(time);

    context.setTransform(1, 0, 0, 1, 0, 0);
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.setTransform(ratio, 0, 0, ratio, 0, 0);
    context.save();
    context.globalCompositeOperation = "lighter";

    paint(points, 170 * glowScale, "rgba(31, 102, 255, 0.055)", 36 * glowScale);
    paint(points, 104 * glowScale, "rgba(132, 78, 255, 0.11)", 25 * glowScale);
    paint(points, 56 * glowScale, "rgba(66, 220, 255, 0.19)", 15 * glowScale);
    paint(points, 30 * glowScale, "rgba(255, 126, 224, 0.29)", 8 * glowScale);
    paint(points, 14 * glowScale, `rgba(149, 242, 255, ${0.46 + pulse * 0.18})`, 3 * glowScale);
    paint(points, 4.4 * glowScale, `rgba(255, 252, 238, ${0.84 + pulse * 0.14})`);

    const colors = ["rgba(91, 233, 255, 0.64)", "rgba(255, 104, 220, 0.55)", "rgba(255, 196, 93, 0.58)"];
    const stripWidth = width / STRIP_COUNT;
    for (let index = 0; index < STRIP_COUNT; index += 1) {
      const edge = index * stripWidth;
      const displacement = (index % 5 - 2) * 1.8 + motion.directionX * 8;
      context.save();
      context.beginPath();
      context.rect(edge, 0, stripWidth + 1, height);
      context.clip();
      paint(points, 5.2 * glowScale, colors[index % colors.length], 0, displacement, motion.directionY * 8);
      context.restore();
    }

    context.restore();
  }

  function settle(delta) {
    const easing = 1 - Math.exp(-delta * 7);
    motion.x += (motion.targetX - motion.x) * easing;
    motion.y += (motion.targetY - motion.y) * easing;
    motion.directionX += (motion.targetDirectionX - motion.directionX) * easing;
    motion.directionY += (motion.targetDirectionY - motion.directionY) * easing;
    const decay = Math.exp(-delta * 3.4);
    motion.targetDirectionX *= decay;
    motion.targetDirectionY *= decay;
  }

  function loop(now) {
    if (!running) return;
    animationFrame = window.requestAnimationFrame(loop);
    if (now - lastFrame < FRAME_INTERVAL) return;
    const delta = Math.min(0.05, Math.max(0.001, (now - lastFrame) / 1000));
    lastFrame = now;
    settle(delta);
    render(now);
  }

  function start() {
    if (running) return;
    running = true;
    lastFrame = performance.now();
    animationFrame = window.requestAnimationFrame(loop);
  }

  function stop() {
    if (!running) return;
    running = false;
    window.cancelAnimationFrame(animationFrame);
    animationFrame = 0;
  }

  function sync() {
    active = canShow();
    root.hidden = !active;
    root.classList.toggle("is-active", active);
    document.body.classList.toggle("energy-wave-active", active);
    if (!active) {
      stop();
      return;
    }

    resize();
    render(performance.now());
    if (!reduceMotion.matches && !document.hidden) start();
    else stop();
  }

  function updatePointer(event) {
    if (!active || reduceMotion.matches) return;
    const rect = root.getBoundingClientRect();
    const x = clamp((event.clientX - rect.left) / Math.max(rect.width, 1), 0, 1);
    const y = clamp((event.clientY - rect.top) / Math.max(rect.height, 1), 0, 1);
    const now = performance.now();
    const elapsed = Math.max(16, now - lastPointer.time);
    motion.targetX = x;
    motion.targetY = y;
    motion.targetDirectionX = clamp(((x - lastPointer.x) * 1000) / elapsed, -1, 1);
    motion.targetDirectionY = clamp(((y - lastPointer.y) * 1000) / elapsed, -1, 1);
    lastPointer.x = x;
    lastPointer.y = y;
    lastPointer.time = now;
  }

  function resetPointer() {
    motion.targetX = 0.5;
    motion.targetY = 0.5;
    motion.targetDirectionX = 0;
    motion.targetDirectionY = 0;
  }

  const observer = new MutationObserver(sync);
  observer.observe(document.body, { attributes: true, attributeFilter: ["class"] });
  const resizeObserver = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(sync);
  resizeObserver?.observe(root);
  window.addEventListener("pointermove", updatePointer, { passive: true });
  window.addEventListener("blur", resetPointer);
  window.addEventListener("resize", sync, { passive: true });
  document.addEventListener("visibilitychange", sync);
  watchMedia(reduceMotion, sync);
  watchMedia(compactViewport, sync);
  sync();
}
