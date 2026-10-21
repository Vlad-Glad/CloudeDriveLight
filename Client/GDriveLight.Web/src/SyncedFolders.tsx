import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { ChevronRight, FolderSync, LoaderCircle, Monitor, RotateCw } from "lucide-react";
import { request } from "./api";
import { Profile, Shell } from "./Layout";
import styles from "./SyncedFolders.module.css";
import driveStyles from "./Drive.module.css";

interface Device {
  id: string;
  name: string;
  syncRootFolderId: string | null;
  localPath: string | null;
}

export default function SyncedFolders() {
  const devices = useQuery({
    queryKey: ["drive", "devices"],
    queryFn: ({ signal }) => request<Device[]>("devices", { signal }),
  });
  const folders = devices.data?.filter((device) => device.syncRootFolderId) || [];
  return (
    <Shell>
      <header className={driveStyles.header}>
        <h1 className={styles.title}>Synced folders</h1>
        <div className={driveStyles.headerActions}>
          <button
            className="iconButton"
            aria-label="Refresh"
            title="Refresh"
            onClick={() => void devices.refetch()}
          >
            <RotateCw size={20} className={devices.isFetching ? "spin" : ""} />
          </button>
          <Profile />
        </div>
      </header>
      <p className={styles.description}>
        Browse the cloud copies of folders synced from your devices.
      </p>
      {devices.error ? (
        <div className="error" role="alert">
          {devices.error.message}
          <button className="textButton" onClick={() => void devices.refetch()}>
            Try again
          </button>
        </div>
      ) : devices.isPending ? (
        <div className={driveStyles.empty} role="status">
          <LoaderCircle className="spin" size={32} />
          <p>Loading synced folders…</p>
        </div>
      ) : folders.length === 0 ? (
        <div className={driveStyles.empty}>
          <FolderSync size={52} />
          <h2>No synced folders yet</h2>
          <p>Choose a sync folder in the Desktop app to see its cloud copy here.</p>
        </div>
      ) : (
        <ul className={styles.folders} aria-label="Synced folders">
          {folders.map((device) => (
            <li key={device.id}>
              <Link
                className={styles.folder}
                to={`/synced/folders/${encodeURIComponent(device.syncRootFolderId!)}`}
              >
                <FolderSync size={32} aria-hidden="true" />
                <div className={styles.details}>
                  <strong>
                    <Monitor size={16} aria-hidden="true" />
                    {device.name}
                  </strong>
                  <span>{device.localPath || "Synced folder"}</span>
                </div>
                <ChevronRight size={20} aria-hidden="true" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Shell>
  );
}
