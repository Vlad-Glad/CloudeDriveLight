import { afterEach, expect, it, vi } from "vitest";
import { moveItem, uploadFile, deleteItem } from "./operations-api";
import type { DriveItem } from "./drive-api";
afterEach(() => vi.unstubAllGlobals());
it("uses distinct file and folder move contracts", async () => {
  const fetchMock = vi
    .fn()
    .mockImplementation(() =>
      Promise.resolve(new Response(null, { status: 204 })),
    );
  vi.stubGlobal("fetch", fetchMock);
  await moveItem({ id: "a", kind: "file" } as DriveItem, null);
  await moveItem({ id: "b", kind: "folder" } as DriveItem, "parent");
  expect(fetchMock.mock.calls[0][0]).toBe("/api/files/a/move");
  expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
    newFolderId: null,
  });
  expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({
    newParentFolderId: "parent",
  });
});
it("uploads multipart with folderId and lets the browser set the boundary", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(new Response('{"id":"uploaded"}'));
  vi.stubGlobal("fetch", fetchMock);
  await uploadFile(
    new File(["hello"], "test.txt"),
    "folder",
    new AbortController().signal,
  );
  const init = fetchMock.mock.calls[0][1];
  expect(init.body.get("folderId")).toBe("folder");
  expect(init.body.get("file").name).toBe("test.txt");
  expect(init.headers.has("Content-Type")).toBe(false);
});
it("does not hide deletion failures", async () => {
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockResolvedValue(
        new Response('{"error":"Permission denied"}', { status: 403 }),
      ),
  );
  await expect(
    deleteItem({ id: "a", kind: "folder" } as DriveItem),
  ).rejects.toThrow("Permission denied");
});
