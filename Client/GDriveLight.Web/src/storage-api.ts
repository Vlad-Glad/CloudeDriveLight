import { request } from "./api";

export interface StorageUsageResponse {
  quotaBytes: number;
  usedBytes: number;
  availableBytes: number;
  isOverQuota: boolean;
}

export type StorageUsage = StorageUsageResponse;

export const storageUsageQuery = (userId: string | undefined) => ({
  queryKey: ["drive", "storage", "usage", userId],
  queryFn: ({ signal }: { signal: AbortSignal }) =>
    request<StorageUsageResponse>("storage/usage", { signal }),
  enabled: !!userId,
  staleTime: 15_000,
  refetchOnWindowFocus: "always" as const,
  refetchInterval: 60_000,
  refetchIntervalInBackground: false,
});

export function formatStorageSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  const unit = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), 4);
  return `${(bytes / 1024 ** unit).toLocaleString(undefined, { maximumFractionDigits: 1 })} ${["B", "KiB", "MiB", "GiB", "TiB"][unit]}`;
}
