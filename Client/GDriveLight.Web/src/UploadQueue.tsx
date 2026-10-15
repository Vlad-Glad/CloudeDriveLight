import { CheckCircle2, LoaderCircle, AlertCircle, Upload } from "lucide-react";
import type { UploadJob } from "./useUploads";
import styles from "./Operations.module.css";
export function UploadQueue({
  jobs,
  retry,
  dismiss,
}: {
  jobs: UploadJob[];
  retry: (id: string) => void;
  dismiss: () => void;
}) {
  if (!jobs.length) return null;
  return (
    <section className={styles.uploads} aria-label="Uploads">
      <header>
        <strong>
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
              ) : job.status === "error" ? (
                <AlertCircle color="#b3261e" size={20} />
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
                  : job.status === "error"
                    ? "Failed"
                    : job.status === "uploading"
                      ? "Uploading…"
                      : "Waiting"}
              </span>
              {job.error && <p role="alert">{job.error}</p>}
            </div>
            {job.status === "error" && (
              <button className="textButton" onClick={() => retry(job.id)}>
                Retry
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
