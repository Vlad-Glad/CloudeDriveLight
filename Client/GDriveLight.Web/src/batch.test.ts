import { afterEach, expect, it, vi } from "vitest";
import { runItemBatch, topLevelItems } from "./batch";
import { moveItems } from "./operations-api";
import type { DriveFolder, DriveItem } from "./drive-api";
const file = (id: string, folderId: string | null = null) =>
  ({ id, name: `${id}.txt`, kind: "file", folderId }) as DriveItem;
const folder = (id: string, parentFolderId: string | null = null) =>
  ({ id, name: id, kind: "folder", parentFolderId }) as DriveItem;
afterEach(() => vi.unstubAllGlobals());
it("moving a selected parent and its descendants operates only on the parent", async () => {
  const result = await topLevelItems(
    [
      folder("parent"),
      file("child", "subfolder"),
      folder("subfolder", "parent"),
      file("other"),
    ],
    async () => [{ id: "parent" }, { id: "subfolder" }] as DriveFolder[],
  );
  expect(result.map((item) => item.id)).toEqual(["parent", "other"]);
});
it("keeps successful and failed batch results separate for retry", async () => {
  const action = vi
    .fn()
    .mockResolvedValueOnce(undefined)
    .mockRejectedValueOnce(new Error("Duplicate name"));
  const result = await runItemBatch([file("a"), file("b")], action);
  expect(result.succeeded.map((item) => item.id)).toEqual(["a"]);
  expect(
    result.failed.map((failure) => [failure.item.id, failure.message]),
  ).toEqual([["b", "Duplicate name"]]);
});
it("does not mutate any item when a group destination is a selected folder descendant", async () => {
  const fetchMock = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  await expect(
    moveItems(
      [file("first"), folder("parent")],
      "child",
      async () => [{ id: "parent" }, { id: "child" }] as DriveFolder[],
    ),
  ).rejects.toThrow("descendant");
  expect(fetchMock).not.toHaveBeenCalled();
});
it("stops the remainder of a batch after authorization fails", async () => {
  const action = vi
    .fn()
    .mockRejectedValue(
      Object.assign(new Error("Unauthorized"), { status: 401 }),
    );
  const result = await runItemBatch([file("a"), file("b")], action);
  expect(action).toHaveBeenCalledTimes(1);
  expect(result.failed).toHaveLength(2);
});
