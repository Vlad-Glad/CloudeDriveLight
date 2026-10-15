import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { DriveFile } from "./drive-api";
import { PreviewDialog } from "./PreviewDialog";
import { setSession } from "./session";

const sampleFiles: DriveFile[] = [
  {
    id: "file-1",
    name: "Doc.txt",
    fileTypeId: 1,
    sizeBytes: 256,
    contentHash: "hash1",
    folderId: null,
    ownerId: "user-1",
    uploadedAtUtc: "2026-10-10T10:00:00Z",
    editedAtUtc: null,
    fileUrl: "",
  },
  {
    id: "file-2",
    name: "Photo.png",
    fileTypeId: 2,
    sizeBytes: 1024,
    contentHash: "hash2",
    folderId: null,
    ownerId: "user-1",
    uploadedAtUtc: "2026-10-10T11:00:00Z",
    editedAtUtc: null,
    fileUrl: "",
  },
  {
    id: "file-3",
    name: "Archive.zip",
    fileTypeId: 3,
    sizeBytes: 2048,
    contentHash: "hash3",
    folderId: null,
    ownerId: "user-1",
    uploadedAtUtc: "2026-10-10T12:00:00Z",
    editedAtUtc: null,
    fileUrl: "",
  },
];

const catalogTypes = [
  {
    id: 1,
    name: "Text",
    extension: ".txt",
    mimeType: "text/plain",
    previewKind: "text",
  },
  {
    id: 2,
    name: "PNG Image",
    extension: ".png",
    mimeType: "image/png",
    previewKind: "image",
  },
  {
    id: 3,
    name: "Zip Archive",
    extension: ".zip",
    mimeType: "application/zip",
    previewKind: "none",
  },
];

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
  );
}

describe("PreviewDialog", () => {
  beforeEach(() => {
    setSession(
      `header.${btoa(JSON.stringify({ sub: "user-1", exp: Date.now() / 1000 + 3600 }))}.signature`,
    );

    URL.createObjectURL = vi.fn(() => "blob:mock-preview-url");
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("navigates between files using next and previous buttons", async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("files/types")) {
        return Promise.resolve(
          new Response(JSON.stringify(catalogTypes), { status: 200 }),
        );
      }
      return Promise.resolve(
        new Response("Sample text", {
          status: 200,
          headers: { "Content-Type": "text/plain" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);

    renderWithClient(
      <PreviewDialog
        files={sampleFiles}
        initialId="file-1"
        close={vi.fn()}
        restoreFocus={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText("Doc.txt")).toBeInTheDocument();
    });

    const prevBtn = screen.getByRole("button", { name: "Previous file" });
    const nextBtn = screen.getByRole("button", { name: "Next file" });

    expect(prevBtn).toBeDisabled();
    expect(nextBtn).toBeEnabled();

    await user.click(nextBtn);
    expect(screen.getByText("Photo.png")).toBeInTheDocument();
    expect(prevBtn).toBeEnabled();
    expect(nextBtn).toBeEnabled();

    await user.click(nextBtn);
    expect(screen.getByText("Archive.zip")).toBeInTheDocument();
    expect(nextBtn).toBeDisabled();
  });

  it("shows 'No preview available' for unsupported types (previewKind none)", async () => {
    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("files/types")) {
        return Promise.resolve(
          new Response(JSON.stringify(catalogTypes), { status: 200 }),
        );
      }
      return Promise.resolve(new Response("", { status: 415 }));
    });
    vi.stubGlobal("fetch", fetchMock);

    renderWithClient(
      <PreviewDialog
        files={sampleFiles}
        initialId="file-3"
        close={vi.fn()}
        restoreFocus={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText("No preview available")).toBeInTheDocument();
      expect(
        screen.getByText("You can still download this file."),
      ).toBeInTheDocument();
      expect(screen.getByText(/Zip Archive/)).toBeInTheDocument();
    });
  });

  it("identifies preview kind by fileTypeId regardless of extension", async () => {
    // File with name "script.png" but fileTypeId 1 (text)
    const customFiles: DriveFile[] = [
      {
        id: "file-custom",
        name: "script.png",
        fileTypeId: 1, // Text in catalog
        sizeBytes: 120,
        contentHash: "h",
        folderId: null,
        ownerId: "u",
        uploadedAtUtc: "2026-10-10T10:00:00Z",
        editedAtUtc: null,
        fileUrl: "",
      },
    ];

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.includes("files/types")) {
        return Promise.resolve(
          new Response(JSON.stringify(catalogTypes), { status: 200 }),
        );
      }
      return Promise.resolve(
        new Response("echo 'not an image'", {
          status: 200,
          headers: { "Content-Type": "text/plain" },
        }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);

    renderWithClient(
      <PreviewDialog
        files={customFiles}
        initialId="file-custom"
        close={vi.fn()}
        restoreFocus={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(
        screen.getByRole("main", { name: "Preview content" }),
      ).toHaveAttribute("data-preview-kind", "text");
    });
  });

  it("calls close handler when Close button is clicked", async () => {
    const user = userEvent.setup();
    const closeMock = vi.fn();

    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(JSON.stringify(catalogTypes), { status: 200 }),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    renderWithClient(
      <PreviewDialog
        files={sampleFiles}
        initialId="file-1"
        close={closeMock}
        restoreFocus={vi.fn()}
      />,
    );

    const closeBtn = screen.getByRole("button", { name: "Close preview" });
    await user.click(closeBtn);
    expect(closeMock).toHaveBeenCalled();
  });
});
