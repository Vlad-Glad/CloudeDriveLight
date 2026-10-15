import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { DocxViewer } from "./DocxViewer";

const renderAsyncMock = vi.fn().mockImplementation((_blob, container) => {
  const p = document.createElement("p");
  p.textContent = "Word Content";
  container.appendChild(p);
  return Promise.resolve();
});

vi.mock("docx-preview", () => ({
  renderAsync: (...args: unknown[]) => renderAsyncMock(...args),
}));

describe("DocxViewer", () => {
  it("renders toolbar with docx badge, title, and zoom controls", async () => {
    const blob = new Blob(["mock docx content"], {
      type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    });

    render(<DocxViewer blob={blob} title="Report.docx" />);

    expect(screen.getByText("DOCX")).toBeInTheDocument();
    expect(screen.getByText("Report.docx")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Word Content")).toBeInTheDocument();
    });

    expect(screen.getByRole("button", { name: "Zoom in" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Zoom out" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset zoom" })).toBeInTheDocument();
  });

  it("handles zoom controls", async () => {
    const user = userEvent.setup();
    const blob = new Blob(["mock docx content"], {
      type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    });

    render(<DocxViewer blob={blob} title="Report.docx" />);

    await waitFor(() => {
      expect(screen.getByText("100%")).toBeInTheDocument();
    });

    const zoomIn = screen.getByRole("button", { name: "Zoom in" });
    await user.click(zoomIn);
    expect(screen.getByText("115%")).toBeInTheDocument();

    const zoomOut = screen.getByRole("button", { name: "Zoom out" });
    await user.click(zoomOut);
    expect(screen.getByText("100%")).toBeInTheDocument();
  });

  it("handles rendering error gracefully", async () => {
    renderAsyncMock.mockRejectedValueOnce(new Error("Corrupt docx file"));
    const blob = new Blob(["bad data"]);

    render(<DocxViewer blob={blob} title="Corrupt.docx" />);

    await waitFor(() => {
      expect(
        screen.getByText("Could not display document"),
      ).toBeInTheDocument();
      expect(screen.getByText("Corrupt docx file")).toBeInTheDocument();
    });
  });
});
