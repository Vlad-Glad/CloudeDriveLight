import { ApiError, errorMessage } from "./api";
import { expireSession, getSession } from "./session";

// XHR exposes upload progress; completion still waits for the API response.
export function uploadWithProgress(
  file: File,
  folderId: string | null,
  signal: AbortSignal,
  onProgress: (percent: number) => void,
): Promise<{ id: string }> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(new DOMException("Upload cancelled.", "AbortError"));
      return;
    }
    const token = getSession()?.token;
    if (!token) {
      reject(new ApiError("Please sign in again to upload files.", 401));
      return;
    }
    const xhr = new XMLHttpRequest();
    const abort = () => xhr.abort();
    const cleanup = () => signal.removeEventListener("abort", abort);
    const fail = (error: Error) => {
      cleanup();
      reject(error);
    };
    xhr.open("POST", "/api/files");
    xhr.setRequestHeader("Authorization", `Bearer ${token}`);
    xhr.timeout = 15 * 60 * 1000;
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable)
        onProgress(
          Math.min(100, Math.round((event.loaded / event.total) * 100)),
        );
    };
    xhr.onerror = () =>
      fail(
        new ApiError(
          "Connection lost. Check the destination folder before retrying.",
          0,
        ),
      );
    xhr.ontimeout = () =>
      fail(
        new ApiError(
          "Upload timed out. Check the destination folder before retrying.",
          0,
        ),
      );
    xhr.onabort = () =>
      fail(new DOMException("Upload cancelled.", "AbortError"));
    xhr.onload = async () => {
      cleanup();
      if (xhr.status === 401) expireSession(token);
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(
          new ApiError(
            await errorMessage(
              new Response(xhr.responseText || null, {
                status: xhr.status || 502,
              }),
            ),
            xhr.status,
          ),
        );
        return;
      }
      try {
        const result = JSON.parse(xhr.responseText);
        if (typeof result.id !== "string") throw new Error();
        resolve(result);
      } catch {
        reject(
          new ApiError(
            "The server returned an invalid upload response. Check the destination folder before retrying.",
            xhr.status,
          ),
        );
      }
    };
    const data = new FormData();
    data.append("file", file);
    if (folderId) data.append("folderId", folderId);
    signal.addEventListener("abort", abort, { once: true });
    try {
      xhr.send(data);
    } catch (error) {
      fail(error instanceof Error ? error : new Error("Upload failed."));
    }
  });
}
