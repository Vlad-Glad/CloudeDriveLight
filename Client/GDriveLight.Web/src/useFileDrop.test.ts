import { expect, it } from "vitest";
import { filesFromDrop } from "./useFileDrop";
it("takes files from a mixed drop and skips directories without flattening them", () => {
  const file = new File(["hello"], "hello.txt");
  const result = filesFromDrop({
    items: [
      {
        kind: "file",
        webkitGetAsEntry: () => ({ isDirectory: true }),
        getAsFile: () => new File([], "folder"),
      },
      { kind: "file", webkitGetAsEntry: () => null, getAsFile: () => file },
      { kind: "string", getAsFile: () => null },
    ],
  } as unknown as DataTransfer);
  expect(result.files).toEqual([file]);
  expect(result.foldersSkipped).toBe(true);
});
it("supports the FileList fallback", () => {
  const file = new File([], "empty.txt");
  expect(
    filesFromDrop({ items: [], files: [file] } as unknown as DataTransfer),
  ).toEqual({ files: [file], foldersSkipped: false });
});
