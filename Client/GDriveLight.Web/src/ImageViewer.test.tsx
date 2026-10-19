import { act, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ImageViewer } from "./ImageViewer";

describe("ImageViewer", () => {
  let resize: () => void;

  beforeEach(() => {
    vi.stubGlobal("ResizeObserver", class {
      constructor(callback: () => void) { resize = callback; }
      observe() {}
      disconnect() {}
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  function loadImage(width: number, height: number) {
    const img = screen.getByRole("img") as HTMLImageElement;
    Object.defineProperties(img, {
      naturalWidth: { configurable: true, value: width },
      naturalHeight: { configurable: true, value: height },
      complete: { configurable: true, value: true },
    });
    Object.defineProperties(img.parentElement!, {
      clientWidth: { configurable: true, value: 800 },
      clientHeight: { configurable: true, value: 600 },
    });
    fireEvent.load(img);
    return img;
  }

  it.each([
    [2400, 1200, "33%", 1 / 3],
    [1200, 2400, "25%", 0.25],
    [400, 300, "100%", 1],
  ])("fits a %s by %s image as soon as it loads", (width, height, label, scale) => {
    render(<ImageViewer url="blob:test-image" alt="Test Photo" />);
    const img = loadImage(width, height);

    expect(img.style.transform).toBe(`translate(0px, 0px) scale(${scale})`);
    expect(screen.getByText(label)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Fit to window" }))
      .toHaveAttribute("aria-pressed", "true");
  });

  it("refits on resize, preserves manual zoom, and resets for the next image", async () => {
    const user = userEvent.setup();
    const { rerender } = render(<ImageViewer url="blob:first" alt="Photo" />);
    const img = loadImage(1600, 1200);
    expect(screen.getByText("50%")).toBeInTheDocument();

    Object.defineProperty(img.parentElement!, "clientWidth", { value: 400 });
    act(() => resize());
    expect(screen.getByText("25%")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Zoom in" }));
    expect(screen.getByText("50%")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Original size" }));
    Object.defineProperty(img.parentElement!, "clientHeight", { value: 240 });
    act(() => resize());
    expect(screen.getByText("100%")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Fit to window" }));
    expect(screen.getByText("20%")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Original size" }));
    rerender(<ImageViewer url="blob:second" alt="Photo" />);
    loadImage(2400, 1200);
    expect(screen.getByText("33%")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Fit to window" }))
      .toHaveAttribute("aria-pressed", "true");
  });

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
