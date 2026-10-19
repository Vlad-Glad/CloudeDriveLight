import { describe, expect, it } from "vitest";
import { buildApiUrl } from "./api";

describe("buildApiUrl", () => {
  it("defaults to /api/path when VITE_API_BASE_URL is not set", () => {
    expect(buildApiUrl("files")).toBe("/api/files");
    expect(buildApiUrl("/files")).toBe("/api/files");
    expect(buildApiUrl("folders/search?name=test")).toBe("/api/folders/search?name=test");
  });
});
