import { createPortal } from "react-dom";
import { CheckCircle2, AlertCircle, LoaderCircle, X } from "lucide-react";
import type { ReactNode } from "react";
import styles from "./ActivityPopups.module.css";

export function ActivityPopups({
  notice,
  error,
  moving,
  downloading,
  dismissNotice,
  dismissError,
  cancelDownload,
  children,
}: {
  notice: string;
  error: string;
  moving: boolean;
  downloading: boolean;
  dismissNotice: () => void;
  dismissError: () => void;
  cancelDownload: () => void;
  children: ReactNode;
}) {
  return createPortal(
    <aside className={styles.stack} aria-label="File activity">
      {notice && (
        <div className={styles.message} role="status">
          <CheckCircle2 className={styles.success} size={20} />
          <span>{notice}</span>
          <button
            className="iconButton"
            aria-label="Dismiss notification"
            onClick={dismissNotice}
          >
            <X size={18} />
          </button>
        </div>
      )}
      {error && (
        <div className={`${styles.message} ${styles.error}`} role="alert">
          <AlertCircle size={20} />
          <span>{error}</span>
          <button
            className="iconButton"
            aria-label="Dismiss error"
            onClick={dismissError}
          >
            <X size={18} />
          </button>
        </div>
      )}
      {moving && (
        <div className={styles.message} role="status">
          <LoaderCircle className="spin" size={20} />
          <span>Moving item…</span>
        </div>
      )}
      {downloading && (
        <div className={styles.message} role="status">
          <LoaderCircle className="spin" size={20} />
          <span>Preparing download…</span>
          <button className="textButton" onClick={cancelDownload}>
            Cancel
          </button>
        </div>
      )}
      {children}
    </aside>,
    document.body,
  );
}
