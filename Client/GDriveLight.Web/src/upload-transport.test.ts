import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { getSession, setSession } from "./session";
import { uploadWithProgress } from "./upload-transport";
class FakeXHR {
  static instances: FakeXHR[] = [];
  status = 201;
  responseText = '{"id":"uploaded"}';
  timeout = 0;
  headers = new Map<string, string>();
  body?: FormData;
  upload = {
    onprogress: null as
      | null
      | ((event: {
          lengthComputable: boolean;
          loaded: number;
          total: number;
        }) => void),
  };
  onload: null | (() => void | Promise<void>) = null;
  onerror: null | (() => void) = null;
  ontimeout: null | (() => void) = null;
  onabort: null | (() => void) = null;
  open = vi.fn();
  constructor() {
    FakeXHR.instances.push(this);
  }
  setRequestHeader(key: string, value: string) {
    this.headers.set(key, value);
  }
  send(body: FormData) {
    this.body = body;
  }
  abort() {
    this.onabort?.();
  }
}
beforeEach(() => {
  FakeXHR.instances = [];
  vi.stubGlobal("XMLHttpRequest", FakeXHR);
  setSession(
    `header.${btoa(JSON.stringify({ sub: "user", exp: Date.now() / 1000 + 3600 }))}.signature`,
  );
});
afterEach(() => {
  vi.unstubAllGlobals();
  setSession(null);
});
describe("progress upload transport", () => {
  it("sends authenticated multipart and waits for server confirmation after 100%", async () => {
    const progress = vi.fn();
    const done = vi.fn();
    const promise = uploadWithProgress(
      new File(["hello"], "hello.txt"),
      "destination",
      new AbortController().signal,
      progress,
    ).then(done);
    const xhr = FakeXHR.instances[0];
    expect(xhr.open).toHaveBeenCalledWith("POST", "/api/files");
    expect(xhr.headers.get("Authorization")).toMatch(/^Bearer /);
    expect(xhr.headers.has("Content-Type")).toBe(false);
    expect(xhr.body?.get("folderId")).toBe("destination");
    expect((xhr.body?.get("file") as File).name).toBe("hello.txt");
    xhr.upload.onprogress?.({
      lengthComputable: true,
      loaded: 100,
      total: 100,
    });
    expect(progress).toHaveBeenCalledWith(100);
    await Promise.resolve();
    expect(done).not.toHaveBeenCalled();
    await xhr.onload?.();
    await promise;
    expect(done).toHaveBeenCalledWith({ id: "uploaded" });
  });
  it("omits folderId for root and cancels the in-flight request", async () => {
    const controller = new AbortController();
    const promise = uploadWithProgress(
      new File(["hello"], "hello.txt"),
      null,
      controller.signal,
      vi.fn(),
    );
    expect(FakeXHR.instances[0].body?.has("folderId")).toBe(false);
    controller.abort();
    await expect(promise).rejects.toMatchObject({ name: "AbortError" });
  });
  it("does not send a request if already cancelled", async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(
      uploadWithProgress(
        new File([], "empty.txt"),
        null,
        controller.signal,
        vi.fn(),
      ),
    ).rejects.toMatchObject({ name: "AbortError" });
    expect(FakeXHR.instances).toHaveLength(0);
  });
  it("expires a rejected session and returns server errors", async () => {
    const promise = uploadWithProgress(
      new File([], "empty.txt"),
      null,
      new AbortController().signal,
      vi.fn(),
    );
    const xhr = FakeXHR.instances[0];
    xhr.status = 401;
    xhr.responseText = '{"error":"Session expired"}';
    const rejected = expect(promise).rejects.toThrow("Session expired");
    await xhr.onload?.();
    await rejected;
    expect(getSession()).toBeNull();
  });
  it("explains a reverse proxy size rejection with a non-JSON body", async () => {
    const promise = uploadWithProgress(
      new File([], "empty.txt"),
      null,
      new AbortController().signal,
      vi.fn(),
    );
    const xhr = FakeXHR.instances[0];
    xhr.status = 413;
    xhr.responseText = "<html>Too large</html>";
    const rejected = expect(promise).rejects.toThrow("server upload limit");
    await xhr.onload?.();
    await rejected;
  });
});
