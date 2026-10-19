import { useEffect, useRef, useState } from "react";
import styles from "./ImageViewer.module.css";
import { ZoomIn, ZoomOut, Maximize2 } from "lucide-react";

const ZOOM_STEP = 0.25;
const ZOOM_MIN = 0.1;
const ZOOM_MAX = 8;

export function ImageViewer({
  url,
  alt,
}: {
  url: string;
  alt: string;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);
  const [zoom, setZoom] = useState<number | null>(null); // null = fit mode
  const [fitZoom, setFitZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const dragging = useRef(false);
  const dragStart = useRef({ mx: 0, my: 0, px: 0, py: 0 });

  // Recalculate fit after decoding the image and whenever the canvas resizes.
  useEffect(() => {
    setZoom(null);
    setPan({ x: 0, y: 0 });
    const img = imgRef.current;
    const container = containerRef.current;
    if (!img || !container) return;

    function updateFitZoom() {
      if (!img || !container || !img.complete || !img.naturalWidth || !img.naturalHeight
        || !container.clientWidth || !container.clientHeight) return;
      setFitZoom(Math.min(
        container.clientWidth / img.naturalWidth,
        container.clientHeight / img.naturalHeight,
        1,
      ));
    }

    img.addEventListener("load", updateFitZoom);
    const observer = new ResizeObserver(updateFitZoom);
    observer.observe(container);
    updateFitZoom();

    return () => {
      img.removeEventListener("load", updateFitZoom);
      observer.disconnect();
    };
  }, [url]);

  function effectiveZoom(): number {
    return zoom ?? fitZoom;
  }

  function zoomBy(delta: number, cx?: number, cy?: number) {
    const current = effectiveZoom();
    const next = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, current + delta));
    if (cx !== undefined && cy !== undefined) {
      // Zoom towards the pointer.
      const ratio = next / current;
      setPan((p) => ({
        x: cx + (p.x - cx) * ratio,
        y: cy + (p.y - cy) * ratio,
      }));
    }
    setZoom(next);
  }

  function fitImage() {
    setZoom(null);
    setPan({ x: 0, y: 0 });
  }

  function originalSize() {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  }

  function onWheel(e: React.WheelEvent) {
    e.preventDefault();
    const rect = containerRef.current!.getBoundingClientRect();
    const cx = e.clientX - rect.left - rect.width / 2;
    const cy = e.clientY - rect.top - rect.height / 2;
    zoomBy(-e.deltaY * 0.001, cx, cy);
  }

  function onPointerDown(e: React.PointerEvent) {
    if (zoom === null) return;
    dragging.current = true;
    dragStart.current = { mx: e.clientX, my: e.clientY, px: pan.x, py: pan.y };
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function onPointerMove(e: React.PointerEvent) {
    if (!dragging.current) return;
    setPan({
      x: dragStart.current.px + e.clientX - dragStart.current.mx,
      y: dragStart.current.py + e.clientY - dragStart.current.my,
    });
  }

  function onPointerUp(e: React.PointerEvent) {
    dragging.current = false;
    (e.currentTarget as HTMLElement).releasePointerCapture(e.pointerId);
  }

  const scale = zoom ?? effectiveZoom();
  const isFit = zoom === null;

  return (
    <div className={styles.wrapper}>
      <div
        ref={containerRef}
        className={styles.canvas}
        style={{ cursor: isFit ? "default" : dragging.current ? "grabbing" : "grab" }}
        onWheel={onWheel}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
      >
        <img
          ref={imgRef}
          src={url}
          alt={alt}
          className={styles.image}
          draggable={false}
          style={{
            transform: `translate(${pan.x}px, ${pan.y}px) scale(${scale})`,
          }}
        />
      </div>
      <div className={styles.toolbar} role="toolbar" aria-label="Image zoom controls">
        <button
          className={styles.toolbarBtn}
          aria-label="Zoom out"
          onClick={() => zoomBy(-ZOOM_STEP)}
          disabled={scale <= ZOOM_MIN}
        >
          <ZoomOut size={18} />
        </button>
        <span className={styles.zoomLabel} aria-live="polite">
          {Math.round(scale * 100)}%
        </span>
        <button
          className={styles.toolbarBtn}
          aria-label="Zoom in"
          onClick={() => zoomBy(ZOOM_STEP)}
          disabled={scale >= ZOOM_MAX}
        >
          <ZoomIn size={18} />
        </button>
        <div className={styles.separator} />
        <button
          className={styles.toolbarBtn}
          aria-label="Fit to window"
          aria-pressed={isFit}
          onClick={fitImage}
        >
          <Maximize2 size={18} />
        </button>
        <button
          className={styles.toolbarBtn}
          aria-label="Original size"
          aria-pressed={!isFit && zoom === 1}
          onClick={originalSize}
        >
          1:1
        </button>
      </div>
    </div>
  );
}
