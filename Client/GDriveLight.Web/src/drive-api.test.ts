import { expect, it, vi, afterEach } from "vitest";
import { loadItems, sortItems, type DriveItem } from "./drive-api";
afterEach(() => vi.unstubAllGlobals());
it("keeps folders first when sorting descending and retains them in a type filter", () => {
  const items = [
    { id: "a", kind: "file", name: "a.pdf", fileTypeId: 1, sizeBytes: 5 },
    { id: "b", kind: "folder", name: "Folder" },
    { id: "c", kind: "file", name: "z.txt", fileTypeId: 2, sizeBytes: 20 },
  ] as DriveItem[];
  expect(sortItems(items, "size", true, "").map((x) => x.id)).toEqual([
    "b",
    "c",
    "a",
  ]);
  expect(sortItems(items, "name", false, "1").map((x) => x.id)).toEqual([
    "b",
    "a",
  ]);
  const datedItems = [
    {
      id: "older",
      kind: "file",
      name: "older.txt",
      fileTypeId: 1,
      uploadedAtUtc: "2026-01-01T00:00:00Z",
    },
    { id: "dir", kind: "folder", name: "Folder" },
    {
      id: "newer",
      kind: "file",
      name: "newer.txt",
      fileTypeId: 1,
      uploadedAtUtc: "2026-05-01T00:00:00Z",
    },
  ] as DriveItem[];
  expect(sortItems(datedItems, "created", false, "").map((x) => x.id)).toEqual([
    "dir",
    "older",
    "newer",
  ]);
  expect(sortItems(datedItems, "created", true, "").map((x) => x.id)).toEqual([
    "dir",
    "newer",
    "older",
  ]);
});
it("searches both files and folders with encoded text", async () => {
  const fetchMock = vi
    .fn()
    .mockImplementation(() => Promise.resolve(new Response("[]")));
  vi.stubGlobal("fetch", fetchMock);
  await loadItems("folder", "a & b", new AbortController().signal);
  expect(fetchMock.mock.calls.map((call) => call[0])).toEqual([
    "/api/folders/search?name=a%20%26%20b",
    "/api/files/search?name=a%20%26%20b",
  ]);
});
