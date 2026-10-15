import { test, expect, type Locator, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";

const row = (page: Page, name: string) =>
  page
    .getByRole("table")
    .getByRole("row")
    .filter({ has: page.getByText(name, { exact: true }) });
async function dragCloudItem(page: Page, source: Locator, target: Locator) {
  const data = await page.evaluateHandle(() => new DataTransfer());
  await source.dispatchEvent("dragstart", { dataTransfer: data });
  await target.dispatchEvent("dragenter", { dataTransfer: data });
  await target.dispatchEvent("dragover", { dataTransfer: data });
  await target.dispatchEvent("drop", { dataTransfer: data });
  await data.dispose();
}

test("drag a cloud file into a folder and back to Home without an upload or download", async ({
  page,
}, testInfo) => {
  await mockApi(page);
  const requests: { path: string; method: string; body: unknown }[] = [];
  page.on("request", (request) => {
    const path = new URL(request.url()).pathname;
    if (request.method() === "PUT" && path.endsWith("/move"))
      requests.push({
        path,
        method: request.method(),
        body: request.postDataJSON(),
      });
    if (
      (request.method() === "POST" && path === "/api/files") ||
      path.endsWith("/download")
    )
      throw new Error("Cloud moves must not transfer file content.");
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  if (testInfo.project.name === "desktop")
    await row(page, "Notes.txt").dragTo(row(page, "Documents"));
  else
    await dragCloudItem(page, row(page, "Notes.txt"), row(page, "Documents"));
  await expect(page.getByRole("status")).toContainText("“Notes.txt” moved.");
  expect(requests).toEqual([
    {
      path: "/api/files/file-1/move",
      method: "PUT",
      body: { newFolderId: "folder-1" },
    },
  ]);
  await expect(
    page.getByRole("table").getByText("Notes.txt", { exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(row(page, "Notes.txt")).toBeVisible();
  await dragCloudItem(
    page,
    row(page, "Notes.txt"),
    page
      .getByRole("navigation", { name: "Main navigation" })
      .getByRole("link", { name: "Home" }),
  );
  await expect(
    page.getByRole("heading", { name: "This folder is empty" }),
  ).toBeVisible();
  expect(requests.at(-1)?.body).toEqual({ newFolderId: null });
  await page
    .getByRole("navigation", { name: "Folder path" })
    .getByRole("link", { name: "Home" })
    .click();
  await expect(row(page, "Notes.txt")).toBeVisible();
});

test("drag a folder into another folder and move it back using the path", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  const move = page.waitForRequest(
    (request) =>
      request.method() === "PUT" &&
      request.url().endsWith("/api/folders/folder-2/move"),
  );
  await dragCloudItem(page, row(page, "Archive"), row(page, "Documents"));
  expect((await move).postDataJSON()).toEqual({
    newParentFolderId: "folder-1",
  });
  await expect(
    page.getByRole("link", { name: "Archive", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(row(page, "Archive")).toBeVisible();
  await dragCloudItem(
    page,
    row(page, "Archive"),
    page
      .getByRole("navigation", { name: "Folder path" })
      .getByRole("link", { name: "Home" }),
  );
  await expect(
    page.getByRole("heading", { name: "This folder is empty" }),
  ).toBeVisible();
});

test("drop highlight identifies the destination and leaves when the drag ends", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  const data = await page.evaluateHandle(() => new DataTransfer());
  await row(page, "Notes.txt").dispatchEvent("dragstart", {
    dataTransfer: data,
  });
  await row(page, "Documents").dispatchEvent("dragover", {
    dataTransfer: data,
  });
  await expect(row(page, "Documents")).toHaveAttribute(
    "data-move-active",
    "true",
  );
  await expect(page.getByText(/Move “Notes.txt” into a folder/)).toBeVisible();
  await expect(page.getByText(/Drop files to upload to/)).toHaveCount(0);
  await page.screenshot({
    path: test.info().outputPath("move-cloud-file.png"),
    fullPage: true,
  });
  await row(page, "Notes.txt").dispatchEvent("dragend", { dataTransfer: data });
  await expect(row(page, "Documents")).not.toHaveAttribute(
    "data-move-active",
    "true",
  );
  await expect(page.getByText(/Move “Notes.txt” into a folder/)).toHaveCount(0);
  await data.dispose();
});

test("reject self and same-parent drops without API mutations", async ({
  page,
}) => {
  await mockApi(page);
  let mutations = 0;
  page.on("request", (request) => {
    if (request.method() === "PUT" && request.url().endsWith("/move"))
      mutations++;
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dragCloudItem(page, row(page, "Documents"), row(page, "Documents"));
  await dragCloudItem(
    page,
    row(page, "Notes.txt"),
    page
      .getByRole("navigation", { name: "Folder path" })
      .getByRole("link", { name: "Home" }),
  );
  expect(mutations).toBe(0);
  await expect(page.getByRole("table")).toBeVisible();
});

test("reject moving a folder into a descendant in search results", async ({
  page,
}) => {
  await mockApi(page);
  await page.route("**/api/folders/folder-2/path", (route) =>
    route.fulfill({
      json: [
        {
          id: "folder-1",
          name: "Documents",
          parentFolderId: null,
          ownerId: "user-1",
        },
        {
          id: "folder-2",
          name: "Archive",
          parentFolderId: "folder-1",
          ownerId: "user-1",
        },
      ],
    }),
  );
  let mutations = 0;
  page.on("request", (request) => {
    if (request.method() === "PUT" && request.url().endsWith("/move"))
      mutations++;
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dragCloudItem(page, row(page, "Documents"), row(page, "Archive"));
  await expect(page.getByRole("alert")).toContainText(
    "Cannot move a folder into itself or its descendant",
  );
  expect(mutations).toBe(0);
});

test("a failed move leaves the item in place and displays the server error", async ({
  page,
}) => {
  await mockApi(page);
  await page.route("**/api/files/file-1/move", (route) =>
    route.fulfill({
      status: 400,
      json: {
        error:
          "A file with this name already exists in the destination folder.",
      },
    }),
  );
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dragCloudItem(page, row(page, "Notes.txt"), row(page, "Documents"));
  await expect(page.getByRole("alert")).toContainText("already exists");
  await expect(row(page, "Notes.txt")).toBeVisible();
});

test("the selection toolbar moves a file through the destination dialog", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await page.getByRole("checkbox", { name: "Select Notes.txt" }).check();
  await page
    .getByRole("button", { name: "Move selected item", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Documents", exact: true })
    .click();
  await page.getByRole("button", { name: "Move here" }).click();
  await expect(page.getByRole("status")).toContainText("Item moved.");
  await expect(
    page.getByRole("table").getByText("Notes.txt", { exact: true }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(row(page, "Notes.txt")).toBeVisible();
});
