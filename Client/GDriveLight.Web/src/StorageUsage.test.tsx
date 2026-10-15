import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { setSession } from "./session";
import { StorageUsage } from "./StorageUsage";

const clients: QueryClient[] = [];
beforeEach(() => {
  setSession(`header.${btoa(JSON.stringify({ sub: "storage-user", exp: Date.now() / 1000 + 3600 }))}.signature`);
});
afterEach(() => {
  clients.forEach((client) => client.clear());
  clients.length = 0;
  vi.unstubAllGlobals();
  setSession(null);
});

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  clients.push(client);
  render(<QueryClientProvider client={client}><StorageUsage /></QueryClientProvider>);
  return client;
}
const response = (usedBytes: number, quotaBytes = 2 * 1024 ** 3) => new Response(JSON.stringify({
  usedBytes, quotaBytes, availableBytes: Math.max(0, quotaBytes - usedBytes), isOverQuota: usedBytes > quotaBytes,
}), { headers: { "content-type": "application/json" } });

it("shows loading without claiming zero usage", () => {
  vi.stubGlobal("fetch", vi.fn(() => new Promise(() => {})));
  show();
  expect(screen.getByRole("status")).toHaveTextContent("Loading storage");
  expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  expect(screen.queryByText(/available/)).not.toBeInTheDocument();
});

it("uses the server quota and refreshes through the shared drive invalidation", async () => {
  const fetch = vi.fn().mockResolvedValueOnce(response(1024 ** 3, 4 * 1024 ** 3))
    .mockResolvedValueOnce(response(2 * 1024 ** 3, 4 * 1024 ** 3));
  vi.stubGlobal("fetch", fetch);
  const client = show();
  expect(await screen.findByText("1 GiB of 4 GiB used")).toBeInTheDocument();
  expect(screen.getByRole("progressbar")).toHaveAttribute("value", "25");
  expect(screen.getByText("3 GiB available")).toBeInTheDocument();
  await client.invalidateQueries({ queryKey: ["drive"] });
  expect(await screen.findByText("2 GiB available")).toBeInTheDocument();
  expect(client.getQueryData(["drive", "storage", "usage", "storage-user"])).toBeDefined();
});

it("shows a warning near the limit", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response(1.7 * 1024 ** 3)));
  show();
  expect(await screen.findByText("Storage is almost full.")).toBeInTheDocument();
});

it.each([2 * 1024 ** 3, 3 * 1024 ** 3])("clamps full and exceeded storage (%s bytes)", async (used) => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response(used)));
  show();
  const progress = await screen.findByRole("progressbar");
  expect(progress).toHaveAttribute("value", "100");
  expect(screen.getByText("0 B available")).toBeInTheDocument();
  expect(screen.getByText(/Delete files to free up space/)).toBeInTheDocument();
});

it("handles a zero-byte quota without division by zero", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response(0, 0)));
  show();
  expect(await screen.findByRole("progressbar")).toHaveAttribute("value", "100");
  expect(screen.getByText(/Storage is full/)).toBeInTheDocument();
});

it("offers retry on failure and recovers", async () => {
  const user = userEvent.setup();
  vi.stubGlobal("fetch", vi.fn().mockRejectedValueOnce(new TypeError("offline")).mockResolvedValueOnce(response(0)));
  show();
  expect(await screen.findByRole("alert")).toHaveTextContent("Could not load storage usage");
  expect(screen.queryByRole("progressbar")).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Retry" }));
  await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
  expect(screen.getByText("2 GiB available")).toBeInTheDocument();
});
