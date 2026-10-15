import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ImageViewer } from "./ImageViewer";

describe("ImageViewer", () => {
  it("renders image with alt text and zoom toolbar controls", () => {
    render(<ImageViewer url="blob:test-image" alt="Test Photo" />);

    const img = screen.getByRole("img", { name: "Test Photo" });
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute("src", "blob:test-image");

    expect(
      screen.getByRole("button", { name: "Zoom in" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Zoom out" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Fit to window" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Original size" }),
    ).toBeInTheDocument();
  });

  it("handles zoom in, original size (1:1), and fit image", async () => {
    const user = userEvent.setup();
    render(<ImageViewer url="blob:test-image" alt="Test Photo" />);

    const originalBtn = screen.getByRole("button", { name: "Original size" });
    await user.click(originalBtn);
    expect(screen.getByText("100%")).toBeInTheDocument();

    const zoomInBtn = screen.getByRole("button", { name: "Zoom in" });
    await user.click(zoomInBtn);
    expect(screen.getByText("125%")).toBeInTheDocument();

    const zoomOutBtn = screen.getByRole("button", { name: "Zoom out" });
    await user.click(zoomOutBtn);
    expect(screen.getByText("100%")).toBeInTheDocument();

    const fitBtn = screen.getByRole("button", { name: "Fit to window" });
    await user.click(fitBtn);
    expect(fitBtn).toHaveAttribute("aria-pressed", "true");
  });
});
