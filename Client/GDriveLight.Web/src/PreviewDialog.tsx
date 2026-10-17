import * as Dialog from "@radix-ui/react-dialog";
import { useEffect, useRef, useState, lazy, Suspense } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  Download,
  File,
  FileImage,
  FileText,
  FileX,
  LoaderCircle,
  ShieldOff,
  Wifi,
  X,
} from "lucide-react";
import { formatSize, typesQuery, type DriveFile } from "./drive-api";
import { downloadItem } from "./operations-api";
import { usePreviewContent } from "./usePreviewContent";
import styles from "./PreviewDialog.module.css";

// Lazy-load heavy renderers so they don't inflate the initial bundle.
const ImageViewer = lazy(() =>
  import("./ImageViewer").then((m) => ({ default: m.ImageViewer })),
);
const TextViewer = lazy(() =>
  import("./TextViewer").then((m) => ({ default: m.TextViewer })),
);
const PdfViewer = lazy(() =>
  import("./PdfViewer").then((m) => ({ default: m.PdfViewer })),
);
const DocxViewer = lazy(() =>
  import("./DocxViewer").then((m) => ({ default: m.DocxViewer })),
);

export function PreviewDialog({
  files,
  initialId,
  close,
  restoreFocus,
}: {
  files: DriveFile[];
  initialId: string;
  close: () => void;
  restoreFocus: () => void;
}) {
  const [index, setIndex] = useState(() =>
    Math.max(
      0,
      files.findIndex((file) => file.id === initialId),
    ),
  );
  const safeIndex = files.length > 0 ? Math.min(index, files.length - 1) : -1;
  const file = safeIndex >= 0 ? files[safeIndex] : undefined;
  const viewer = useRef<HTMLDivElement | null>(null);
  const types = useQuery(typesQuery);
  const type = types.data?.find((type) => type.id === file?.fileTypeId);
  const kind = type?.previewKind || "none";
  const Icon =
    kind === "image"
      ? FileImage
      : kind === "text" || kind === "pdf" || kind === "docx"
        ? FileText
        : File;
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState("");
  const controller = useRef<AbortController | null>(null);
  const previewContent = usePreviewContent(
    file?.id ?? "",
    types.isPending ? "none" : kind,
    file?.sizeBytes ?? 0,
  );

  useEffect(() => () => controller.current?.abort(), []);

  useEffect(() => {
    controller.current?.abort();
    setDownloading(false);
    setDownloadError("");
  }, [safeIndex]);

  useEffect(() => {
    if (!file) {
      close();
    }
  }, [file, close]);

  if (!file) {
    return null;
  }

  async function download() {
    if (!file || downloading) return;
    setDownloadError("");
    setDownloading(true);
    const active = new AbortController();
    controller.current = active;
    try {
      await downloadItem({ ...file, kind: "file" }, active.signal);
    } catch (error) {
      if (!active.signal.aborted)
        setDownloadError(
          `Could not download “${file.name}”. ${error instanceof Error ? error.message : "Please try again."}`,
        );
    } finally {
      if (!active.signal.aborted) setDownloading(false);
    }
  }

  function step(offset: number) {
    // Keep arrow keys working when a navigation button becomes disabled at an edge.
    viewer.current?.focus({ preventScroll: true });
    setIndex((current) =>
      Math.max(0, Math.min(files.length - 1, current + offset)),
    );
  }

  return (
    <Dialog.Root
      open
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className={styles.overlay} />
        <Dialog.Content
          ref={viewer}
          className={styles.viewer}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            restoreFocus();
          }}
          onKeyDown={(event) => {
            if (event.altKey || event.ctrlKey || event.metaKey) return;
            if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
              event.preventDefault();
              event.stopPropagation();
              step(event.key === "ArrowLeft" ? -1 : 1);
            }
          }}
        >
          <header className={styles.header}>
            <Dialog.Close className="iconButton" aria-label="Close preview">
              <X size={22} />
            </Dialog.Close>
            <Icon size={24} aria-hidden="true" />
            <Dialog.Title className={styles.title} title={file.name}>
              {file.name}
            </Dialog.Title>
            <button
              className="iconButton"
              aria-label="Download file"
              disabled={downloading}
              onClick={() => void download()}
            >
              {downloading ? (
                <LoaderCircle className="spin" size={22} />
              ) : (
                <Download size={22} />
              )}
            </button>
          </header>
          <Dialog.Description className="srOnly">
            File preview. Use Left and Right arrows to switch files and Escape
            to close.
          </Dialog.Description>
          <div className={styles.content}>
            <button
              className={`iconButton ${styles.previous}`}
              aria-label="Previous file"
              disabled={safeIndex <= 0}
              onClick={() => step(-1)}
            >
              <ChevronLeft size={28} />
            </button>
            <main
              className={styles.body}
              data-preview-kind={kind}
              aria-label="Preview content"
              aria-busy={
                types.isPending ||
                previewContent.status === "loading" ||
                (previewContent.status === "ready" &&
                  previewContent.fileId !== file.id)
              }
            >
              <PreviewBody
                key={file.id}
                kind={kind}
                file={file}
                typeName={type?.name}
                mimeType={type?.mimeType}
                typesPending={types.isPending}
                typesError={types.error}
                onRetryTypes={() => void types.refetch()}
                previewContent={previewContent}
                downloadError={downloadError}
                downloading={downloading}
                onDownload={() => void download()}
              />
            </main>
            <button
              className={`iconButton ${styles.next}`}
              aria-label="Next file"
              disabled={safeIndex >= files.length - 1}
              onClick={() => step(1)}
            >
              <ChevronRight size={28} />
            </button>
          </div>
          <footer className={styles.footer} aria-live="polite">
            {safeIndex + 1} of {files.length}
          </footer>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

// ─── Sub-component so the hook rules stay clean ─────────────────────────────

import type { PreviewState, PreviewErrorKind } from "./usePreviewContent";

function PreviewBody({
  kind,
  file,
  typeName,
  mimeType,
  typesPending,
  typesError,
  onRetryTypes,
  previewContent,
  downloadError,
  downloading,
  onDownload,
}: {
  kind: string;
  file: DriveFile;
  typeName: string | undefined;
  mimeType: string | undefined;
  typesPending: boolean;
  typesError: Error | null;
  onRetryTypes: () => void;
  previewContent: PreviewState;
  downloadError: string;
  downloading: boolean;
  onDownload: () => void;
}) {
  if (typesPending) {
    return (
      <div className={styles.placeholder} role="status">
        <LoaderCircle className="spin" size={36} />
        <p>Loading preview information…</p>
      </div>
    );
  }
  if (typesError) {
    return (
      <div className={styles.placeholder} role="alert">
        <h2>Could not load preview information</h2>
        <p>{typesError.message}</p>
        <button className="secondary" onClick={onRetryTypes}>
          Retry
        </button>
      </div>
    );
  }

  const isCurrentFileReady =
    previewContent.status === "ready" && previewContent.fileId === file.id;
  const isLoading =
    previewContent.status === "loading" ||
    (previewContent.status === "ready" && previewContent.fileId !== file.id);

  return (
    <>
      {isLoading && (
        <div className={styles.placeholder} role="status">
          <LoaderCircle className="spin" size={36} />
          <p>Loading preview…</p>
        </div>
      )}
      {previewContent.status === "error" && (
        <div className={styles.placeholder} role="alert">
          <ErrorIcon kind={previewContent.kind} />
          <h2>{errorHeading(previewContent.kind)}</h2>
          <p>{previewContent.message}</p>
          <p className={styles.details}>
            {typeName || "Unknown file type"} · {formatSize(file.sizeBytes)}
          </p>
          <button className="secondary" onClick={onDownload} disabled={downloading}>
            <Download size={16} />
            Download anyway
          </button>
        </div>
      )}
      {previewContent.status === "tooLarge" && (
        <div className={styles.placeholder}>
          <FileText size={56} aria-hidden="true" />
          <h2>File is too large to preview</h2>
          <p>Only files up to 10 MB can be previewed inline.</p>
          <p className={styles.details}>
            {typeName || "Unknown file type"} · {formatSize(file.sizeBytes)}
          </p>
          <button className="secondary" onClick={onDownload} disabled={downloading}>
            <Download size={16} />
            Download
          </button>
        </div>
      )}
      {isCurrentFileReady && kind === "image" && (
        <Suspense
          fallback={
            <div className={styles.placeholder} role="status">
              <LoaderCircle className="spin" size={36} />
            </div>
          }
        >
          <ImageViewer url={previewContent.url} alt={file.name} />
        </Suspense>
      )}
      {isCurrentFileReady &&
        kind === "text" &&
        previewContent.text !== null && (
          <Suspense
            fallback={
              <div className={styles.placeholder} role="status">
                <LoaderCircle className="spin" size={36} />
              </div>
            }
          >
            <TextViewer
              text={previewContent.text}
              mimeType={mimeType ?? "text/plain"}
            />
          </Suspense>
        )}
      {isCurrentFileReady && kind === "pdf" && (
        <Suspense
          fallback={
            <div className={styles.placeholder} role="status">
              <LoaderCircle className="spin" size={36} />
            </div>
          }
        >
          <PdfViewer url={previewContent.url} title={file.name} />
        </Suspense>
      )}
      {isCurrentFileReady && kind === "docx" && (
        <Suspense
          fallback={
            <div className={styles.placeholder} role="status">
              <LoaderCircle className="spin" size={36} />
            </div>
          }
        >
          <DocxViewer blob={previewContent.blob} title={file.name} />
        </Suspense>
      )}
      {previewContent.status === "idle" &&
        kind !== "image" &&
        kind !== "text" &&
        kind !== "pdf" &&
        kind !== "docx" && (
        <div className={styles.placeholder}>
          <File size={56} aria-hidden="true" />
          <h2>No preview available</h2>
          <p>You can still download this file.</p>
          <p className={styles.details}>
            {typeName || "Unknown file type"} · {formatSize(file.sizeBytes)}
          </p>
        </div>
      )}
      {downloadError && (
        <p className={styles.error} role="alert">
          {downloadError}
        </p>
      )}
      {downloading && <p role="status">Preparing download…</p>}
    </>
  );
}

function errorHeading(kind: PreviewErrorKind): string {
  switch (kind) {
    case "access":   return "Access denied";
    case "notFound": return "File not found";
    case "network":  return "Connection error";
    default:         return "Preview failed to load";
  }
}

function ErrorIcon({ kind }: { kind: PreviewErrorKind }) {
  const props = { size: 56, "aria-hidden": true as const };
  switch (kind) {
    case "access":   return <ShieldOff {...props} />;
    case "notFound": return <FileX {...props} />;
    case "network":  return <Wifi {...props} />;
    default:         return <File {...props} />;
  }
}
