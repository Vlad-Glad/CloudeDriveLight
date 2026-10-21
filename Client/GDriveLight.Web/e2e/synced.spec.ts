import { test, expect, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";

async function mockSyncedFolders(page: Page) {
  await mockApi(page);
  const folders = [
    { id: "sync-root", name: "Laptop", parentFolderId: null, ownerId: "user-1" },
    { id: "sync-child", name: "Projects", parentFolderId: "sync-root", ownerId: "user-1" },
    { id: "sync-other", name: "Work PC", parentFolderId: null, ownerId: "user-1" },
  ];
  const file = (id: string, name: string, folderId: string) => ({
    id, name, folderId, ownerId: "user-1", fileTypeId: 1, sizeBytes: 12,
    uploadedAtUtc: "2026-10-10T10:00:00Z", editedAtUtc: null, contentHash: "hash", fileUrl: "",
  });
  const files = [
    file("sync-file", "Synced notes.txt", "sync-root"),
    file("sync-nested", "Nested notes.txt", "sync-child"),
    file("sync-private", "Other device notes.txt", "sync-other"),
  ];
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === "/api/devices") return route.fulfill({ json: [
      { id: "laptop", name: "Laptop", syncRootFolderId: "sync-root", localPath: "C:\\Users\\Alex\\Sync" },
      { id: "work", name: "Work PC", syncRootFolderId: "sync-other", localPath: "D:\\Work" },
      { id: "unconfigured", name: "No sync configured", syncRootFolderId: null, localPath: null },
    ] });
    if (url.pathname.endsWith("/path") && url.pathname.includes("/sync-")) {
      const chain = [];
      let current = folders.find((folder) => folder.id === url.pathname.split("/")[3]);
      while (current) {
        chain.unshift(current);
        current = folders.find((folder) => folder.id === current?.parentFolderId);
      }
      return route.fulfill({ json: chain });
    }
    if (url.pathname === "/api/folders" && url.searchParams.get("parentFolderId")?.startsWith("sync-"))
      return route.fulfill({ json: folders.filter((folder) => folder.parentFolderId === url.searchParams.get("parentFolderId")) });
    if (url.pathname === "/api/files" && url.searchParams.get("folderId")?.startsWith("sync-"))
      return route.fulfill({ json: files.filter((file) => file.folderId === url.searchParams.get("folderId")) });
    if (url.pathname === "/api/files/sync-file/preview")
      return route.fulfill({ contentType: "text/plain", body: "Content synced from Laptop" });
    return route.fallback();
  });
  return { files, file };
}

test("browse device folders, nested content, preview, refresh and return to Home", async ({ page }) => {
  const { files, file } = await mockSyncedFolders(page);
  await page.goto("/");
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("link", { name: "Synced folders" }).click();
  await expect(page.getByRole("heading", { name: "Synced folders", exact: true })).toBeVisible();
  await expect(page.getByText("C:\\Users\\Alex\\Sync", { exact: true })).toBeVisible();
  await expect(page.getByRole("list", { name: "Synced folders" }).getByRole("link")).toHaveCount(2);
  await expect(page.getByText("No sync configured")).toHaveCount(0);
  await page.getByRole("link", { name: /Laptop/ }).click();
  await expect(page).toHaveURL(/\/synced\/folders\/sync-root$/);
  await expect(page.getByRole("table")).toContainText("Synced notes.txt");
  await expect(page.getByRole("table")).not.toContainText("Other device notes.txt");
  await page.getByRole("table").getByText("Synced notes.txt", { exact: true }).dblclick();
  await expect(page.getByRole("dialog")).toContainText("Content synced from Laptop");
  await page.keyboard.press("Escape");
  await page.getByRole("link", { name: "Projects", exact: true }).click();
  await expect(page).toHaveURL(/\/synced\/folders\/sync-child$/);
  await expect(page.getByRole("table")).toContainText("Nested notes.txt");
  await page.reload();
  await expect(page.getByRole("table")).toContainText("Nested notes.txt");
  await page.getByRole("navigation", { name: "Folder path" }).getByRole("link", { name: "Laptop" }).click();
  files.push(file("new-sync-file", "Freshly synced.txt", "sync-root"));
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByRole("table")).toContainText("Freshly synced.txt");
  await page.getByRole("navigation", { name: "Folder path" }).getByRole("link", { name: "Synced folders" }).click();
  await expect(page.getByRole("list", { name: "Synced folders" })).toBeVisible();
  await page.getByRole("navigation", { name: "Main navigation" }).getByRole("link", { name: "Home", exact: true }).click();
  await expect(page.getByRole("table")).toContainText("Notes.txt");
  await expect(page.getByRole("table")).not.toContainText("Synced notes.txt");
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("synced search includes descendants and excludes Home and other devices", async ({ page }) => {
  await mockSyncedFolders(page);
  await page.goto("/synced/folders/sync-root");
  await page.getByLabel("Search files and folders").fill("notes");
  await page.getByLabel("Search files and folders").press("Enter");
  const table = page.getByRole("table");
  await expect(table).toContainText("Synced notes.txt");
  await expect(table).toContainText("Nested notes.txt");
  await expect(table).not.toContainText("Other device notes.txt");
  await expect(table.getByText("Notes.txt", { exact: true })).toHaveCount(0);
  await page.getByLabel("File type", { exact: true }).selectOption("2");
  await expect(page.getByRole("heading", { name: "No matching items" })).toBeVisible();
  await page.getByLabel("File type", { exact: true }).selectOption("");
  await expect(table).toContainText("Nested notes.txt");
  await page.getByRole("button", { name: "Clear search" }).click();
  await expect(table).toContainText("Projects");
  await expect(table).not.toContainText("Nested notes.txt");
});

test("synced folders explains empty state and recovers from API errors", async ({ page }) => {
  await mockApi(page);
  let fail = true;
  await page.route("**/api/devices", (route) => route.fulfill(fail
    ? { status: 500, json: { error: "Devices unavailable" } }
    : { json: [] }));
  await page.goto("/synced");
  await expect(page.getByRole("alert")).toContainText("Devices unavailable");
  fail = false;
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page.getByRole("heading", { name: "No synced folders yet" })).toBeVisible();
  await expect(page.getByText("Choose a sync folder in the Desktop app", { exact: false })).toBeVisible();
});

test("synced routes require sign in", async ({ page }) => {
  await mockApi(page, false);
  for (const url of ["/synced", "/synced/folders/sync-root"]) {
    await page.goto(url);
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  }
});
