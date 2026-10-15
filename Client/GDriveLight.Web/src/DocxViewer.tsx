import { useEffect, useRef, useState } from "react";
import { ZoomIn, ZoomOut, RotateCcw, LoaderCircle, AlertCircle } from "lucide-react";
import styles from "./DocxViewer.module.css";

const ZOOM_STEP = 0.15;
const ZOOM_MIN = 0.4;
const ZOOM_MAX = 2.5;

export function DocxViewer({
  blob,
  title,
}: {
  blob?: Blob;
  title: string;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);

  useEffect(() => {
    let cancelled = false;

    if (!blob) {
      setError("No document data provided.");
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);

    (async () => {
      try {
        const { renderAsync } = await import("docx-preview");
        if (cancelled) return;

        if (containerRef.current) {
          containerRef.current.innerHTML = "";
          await renderAsync(blob, containerRef.current, undefined, {
            inWrapper: true,
            ignoreWidth: false,
            ignoreHeight: false,
            breakPages: true,
          });
        }

        if (!cancelled) {
          setLoading(false);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : "Failed to render Word document.",
          );
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [blob]);

  function zoomIn() {
    setZoom((z) => Math.min(ZOOM_MAX, +(z + ZOOM_STEP).toFixed(2)));
  }

  function zoomOut() {
    setZoom((z) => Math.max(ZOOM_MIN, +(z - ZOOM_STEP).toFixed(2)));
  }

  function resetZoom() {
    setZoom(1);
  }

  return (
    <div className={styles.wrapper}>
      <div className={styles.toolbar}>
        <span className={styles.badge}>DOCX</span>
        <span className={styles.docTitle} title={title}>
          {title}
        </span>
        <div className={styles.zoomControls}>
          <button
            type="button"
            className={styles.toolBtn}
            onClick={zoomOut}
            disabled={zoom <= ZOOM_MIN || loading || !!error}
            aria-label="Zoom out"
            title="Zoom out"
          >
            <ZoomOut size={16} />
          </button>
          <span className={styles.zoomLabel}>{Math.round(zoom * 100)}%</span>
          <button
            type="button"
            className={styles.toolBtn}
            onClick={zoomIn}
            disabled={zoom >= ZOOM_MAX || loading || !!error}
            aria-label="Zoom in"
            title="Zoom in"
          >
            <ZoomIn size={16} />
          </button>
          <div className={styles.separator} />
          <button
            type="button"
            className={styles.toolBtn}
            onClick={resetZoom}
            disabled={zoom === 1 || loading || !!error}
            aria-label="Reset zoom"
            title="Reset zoom to 100%"
          >
            <RotateCcw size={15} />
          </button>
        </div>
      </div>

      <div className={styles.scrollArea}>
        {loading && (
          <div className={styles.loadingPlaceholder} role="status">
            <LoaderCircle className="spin" size={32} />
            <p>Rendering document…</p>
          </div>
        )}

        {error && (
          <div className={styles.errorPlaceholder} role="alert">
            <AlertCircle size={40} color="#f28b82" />
            <h3>Could not display document</h3>
            <p>{error}</p>
          </div>
        )}

        <div
          ref={containerRef}
          className={styles.docContainer}
          style={{
            transform: `scale(${zoom})`,
            display: loading || error ? "none" : "flex",
          }}
          aria-label={`Word document: ${title}`}
        />
      </div>
    </div>
  );
}
