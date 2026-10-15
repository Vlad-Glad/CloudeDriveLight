import { useEffect, useRef, useState } from "react";
import { apiResponse } from "./api";

// Max size we'll attempt to display inline (10 MB).
const MAX_PREVIEW_BYTES = 10 * 1024 * 1024;

export type PreviewState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "tooLarge"; sizeBytes: number }
  | { status: "error"; message: string }
  | { status: "ready"; url: string; text: string | null; mimeType: string };

export function usePreviewContent(
  fileId: string,
  kind: string,
  sizeBytes: number,
): PreviewState {
  const [state, setState] = useState<PreviewState>({ status: "idle" });
  const abortRef = useRef<AbortController | null>(null);
  const prevId = useRef<string | null>(null);

  useEffect(() => {
    // Only fetch for supported kinds.
    if (kind === "none" || kind === "pdf") {
      setState({ status: "idle" });
      return;
    }

    // Cancel the previous request when file changes.
    abortRef.current?.abort();

    if (sizeBytes > MAX_PREVIEW_BYTES) {
      setState({ status: "tooLarge", sizeBytes });
      return;
    }

    const controller = new AbortController();
    abortRef.current = controller;
    prevId.current = fileId;
    setState({ status: "loading" });

    (async () => {
      try {
        const response = await apiResponse(
          `files/${encodeURIComponent(fileId)}/preview`,
          { signal: controller.signal },
        );
        if (controller.signal.aborted) return;

        const contentType =
          response.headers.get("content-type")?.split(";")[0].trim() ?? "";
        const blob = await response.blob();
        if (controller.signal.aborted) {
          return;
        }

        let text: string | null = null;
        if (kind === "text") {
          text = await blob.text();
        }
        const url = URL.createObjectURL(blob);
        setState({ status: "ready", url, text, mimeType: contentType });
      } catch (err) {
        if (controller.signal.aborted) return;
        const message =
          err instanceof Error
            ? err.message
            : "Could not load preview. Please try again.";
        setState({ status: "error", message });
      }
    })();

    return () => {
      controller.abort();
      // Revoke any blob URL from previous render of this file.
      setState((prev) => {
        if (prev.status === "ready" && prevId.current === fileId) {
          URL.revokeObjectURL(prev.url);
        }
        return { status: "idle" };
      });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fileId, kind]);

  return state;
}
