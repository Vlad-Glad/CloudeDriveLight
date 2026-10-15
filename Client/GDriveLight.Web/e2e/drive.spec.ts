import { test, expect } from "@playwright/test";
import { mockApi, openActions, signIn } from "./fixtures";
test("sign in, open profile, log out and protect cloud pages", async ({
  page,
}) => {
  await mockApi(page, false);
  await signIn(page);
  await page.getByRole("button", { name: "User profile" }).click();
  await expect(page.getByText("alex@example.com")).toBeVisible();
  await page.getByRole("menuitem", { name: "Log out" }).click();
  await expect(
    page.getByRole("button", { name: "Sign in", exact: true }),
  ).toBeVisible();
  await page.goto("/folders/folder-1");
  await expect(page).toHaveURL(/login/);
});
test("browse, search and filter without horizontal page overflow", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await page.screenshot({
    path: test.info().outputPath("file-browser.png"),
    fullPage: true,
  });
  await page
    .getByRole("checkbox", { name: "Select Documents", exact: true })
    .check();
  await expect(page).toHaveURL("/");
  await expect(
    page.getByRole("checkbox", { name: "Select Documents", exact: true }),
  ).toBeChecked();
  const folderRow = page
    .getByRole("row")
    .filter({
      has: page.getByRole("link", { name: "Documents", exact: true }),
    });
  await folderRow.locator("td").nth(1).locator("svg").click();
  await expect(
    page.getByRole("heading", { name: "This folder is empty" }),
  ).toBeVisible();
  await page.goBack();
  await expect(
    page.getByRole("table").getByText("Notes.txt", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Search files and folders").fill("Notes");
  await page.getByLabel("Search files and folders").press("Enter");
  await expect(
    page.getByRole("link", { name: "Documents", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("combobox", { name: "File type" }).selectOption("2");
  await expect(
    page.getByRole("heading", { name: "No matching items" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});
test("create, rename, move and confirm deletion", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  await page.getByRole("button", { name: "New", exact: true }).click();
  await page.getByRole("menuitem", { name: "New folder" }).click();
  await page
    .getByRole("textbox", { name: "Name", exact: true })
    .fill("Project");
  await page.getByRole("button", { name: "Create", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "Project", exact: true }),
  ).toBeVisible();
  await openActions(page, "Project", "Rename");
  await page
    .getByRole("textbox", { name: "Name", exact: true })
    .fill("Renamed");
  await page.getByRole("button", { name: "Save", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "Renamed", exact: true }),
  ).toBeVisible();
  await openActions(page, "Renamed", "Move");
  await expect(
    page
      .getByRole("dialog")
      .getByRole("button", { name: "Renamed", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Archive", exact: true })
    .click();
  await page.getByRole("button", { name: "Move here" }).click();
  await expect(
    page.getByRole("link", { name: "Renamed", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Archive", exact: true }).click();
  await openActions(page, "Renamed", "Delete");
  await expect(page.getByRole("dialog")).toContainText(
    "all files and subfolders",
  );
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await expect(
    page.getByRole("link", { name: "Renamed", exact: true }),
  ).toBeVisible();
  await openActions(page, "Renamed", "Delete");
  await page
    .getByRole("button", { name: "Delete permanently", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "This folder is empty" }),
  ).toBeVisible();
});
test("upload files into the current folder and download with authorization", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  const downloadRequests: string[] = [];
  page.on("request", (request) => {
    if (new URL(request.url()).pathname.endsWith("/download"))
      downloadRequests.push(request.url());
  });
  await page.getByRole("table").getByText("Notes.txt", { exact: true }).click();
  await expect(
    page.getByRole("checkbox", { name: "Select Notes.txt" }),
  ).toBeChecked();
  expect(downloadRequests).toHaveLength(0);
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download selected file" }).click();
  expect((await download).suggestedFilename()).toBe("Notes.txt");
  const menuDownload = page.waitForEvent("download");
  await openActions(page, "Notes.txt", "Download");
  expect((await menuDownload).suggestedFilename()).toBe("Notes.txt");
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(
    page
      .getByRole("navigation", { name: "Folder path" })
      .getByRole("link", { name: "Documents", exact: true }),
  ).toBeVisible();
  await page.locator("input[type=file]").setInputFiles({
    name: "Upload.txt",
    mimeType: "text/plain",
    buffer: Buffer.from("hello"),
  });
  await expect(
    page.getByRole("table").getByText("Upload.txt", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("region", { name: "Uploads" })).toContainText(
    "To Documents · Complete",
  );
});
test("shows server errors and restores browsing after retry", async ({
  page,
}) => {
  await mockApi(page);
  let failed = false;
  await page.route("**/api/files", (route) => {
    if (!failed)
      return route.fulfill({
        status: 500,
        json: { error: "Storage unavailable" },
      });
    return route.fallback();
  });
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText("Storage unavailable");
  failed = true;
  await page.getByRole("button", { name: "Try again" }).click();
  await expect(page.getByRole("table")).toBeVisible();
});
test("keyboard can open and close a creation dialog", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  await page.getByRole("button", { name: "New", exact: true }).focus();
  await page.keyboard.press("Enter");
  await page.keyboard.press("Enter");
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "New", exact: true }),
  ).toBeFocused();
});
