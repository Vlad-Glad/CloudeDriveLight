import { expect, it } from "vitest";
import { validateUploadType } from "./upload-validation";
const types = [{ id: 1, name: "Text", extension: ".txt" }];
it("rejects a PDF absent from the server catalog", () => {
  expect(validateUploadType("report.pdf", types)).toBe(
    "File type '.pdf' is not supported.",
  );
});
it("uses the server catalog rather than a hardcoded list", () => {
  expect(
    validateUploadType("report.PDF", [
      ...types,
      { id: 2, name: "PDF", extension: ".pdf" },
    ]),
  ).toBeNull();
  expect(validateUploadType("notes.TXT", types)).toBeNull();
});
it("checks only the last extension and handles missing extensions", () => {
  expect(validateUploadType("report.txt.exe", types)).toBe(
    "File type '.exe' is not supported.",
  );
  expect(validateUploadType("report", types)).toContain("without an extension");
  expect(validateUploadType("report.", types)).toContain(
    "without an extension",
  );
  expect(validateUploadType(".unknown", types)).toBe(
    "File type '.unknown' is not supported.",
  );
});
it("an empty catalog does not allow any extension", () => {
  expect(validateUploadType("notes.txt", [])).not.toBeNull();
});
