import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { setSession } from "./session";
import { usePreviewContent } from "./usePreviewContent";

describe("usePreviewContent", () => {
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;
  const createdUrls: string[] = [];
  const revokedUrls: string[] = [];

  beforeEach(() => {
    createdUrls.length = 0;
    revokedUrls.length = 0;
    URL.createObjectURL = vi.fn((_blob: Blob) => {
      const url = `blob:mock-${createdUrls.length + 1}`;
      createdUrls.push(url);
      return url;
    });
    URL.revokeObjectURL = vi.fn((url: string) => {
      revokedUrls.push(url);
    });
    setSession(
      `header.${btoa(JSON.stringify({ sub: "user", exp: Date.now() / 1000 + 3600 }))}.signature`,
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
  });

  it("remains idle for unsupported preview kinds without making network requests", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("file-1", "none", 1024),
    );

    expect(result.current).toEqual({ status: "idle" });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns tooLarge state when file size exceeds 10 MB limit", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const largeSize = 12 * 1024 * 1024;
    const { result } = renderHook(() =>
      usePreviewContent("file-1", "text", largeSize),
    );

    expect(result.current).toEqual({
      status: "tooLarge",
      sizeBytes: largeSize,
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("loads and parses text content successfully for text kind", async () => {
    const mockContent = 'Console.WriteLine("Hello, World!");';
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(mockContent, {
        status: 200,
        headers: { "Content-Type": "text/x-csharp; charset=utf-8" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("file-123", "text", 256),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });

    if (result.current.status === "ready") {
      expect(result.current.text).toBe(mockContent);
      expect(result.current.mimeType).toBe("text/x-csharp");
      expect(result.current.url).toBe("blob:mock-1");
    }
  });

  it("loads pdf content successfully without attempting text decoding", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(new Blob(["%PDF-1.7"], { type: "application/pdf" }), {
        status: 200,
        headers: { "Content-Type": "application/pdf" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("file-pdf", "pdf", 512),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });

    if (result.current.status === "ready") {
      expect(result.current.text).toBeNull();
      expect(result.current.mimeType).toBe("application/pdf");
      expect(result.current.url).toBe("blob:mock-1");
    }
  });

  it("loads docx content successfully and retains blob", async () => {
    const docxBlob = new Blob(["PK...mock docx"], {
      type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    });
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(docxBlob, {
        status: 200,
        headers: {
          "Content-Type":
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("file-docx", "docx", 1024),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });

    if (result.current.status === "ready") {
      expect(result.current.text).toBeNull();
      expect(result.current.blob).toBeDefined();
      expect(result.current.mimeType).toBe(
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      );
      expect(result.current.url).toBe("blob:mock-1");
    }
  });

  it("classifies 404 response as notFound error", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: "File not found" }), {
        status: 404,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("missing-file", "image", 1024),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("error");
    });

    if (result.current.status === "error") {
      expect(result.current.kind).toBe("notFound");
      expect(result.current.message).toBe("This file is no longer available.");
    }
  });

  it("classifies 403 response as access error", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: "Forbidden" }), {
        status: 403,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() =>
      usePreviewContent("secret-file", "text", 512),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("error");
    });

    if (result.current.status === "error") {
      expect(result.current.kind).toBe("access");
      expect(result.current.message).toBe(
        "You do not have permission to preview this file.",
      );
    }
  });

  it("revokes blob URL on unmount to prevent memory leaks", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response("img-data", {
        status: 200,
        headers: { "Content-Type": "image/png" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const { result, unmount } = renderHook(() =>
      usePreviewContent("img-1", "image", 5000),
    );

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });

    expect(createdUrls).toHaveLength(1);
    expect(revokedUrls).toHaveLength(0);

    unmount();

    expect(revokedUrls).toContain("blob:mock-1");
  });

  it("transitions immediately to loading state and clears stale state when fileId changes", async () => {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("file-1")) {
        return Promise.resolve(
          new Response("Content 1", {
            status: 200,
            headers: { "Content-Type": "text/plain" },
          }),
        );
      }
      return Promise.resolve(
        new Response("Content 2", {
          status: 200,
          headers: { "Content-Type": "text/plain" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);

    const { result, rerender } = renderHook(
      ({ fileId, kind, sizeBytes }: { fileId: string; kind: string; sizeBytes: number }) =>
        usePreviewContent(fileId, kind, sizeBytes),
      {
        initialProps: { fileId: "file-1", kind: "text", sizeBytes: 100 },
      },
    );

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });
    if (result.current.status === "ready") {
      expect(result.current.fileId).toBe("file-1");
      expect(result.current.text).toBe("Content 1");
    }

    // Change fileId to file-2
    rerender({ fileId: "file-2", kind: "text", sizeBytes: 100 });

    // Immediately upon re-render, status must NOT be ready with file-1 data
    expect(result.current.status).toBe("loading");

    await waitFor(() => {
      expect(result.current.status).toBe("ready");
    });
    if (result.current.status === "ready") {
      expect(result.current.fileId).toBe("file-2");
      expect(result.current.text).toBe("Content 2");
    }
  });
});
