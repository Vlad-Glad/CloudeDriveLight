import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { TextViewer } from "./TextViewer";

describe("TextViewer", () => {
  it("renders lines with line numbers and line count badge", async () => {
    const code = "line 1\nline 2\nline 3";
    render(<TextViewer text={code} mimeType="text/plain" />);

    expect(screen.getByText("3 lines")).toBeInTheDocument();
    expect(screen.getByText("1")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("applies syntax highlighting for C# code", async () => {
    const csharpCode = "public class Hello { public int Count { get; set; } }";
    const { container } = render(
      <TextViewer text={csharpCode} mimeType="text/x-csharp" />,
    );

    await waitFor(() => {
      expect(screen.getByText("csharp")).toBeInTheDocument();
    });

    const pre = container.querySelector("pre.hljs");
    expect(pre).toBeInTheDocument();
    expect(pre?.innerHTML).toContain("hljs-keyword");
  });

  it("copies text to clipboard when copy button is clicked", async () => {
    const user = userEvent.setup();
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: writeTextMock },
      configurable: true,
      writable: true,
    });

    const sample = "const x = 42;";
    render(<TextViewer text={sample} mimeType="text/javascript" />);

    const copyBtn = screen.getByRole("button", { name: "Copy to clipboard" });
    await user.click(copyBtn);

    expect(writeTextMock).toHaveBeenCalledWith(sample);
    await waitFor(() => {
      expect(screen.getByText("Copied")).toBeInTheDocument();
    });
  });
});
