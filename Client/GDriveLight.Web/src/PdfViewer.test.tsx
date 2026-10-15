import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PdfViewer } from "./PdfViewer";

describe("PdfViewer", () => {
  it("renders pdf iframe with url and title", () => {
    const { container } = render(
      <PdfViewer url="blob:test-pdf" title="Document.pdf" />,
    );

    const iframe = container.querySelector("iframe");
    expect(iframe).toBeInTheDocument();
    expect(iframe).toHaveAttribute("src", "blob:test-pdf");
    expect(iframe).toHaveAttribute("title", "Document.pdf");
  });

  it("renders toolbar badge and 'Open in new tab' link", () => {
    render(<PdfViewer url="blob:test-pdf" title="Document.pdf" />);

    expect(screen.getByText("PDF")).toBeInTheDocument();
    expect(screen.getByText("Document.pdf")).toBeInTheDocument();

    const openLink = screen.getByRole("link", { name: "Open in new tab" });
    expect(openLink).toBeInTheDocument();
    expect(openLink).toHaveAttribute("href", "blob:test-pdf");
    expect(openLink).toHaveAttribute("target", "_blank");
    expect(openLink).toHaveAttribute("rel", "noopener noreferrer");
  });
});
