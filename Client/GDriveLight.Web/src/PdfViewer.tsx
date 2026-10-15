import { ExternalLink } from "lucide-react";
import styles from "./PdfViewer.module.css";

export function PdfViewer({
  url,
  title,
}: {
  url: string;
  title: string;
}) {
  return (
    <div className={styles.wrapper}>
      <div className={styles.toolbar}>
        <span className={styles.badge}>PDF</span>
        <span className={styles.docTitle} title={title}>
          {title}
        </span>
        <a
          href={url}
          target="_blank"
          rel="noopener noreferrer"
          className={styles.openBtn}
          title="Open in new tab"
        >
          <ExternalLink size={14} aria-hidden="true" />
          <span>Open in new tab</span>
        </a>
      </div>
      <div className={styles.frameContainer}>
        <iframe
          src={url}
          title={title}
          className={styles.pdfFrame}
          aria-label={`PDF document: ${title}`}
        />
      </div>
    </div>
  );
}
