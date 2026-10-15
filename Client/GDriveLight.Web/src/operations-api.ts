import { apiResponse, request } from "./api";
import type { DriveItem } from "./drive-api";
import {
  parentOf,
  runItemBatch,
  topLevelItems,
  type LoadFolderPath,
} from "./batch";
export function createFolder(name: string, parentFolderId: string | null) {
  return request<{ folderId: string }>("folders", {
    method: "POST",
    body: JSON.stringify({ name, parentFolderId }),
  });
}
export function renameItem(item: DriveItem, newName: string) {
  return request(
    `${item.kind === "folder" ? "folders" : "files"}/${encodeURIComponent(item.id)}/rename`,
    { method: "PUT", body: JSON.stringify({ newName }) },
  );
}
export function deleteItem(item: DriveItem) {
  return request(
    `${item.kind === "folder" ? "folders" : "files"}/${encodeURIComponent(item.id)}`,
    { method: "DELETE" },
  );
}
export function moveItem(item: DriveItem, destination: string | null) {
  return request(
    `${item.kind === "folder" ? "folders" : "files"}/${encodeURIComponent(item.id)}/move`,
    {
      method: "PUT",
      body: JSON.stringify(
        item.kind === "folder"
          ? { newParentFolderId: destination }
          : { newFolderId: destination },
      ),
    },
  );
}
export async function downloadItem(item: DriveItem, signal?: AbortSignal) {
  const response = await apiResponse(
    `files/${encodeURIComponent(item.id)}/download`,
    { signal },
  );
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = item.name;
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
export async function moveItems(
  items: DriveItem[],
  destination: string | null,
  loadPath: LoadFolderPath,
) {
  const path = destination ? await loadPath(destination) : [];
  if (
    items.some(
      (item) =>
        item.kind === "folder" &&
        (item.id === destination ||
          path.some((folder) => folder.id === item.id)),
    )
  )
    throw new Error("Cannot move a folder into itself or its descendant.");
  const roots = await topLevelItems(items, loadPath);
  return runItemBatch(
    roots.filter((item) => parentOf(item) !== destination),
    (item) => moveItem(item, destination),
  );
}
