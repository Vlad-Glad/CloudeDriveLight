import { test, expect, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";
async function dropFiles(page: Page, names: string[]) {
  const data = await page.evaluateHandle((names) => {
    const data = new DataTransfer();
    names.forEach((name) =>
      data.items.add(new File(["hello cloud"], name, { type: "text/plain" })),
    );
    return data;
  }, names);
  const surface = page.getByTestId("upload-surface");
  await surface.dispatchEvent("dragenter", { dataTransfer: data });
  await expect(page.getByText(/Drop files to upload to/)).toBeVisible();
  await surface.dispatchEvent("drop", { dataTransfer: data });
  await data.dispose();
}
test("drop multiple files, keep the queue destination while browsing and recover a failed upload", async ({
  page,
}) => {
  await mockApi(page);
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let calls = 0;
  await page.route("**/api/files", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    calls++;
    if (calls === 1) {
      await gate;
      return route.fulfill({
        status: 503,
        json: { error: "Temporary upload failure" },
      });
    }
    return route.fallback();
  });
  await page.goto("/");
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await dropFiles(page, ["First.txt", "Second.txt"]);
  await expect(page.getByRole("progressbar")).toBeVisible();
  await page
    .getByRole("navigation", { name: "Folder path" })
    .getByRole("link", { name: "Home", exact: true })
    .click();
  release();
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue).toContainText("Temporary upload failure");
  await expect(queue).toContainText("To Documents · Complete");
  await queue.getByRole("button", { name: "Retry First.txt" }).click();
  await expect(queue).toContainText("2/2 complete");
  await expect(
    page.getByRole("button", { name: "First.txt", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "First.txt", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Second.txt", exact: true }),
  ).toBeVisible();
});
test("cancel an active and a queued upload without sending the queued file", async ({
  page,
}) => {
  await mockApi(page);
  let calls = 0;
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/files", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    calls++;
    await gate;
    await route
      .fulfill({ status: 201, json: { id: "cancelled" } })
      .catch(() => {});
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dropFiles(page, ["Active.txt", "Queued.txt"]);
  await expect.poll(() => calls).toBe(1);
  await page.getByRole("button", { name: "Cancel upload Queued.txt" }).click();
  await page.getByRole("button", { name: "Cancel upload Active.txt" }).click();
  await expect(
    page.getByRole("button", { name: "Retry Active.txt" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Retry Queued.txt" }),
  ).toBeVisible();
  expect(calls).toBe(1);
  release();
});
test("unsupported file errors do not stop the next dropped file", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dropFiles(page, ["Unsupported.exe", "Accepted.txt"]);
  await expect(page.getByRole("region", { name: "Uploads" })).toContainText(
    "File type '.exe' is not supported.",
  );
  await expect(
    page.getByRole("button", { name: "Accepted.txt", exact: true }),
  ).toBeVisible();
});
test("ignore text drags and reset the overlay when files leave the surface", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  const text = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.setData("text/plain", "text");
    return data;
  });
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragenter", { dataTransfer: text });
  await expect(page.getByText(/Drop files to upload to/)).toHaveCount(0);
  await text.dispose();
  const files = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.items.add(new File(["a"], "a.txt"));
    return data;
  });
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragenter", { dataTransfer: files });
  await expect(page.getByText(/Drop files to upload to/)).toBeVisible();
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragleave", { dataTransfer: files });
  await expect(page.getByText(/Drop files to upload to/)).toHaveCount(0);
  await files.dispose();
});
