import { request } from "./api";
export interface DriveFolder {
  id: string;
  name: string;
  parentFolderId: string | null;
  ownerId: string;
}
export interface DriveFile {
  id: string;
  name: string;
  fileTypeId: number;
  contentHash: string;
  sizeBytes: number;
  folderId: string | null;
  ownerId: string;
  uploadedAtUtc: string;
  editedAtUtc: string | null;
  fileUrl: string;
}
export interface FileType {
  id: number;
  name: string;
  extension: string;
  mimeType?: string;
  previewKind?: "text" | "image" | "pdf" | "docx" | "none";
}
export type DriveItem =
  | (DriveFolder & { kind: "folder" })
  | (DriveFile & { kind: "file" });
export const folderUrl = (id: string | null) =>
  id ? `/folders/${encodeURIComponent(id)}` : "/";
export const foldersQuery = (parentId: string | null) => ({
  queryKey: ["drive", "folders", parentId],
  queryFn: ({ signal }: { signal: AbortSignal }) =>
    request<DriveFolder[]>(
      `folders${parentId ? `?parentFolderId=${encodeURIComponent(parentId)}` : ""}`,
      { signal },
    ),
});
export const pathQuery = (id: string | null) => ({
  queryKey: ["drive", "path", id],
  queryFn: ({ signal }: { signal: AbortSignal }) =>
    id
      ? request<DriveFolder[]>(`folders/${encodeURIComponent(id)}/path`, {
          signal,
        })
      : Promise.resolve([]),
});
export const typesQuery = {
  queryKey: ["fileTypes"],
  queryFn: ({ signal }: { signal: AbortSignal }) =>
    request<FileType[]>("files/types", { signal }),
  staleTime: 300_000,
};
export async function loadItems(
  folderId: string | null,
  search: string,
  signal: AbortSignal,
  synced = false,
): Promise<DriveItem[]> {
  if (synced && search && folderId) {
    // Global search excludes sync trees, so search only this folder's descendants.
    const results: DriveItem[] = [];
    const pending = [folderId];
    const visited = new Set<string>();
    const name = search.toLocaleLowerCase();
    while (pending.length) {
      const id = pending.pop()!;
      if (visited.has(id)) continue;
      visited.add(id);
      const children = await loadItems(id, "", signal);
      for (const item of children) {
        if (item.name.toLocaleLowerCase().includes(name)) results.push(item);
        if (item.kind === "folder") pending.push(item.id);
      }
    }
    return results;
  }
  const query = search ? `search?name=${encodeURIComponent(search)}` : "";
  const [folders, files] = await Promise.all([
    request<DriveFolder[]>(
      `folders${search ? `/${query}` : folderId ? `?parentFolderId=${encodeURIComponent(folderId)}` : ""}`,
      { signal },
    ),
    request<DriveFile[]>(
      `files${search ? `/${query}` : folderId ? `?folderId=${encodeURIComponent(folderId)}` : ""}`,
      { signal },
    ),
  ]);
  return [
    ...folders.map((folder) => ({ ...folder, kind: "folder" as const })),
    ...files.map((file) => ({ ...file, kind: "file" as const })),
  ];
}
export type SortKey = "name" | "created" | "modified" | "size";
export function sortItems(
  items: DriveItem[],
  key: SortKey,
  descending: boolean,
  type: string,
) {
  return items
    .filter(
      (item) =>
        item.kind === "folder" || !type || String(item.fileTypeId) === type,
    )
    .sort((a, b) => {
      if (a.kind !== b.kind) return a.kind === "folder" ? -1 : 1;
      let result = a.name.localeCompare(b.name, undefined, {
        numeric: true,
        sensitivity: "base",
      });
      if (a.kind === "file" && b.kind === "file") {
        if (key === "size") result = a.sizeBytes - b.sizeBytes;
        if (key === "created")
          result =
            Date.parse(a.uploadedAtUtc) - Date.parse(b.uploadedAtUtc);
        if (key === "modified")
          result =
            Date.parse(a.editedAtUtc || a.uploadedAtUtc) -
            Date.parse(b.editedAtUtc || b.uploadedAtUtc);
      }
      return descending ? -result : result;
    });
}
export function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), 4);
  return `${(bytes / 1024 ** index).toLocaleString(undefined, { maximumFractionDigits: 1 })} ${["B", "KB", "MB", "GB", "TB"][index]}`;
}
