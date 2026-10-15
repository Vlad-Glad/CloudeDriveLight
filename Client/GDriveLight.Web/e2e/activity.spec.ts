import { expect, test, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";

async function moveNotes(page: Page) {
  const rows = page.getByRole("table").getByRole("row");
  const data = await page.evaluateHandle(() => new DataTransfer());
  await rows
    .filter({ hasText: "Notes.txt" })
    .dispatchEvent("dragstart", { dataTransfer: data });
  await rows
    .filter({ hasText: "Documents" })
    .dispatchEvent("drop", { dataTransfer: data });
  await data.dispose();
}

async function expectPopupPlacement(page: Page) {
  const activity = page.getByRole("complementary", { name: "File activity" });
  await expect(activity).toHaveCSS("position", "fixed");
  const bounds = await activity.boundingBox();
  const viewport = page.viewportSize()!;
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.y).toBeGreaterThanOrEqual(0);
  expect(
    Math.abs(viewport.width - bounds!.x - bounds!.width),
  ).toBeLessThanOrEqual(24);
  expect(
    Math.abs(viewport.height - bounds!.y - bounds!.height),
  ).toBeLessThanOrEqual(24);
}

test("move progress and completion appear at the right edge without shifting the file list", async ({
  page,
}) => {
  await mockApi(page);
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/files/file-1/move", async (route) => {
    await gate;
    await route.fallback();
  });
  await page.goto("/");
  const table = page.getByRole("table");
  await expect(table).toBeVisible();
  const originalY = (await table.boundingBox())!.y;
  await moveNotes(page);
  const activity = page.getByRole("complementary", { name: "File activity" });
  await expect(activity.getByRole("status")).toHaveText("Moving item…");
  await expectPopupPlacement(page);
  expect((await table.boundingBox())!.y).toBe(originalY);
  release();
  await expect(activity.getByRole("status")).toContainText(
    "“Notes.txt” moved.",
  );
  expect((await table.boundingBox())!.y).toBe(originalY);
  await page.screenshot({
    path: test.info().outputPath("move-popup.png"),
    fullPage: true,
  });
  await activity.getByRole("button", { name: "Dismiss notification" }).click();
  await expect(activity.getByRole("status")).toHaveCount(0);
});

test("move failures appear in a dismissible popup and preserve the file", async ({
  page,
}) => {
  await mockApi(page);
  await page.route("**/api/files/file-1/move", (route) =>
    route.fulfill({
      status: 409,
      json: { error: "A file with this name already exists." },
    }),
  );
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await moveNotes(page);
  const activity = page.getByRole("complementary", { name: "File activity" });
  await expect(activity.getByRole("alert")).toContainText(
    "A file with this name already exists.",
  );
  await expectPopupPlacement(page);
  await expect(
    page.getByRole("table").getByText("Notes.txt", { exact: true }),
  ).toBeVisible();
  await activity.getByRole("button", { name: "Dismiss error" }).click();
  await expect(activity.getByRole("alert")).toHaveCount(0);
});

test("upload popup minimizes without cancelling, survives navigation and clears completed jobs", async ({
  page,
}) => {
  await mockApi(page);
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let uploadCalls = 0;
  await page.route("**/api/files", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    uploadCalls++;
    await gate;
    await route.fallback();
  });
  await page.goto("/");
  const table = page.getByRole("table");
  await expect(table).toBeVisible();
  const originalY = (await table.boundingBox())!.y;
  await page
    .locator('input[type="file"]')
    .setInputFiles({
      name: "Popup.txt",
      mimeType: "text/plain",
      buffer: Buffer.from("hello"),
    });
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue.getByRole("progressbar")).toBeVisible();
  await expect.poll(() => uploadCalls).toBe(1);
  await expectPopupPlacement(page);
  expect((await table.boundingBox())!.y).toBe(originalY);
  await page.screenshot({
    path: test.info().outputPath("upload-popup.png"),
    fullPage: true,
  });
  await queue.getByRole("button", { name: "Minimize uploads" }).click();
  await expect(
    queue.getByRole("button", { name: "Expand uploads" }),
  ).toHaveAttribute("aria-expanded", "false");
  await expect(queue.getByRole("progressbar")).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  release();
  await expect(queue).toContainText("1/1 complete");
  await queue.getByRole("button", { name: "Expand uploads" }).click();
  await expect(queue).toContainText("To Home · Complete");
  expect(uploadCalls).toBe(1);
  await queue.getByRole("button", { name: "Clear finished" }).click();
  await expect(queue).toHaveCount(0);
});
