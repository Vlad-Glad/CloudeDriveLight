import type { DriveFolder, DriveItem } from "./drive-api";
export interface BatchResult {
  succeeded: DriveItem[];
  failed: { item: DriveItem; message: string }[];
}
export type LoadFolderPath = (id: string) => Promise<DriveFolder[]>;
export const parentOf = (item: DriveItem) =>
  item.kind === "folder" ? item.parentFolderId : item.folderId;
export async function topLevelItems(
  items: DriveItem[],
  loadPath: LoadFolderPath,
): Promise<DriveItem[]> {
  const folderIds = new Set(
    items.filter((item) => item.kind === "folder").map((item) => item.id),
  );
  if (!folderIds.size) return items;
  const cache = new Map<string, Promise<DriveFolder[]>>();
  const result: DriveItem[] = [];
  for (const item of items) {
    const parent = parentOf(item);
    if (!parent) {
      result.push(item);
      continue;
    }
    if (folderIds.has(parent)) continue;
    if (!cache.has(parent)) cache.set(parent, loadPath(parent));
    if (!(await cache.get(parent)!).some((folder) => folderIds.has(folder.id)))
      result.push(item);
  }
  return result;
}
export async function runItemBatch(
  items: DriveItem[],
  action: (item: DriveItem) => Promise<unknown>,
): Promise<BatchResult> {
  const result: BatchResult = { succeeded: [], failed: [] };
  for (let index = 0; index < items.length; index++) {
    const item = items[index];
    try {
      await action(item);
      result.succeeded.push(item);
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Operation failed.";
      result.failed.push({ item, message });
      if (
        (error as { status?: number })?.status === 401 ||
        (error instanceof Error && error.name === "AbortError")
      ) {
        result.failed.push(
          ...items
            .slice(index + 1)
            .map((item) => ({ item, message: "Operation stopped." })),
        );
        break;
      }
    }
  }
  return result;
}
export function batchError(result: BatchResult): string {
  const details = result.failed
    .map(({ item, message }) => `${item.name}: ${message}`)
    .join(" ");
  return result.succeeded.length
    ? `${result.succeeded.length} completed; ${result.failed.length} failed. ${details}`
    : details;
}
