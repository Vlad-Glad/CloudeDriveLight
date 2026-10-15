import { test, expect } from "@playwright/test";
import { mockApi, openActions } from "./fixtures";

test("storage is visible on all screen sizes and updates after upload and deletion", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  const storage = page.getByRole("region", { name: "Storage", exact: true });
  await expect(storage).toBeVisible();
  await expect(storage).toContainText("1 KiB of 2 GiB used");
  await page.getByRole("button", { name: "New", exact: true }).click();
  const picker = page.waitForEvent("filechooser");
  await page.getByRole("menuitem", { name: "Upload files", exact: true }).click();
  await (await picker).setFiles({ name: "Quota.txt", mimeType: "text/plain", buffer: Buffer.from("quota") });
  await expect(storage).toContainText("2 KiB of 2 GiB used");
  await openActions(page, "Quota.txt", "Delete");
  await page.getByRole("dialog").getByRole("button", { name: "Delete permanently", exact: true }).click();
  await expect(storage).toContainText("1 KiB of 2 GiB used");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("quota rejection refreshes usage and leaves the upload error visible", async ({ page }) => {
  await mockApi(page);
  let full = false;
  await page.route("**/api/storage/usage", (route) => route.fulfill({ json: {
    quotaBytes: 2 * 1024 ** 3, usedBytes: full ? 2 * 1024 ** 3 : 0,
    availableBytes: full ? 0 : 2 * 1024 ** 3, isOverQuota: false,
  } }));
  await page.route("**/api/files", (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    full = true;
    return route.fulfill({ status: 409, json: {
      code: "storage_quota_exceeded", error: "There is not enough storage space.",
    } });
  });
  await page.goto("/");
  await expect(page.getByRole("region", { name: "Storage" })).toContainText("2 GiB available");
  await page.getByRole("button", { name: "New", exact: true }).click();
  const picker = page.waitForEvent("filechooser");
  await page.getByRole("menuitem", { name: "Upload files", exact: true }).click();
  await (await picker).setFiles({ name: "Full.txt", mimeType: "text/plain", buffer: Buffer.from("full") });
  await expect(page.getByText("There is not enough storage space.", { exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Storage" })).toContainText("Storage is full.");
});
