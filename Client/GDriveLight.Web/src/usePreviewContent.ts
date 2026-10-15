import { useEffect, useRef, useState } from "react";
import { apiResponse, ApiError } from "./api";

// Maximum inline-preview size (bytes).
// Intent: keep this configurable without a server round-trip for v1.
// A future improvement is to read it from a dedicated API endpoint so the
// limit can be changed server-side without redeploying the client.
const MAX_PREVIEW_BYTES = 10 * 1024 * 1024; // 10 MB

export type PreviewErrorKind =
  | "access"   // HTTP 401 / 403 – not authorised to view this file
  | "notFound" // HTTP 404     – file deleted or never existed
  | "network"  // fetch failed – no connectivity or server unreachable
  | "generic"; // anything else

export type PreviewState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "tooLarge"; sizeBytes: number }
  | { status: "error"; kind: PreviewErrorKind; message: string }
  | { status: "ready"; url: string; text: string | null; mimeType: string; blob?: Blob };

export function usePreviewContent(
  fileId: string,
  kind: string,
  sizeBytes: number,
): PreviewState {
  const [state, setState] = useState<PreviewState>({ status: "idle" });
  const activeUrlRef = useRef<string | null>(null);

  useEffect(() => {
    // Only fetch for supported kinds.
    if (kind === "none") {
      setState({ status: "idle" });
      return;
    }

    if (sizeBytes > MAX_PREVIEW_BYTES) {
      setState({ status: "tooLarge", sizeBytes });
      return;
    }

    const controller = new AbortController();
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
        if (controller.signal.aborted) return;

        let text: string | null = null;
        if (kind === "text") {
          text = await blob.text();
        }
        if (controller.signal.aborted) return;

        if (activeUrlRef.current) {
          URL.revokeObjectURL(activeUrlRef.current);
        }
        const url = URL.createObjectURL(blob);
        activeUrlRef.current = url;

        setState({ status: "ready", url, text, mimeType: contentType, blob });
      } catch (err) {
        if (controller.signal.aborted) return;

        let errorKind: PreviewErrorKind = "generic";
        let message = "Could not load preview. Please try again.";

        if (err instanceof ApiError) {
          message = err.message;
          if (err.status === 401 || err.status === 403) {
            errorKind = "access";
            message = "You do not have permission to preview this file.";
          } else if (err.status === 404) {
            errorKind = "notFound";
            message = "This file is no longer available.";
          }
          // HTTP 415 (preview not supported by server) falls through to generic;
          // the UI already handles unsupported types via the idle/tooLarge path.
        } else if (err instanceof Error && err.name === "AbortError") {
          return; // request was cancelled — not an error to show
        } else if (err instanceof Error && err.message.includes("reach")) {
          errorKind = "network";
          message = err.message;
        }

        setState({ status: "error", kind: errorKind, message });
      }
    })();

    return () => {
      controller.abort();
      if (activeUrlRef.current) {
        URL.revokeObjectURL(activeUrlRef.current);
        activeUrlRef.current = null;
      }
    };
  }, [fileId, kind, sizeBytes]);

  return state;
}
