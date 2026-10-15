import { afterEach, describe, expect, it, vi } from "vitest";
import { getSession, parseSession, setSession } from "./session";
import { request } from "./api";
const token = (exp = Date.now() / 1000 + 60) =>
  `header.${btoa(JSON.stringify({ sub: "user-1", email: "test@example.com", firstName: "Test", exp }))}.signature`;
afterEach(() => {
  setSession(null);
  vi.unstubAllGlobals();
});
describe("session and API boundary", () => {
  it("rejects expired and malformed tokens", () => {
    expect(parseSession(token(1))).toBeNull();
    expect(parseSession("broken")).toBeNull();
  });
  it("adds bearer credentials and clears a rejected session", async () => {
    setSession(token());
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ error: "Expired" }), { status: 401 }),
      );
    vi.stubGlobal("fetch", fetchMock);
    await expect(request("files")).rejects.toThrow("Expired");
    expect(fetchMock.mock.calls[0][1].headers.get("Authorization")).toMatch(
      /^Bearer /,
    );
    expect(getSession()).toBeNull();
  });
  it("does not end a new session when an older request returns 401", async () => {
    setSession(token());
    vi.stubGlobal(
      "fetch",
      vi.fn().mockImplementation(async () => {
        setSession(token(Date.now() / 1000 + 600));
        return new Response("", { status: 401 });
      }),
    );
    await expect(request("files")).rejects.toThrow();
    expect(getSession()).not.toBeNull();
  });
  it("preserves validation errors and empty successful responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValueOnce(
          new Response(
            JSON.stringify({ errors: { Name: ["Name is required."] } }),
            { status: 400 },
          ),
        )
        .mockResolvedValueOnce(new Response(null, { status: 204 })),
    );
    await expect(request("folders")).rejects.toThrow("Name is required.");
    await expect(
      request("files/id", { method: "DELETE" }),
    ).resolves.toBeUndefined();
  });
});
