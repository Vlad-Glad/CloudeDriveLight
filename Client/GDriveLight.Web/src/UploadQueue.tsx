import {
  CheckCircle2,
  LoaderCircle,
  AlertCircle,
  Upload,
  XCircle,
  X,
} from "lucide-react";
import type { UploadJob } from "./useUploads";
import { formatSize } from "./drive-api";
import styles from "./Operations.module.css";
export function UploadQueue({
  jobs,
  retry,
  dismiss,
  cancel,
}: {
  jobs: UploadJob[];
  retry: (id: string) => void;
  dismiss: () => void;
  cancel: (id: string) => void;
}) {
  if (!jobs.length) return null;
  return (
    <section className={styles.uploads} aria-label="Uploads">
      <header>
        <strong aria-live="polite">
          Uploads · {jobs.filter((job) => job.status === "done").length}/
          {jobs.length} complete
        </strong>
        <button className="textButton" onClick={dismiss}>
          Clear finished
        </button>
      </header>
      <ul>
        {jobs.map((job) => (
          <li key={job.id}>
            <div className={styles.uploadIcon}>
              {job.status === "done" ? (
                <CheckCircle2 color="#137333" size={20} />
              ) : job.status === "error" || job.status === "rejected" ? (
                <AlertCircle color="#b3261e" size={20} />
              ) : job.status === "cancelled" ? (
                <XCircle size={20} />
              ) : job.status === "uploading" ? (
                <LoaderCircle className="spin" size={20} />
              ) : (
                <Upload size={20} />
              )}
            </div>
            <div className={styles.uploadInfo}>
              <strong title={job.file.name}>{job.file.name}</strong>
              <span>
                To {job.folderName} ·{" "}
                {job.status === "done"
                  ? "Complete"
                  : job.status === "rejected"
                    ? "Rejected"
                    : job.status === "error"
                      ? "Failed"
                      : job.status === "cancelled"
                        ? "Cancelled"
                        : job.status === "uploading"
                          ? job.progress === 100
                            ? "Processing on server…"
                            : `Uploading ${job.progress}%`
                          : "Waiting"}{" "}
                · {formatSize(job.file.size)}
              </span>
              {job.status === "uploading" && (
                <progress
                  aria-label={`Upload progress for ${job.file.name}`}
                  value={job.progress}
                  max={100}
                />
              )}
              {job.error && <p role="alert">{job.error}</p>}
            </div>
            {(job.status === "error" || job.status === "cancelled") && (
              <button
                className="textButton"
                aria-label={`Retry ${job.file.name}`}
                onClick={() => retry(job.id)}
              >
                Retry
              </button>
            )}
            {(job.status === "queued" || job.status === "uploading") && (
              <button
                className="iconButton"
                aria-label={`Cancel upload ${job.file.name}`}
                onClick={() => cancel(job.id)}
              >
                <X size={18} />
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
