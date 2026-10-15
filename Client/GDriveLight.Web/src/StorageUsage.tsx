import { useQuery } from "@tanstack/react-query";
import { HardDrive } from "lucide-react";
import { useSession } from "./auth";
import { formatStorageSize, storageUsageQuery } from "./storage-api";
import styles from "./StorageUsage.module.css";

export function StorageUsage() {
  const session = useSession();
  const usage = useQuery(storageUsageQuery(session?.user.id));
  if (!session) return null;

  const data = usage.data;
  const percent = data
    ? data.quotaBytes > 0
      ? Math.min(100, Math.max(0, (data.usedBytes / data.quotaBytes) * 100))
      : 100
    : 0;
  const full = !!data && (data.isOverQuota || data.usedBytes >= data.quotaBytes);
  const used = data && formatStorageSize(data.usedBytes);
  const quota = data && formatStorageSize(data.quotaBytes);

  return (
    <section className={styles.storage} aria-label="Storage">
      <div className={styles.heading}>
        <HardDrive size={18} aria-hidden="true" />
        Storage
      </div>
      {usage.isPending && <p role="status">Loading storage…</p>}
      {data && (
        <>
          <progress
            className={styles.progress}
            data-level={full ? "full" : percent >= 80 ? "warning" : "normal"}
            max={100}
            value={percent}
            aria-label="Storage used"
            aria-valuetext={`${used} used of ${quota}`}
          />
          <p>{used} of {quota} used</p>
          <p>{formatStorageSize(data.availableBytes)} available</p>
          {full ? (
            <p className={styles.warning}>
              {data.isOverQuota ? "Storage limit exceeded." : "Storage is full."}
              {" "}Delete files to free up space.
            </p>
          ) : percent >= 80 ? (
            <p className={styles.warning}>Storage is almost full.</p>
          ) : null}
        </>
      )}
      {usage.isError && (
        <div role="alert">
          <p>{data ? "Could not refresh storage usage." : "Could not load storage usage."}</p>
          <button
            className="textButton"
            onClick={() => void usage.refetch()}
            disabled={usage.isFetching}
          >
            Retry
          </button>
        </div>
      )}
    </section>
  );
}
