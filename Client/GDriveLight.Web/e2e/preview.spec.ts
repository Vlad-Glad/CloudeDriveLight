import { expect, test, type Page } from "@playwright/test";
import { mockApi, openActions } from "./fixtures";

const row = (page: Page, name: string) =>
  page
    .getByRole("table")
    .getByRole("row")
    .filter({ has: page.getByText(name, { exact: true }) });
const viewer = (page: Page) => page.getByRole("dialog");

test("single click selects; double-click opens a full-screen preview without downloading", async ({
  page,
}) => {
  await mockApi(page);
  const downloads: string[] = [];
  page.on("request", (request) => {
    if (request.url().endsWith("/download")) downloads.push(request.url());
  });
  await page.goto("/");
  await row(page, "Notes.txt").click();
  await expect(viewer(page)).toHaveCount(0);
  await expect(row(page, "Notes.txt")).toHaveAttribute("aria-selected", "true");
  await row(page, "Notes.txt").dblclick();
  await expect(viewer(page)).toHaveAccessibleName("Notes.txt");
  await expect(
    viewer(page).getByRole("heading", { name: "No preview available" }),
  ).toBeVisible();
  await expect(
    viewer(page).getByRole("main", { name: "Preview content" }),
  ).toHaveAttribute("data-preview-kind", "text");
  await expect(
    viewer(page).getByRole("button", { name: "Previous file" }),
  ).toBeDisabled();
  await expect(
    viewer(page).getByRole("button", { name: "Next file" }),
  ).toBeDisabled();
  const bounds = (await viewer(page).boundingBox())!;
  expect(bounds.x).toBe(0);
  expect(bounds.y).toBe(0);
  expect(bounds.width).toBe(page.viewportSize()!.width);
  expect(bounds.height).toBe(page.viewportSize()!.height);
  await expect(
    viewer(page).getByRole("button", { name: "Close preview" }),
  ).toBeFocused();
  await page.keyboard.press("Shift+Tab");
  await expect(
    viewer(page).getByRole("button", { name: "Download file" }),
  ).toBeFocused();
  expect(downloads).toEqual([]);
  await page.screenshot({
    path: test.info().outputPath("preview-shell.png"),
    fullPage: false,
  });
  await page.keyboard.press("Escape");
  await expect(viewer(page)).toHaveCount(0);
  await expect(row(page, "Notes.txt")).toBeFocused();
  await expect(row(page, "Notes.txt")).toHaveAttribute("aria-selected", "true");
});

test("Enter opens the selected file, arrows stay within sorted files and Escape preserves group selection", async ({
  page,
}) => {
  await mockApi(page, true, ["Alpha.txt", "Beta.txt", "Gamma.txt"]);
  await page.goto("/");
  await row(page, "Alpha.txt").click();
  await row(page, "Gamma.txt").click({ modifiers: ["Shift"] });
  await page.keyboard.press("Enter");
  await expect(viewer(page)).toHaveAccessibleName("Gamma.txt");
  await page.keyboard.press("ArrowLeft");
  await expect(viewer(page)).toHaveAccessibleName("Beta.txt");
  await viewer(page).getByRole("button", { name: "Previous file" }).click();
  await expect(viewer(page)).toHaveAccessibleName("Alpha.txt");
  await page.keyboard.press("ArrowLeft");
  await expect(viewer(page)).toHaveAccessibleName("Alpha.txt");
  await page.keyboard.press("ArrowRight");
  await page.keyboard.press("ArrowRight");
  await page.keyboard.press("ArrowRight");
  await expect(viewer(page)).toHaveAccessibleName("Notes.txt");
  await expect(viewer(page)).toContainText("4 of 4");
  await expect(
    viewer(page).getByRole("button", { name: "Next file" }),
  ).toBeDisabled();
  await page.keyboard.press("Escape");
  await expect(row(page, "Gamma.txt")).toBeFocused();
  await expect(
    page.getByRole("table").getByRole("row", { selected: true }),
  ).toHaveCount(3);
});

test("action menu opens Preview on touch and keyboard, then restores its trigger", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  const trigger = page.getByRole("button", { name: "Actions for Notes.txt" });
  await trigger.focus();
  await page.keyboard.press("Enter");
  await expect(
    page.getByRole("menuitem", { name: "Preview", exact: true }),
  ).toBeVisible();
  await expect(viewer(page)).toHaveCount(0);
  await page.getByRole("menuitem", { name: "Preview", exact: true }).click();
  await expect(viewer(page)).toHaveAccessibleName("Notes.txt");
  await viewer(page).getByRole("button", { name: "Close preview" }).click();
  await expect(trigger).toBeFocused();
  await openActions(page, "Notes.txt", "Preview");
  await expect(viewer(page)).toBeVisible();
  await page.keyboard.press("Escape");
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(page).toHaveURL(/folders\/folder-1/);
  await expect(viewer(page)).toHaveCount(0);
});

test("preview navigation respects search, filter and descending sort", async ({
  page,
}) => {
  await mockApi(page, true, ["Group A.txt", "Group B.txt", "Outside.txt"]);
  await page.route("**/api/files/search?**", (route) =>
    route.fulfill({
      json: [
        {
          id: "group-a",
          name: "Group A.txt",
          fileTypeId: 1,
          sizeBytes: 10,
          uploadedAtUtc: "2026-10-10T10:00:00Z",
        },
        {
          id: "group-b",
          name: "Group B.txt",
          fileTypeId: 1,
          sizeBytes: 20,
          uploadedAtUtc: "2026-10-10T10:00:00Z",
        },
        {
          id: "group-pdf",
          name: "Group Other.pdf",
          fileTypeId: 2,
          sizeBytes: 30,
          uploadedAtUtc: "2026-10-10T10:00:00Z",
        },
      ],
    }),
  );
  await page.goto("/");
  await page
    .getByRole("textbox", { name: "Search files and folders" })
    .fill("Group");
  await page
    .getByRole("textbox", { name: "Search files and folders" })
    .press("Enter");
  await expect(row(page, "Outside.txt")).toHaveCount(0);
  await expect(row(page, "Group Other.pdf")).toBeVisible();
  await page.getByRole("combobox").selectOption("1");
  await expect(row(page, "Group Other.pdf")).toHaveCount(0);
  await page.getByRole("button", { name: "Name", exact: true }).click();
  await openActions(page, "Group B.txt", "Preview");
  await expect(viewer(page)).toContainText("1 of 2");
  await viewer(page).getByRole("button", { name: "Next file" }).click();
  await expect(viewer(page)).toHaveAccessibleName("Group A.txt");
  await expect(
    viewer(page).getByRole("button", { name: "Next file" }),
  ).toBeDisabled();
});

test("preview capability comes from fileTypeId, with safe fallback for missing catalog entries", async ({
  page,
}) => {
  await mockApi(page, true, ["Renamed.png"]);
  await page.route("**/api/files/types", (route) =>
    route.fulfill({
      json: [
        {
          id: 1,
          name: "Custom source",
          extension: ".new",
          mimeType: "text/x-custom",
          previewKind: "text",
        },
      ],
    }),
  );
  await page.goto("/");
  await openActions(page, "Renamed.png", "Preview");
  await expect(viewer(page).getByRole("main")).toHaveAttribute(
    "data-preview-kind",
    "text",
  );
  await expect(viewer(page)).toContainText("Custom source");
  await page.keyboard.press("Escape");
  await page.route("**/api/files/types", (route) =>
    route.fulfill({ json: [] }),
  );
  await page.reload();
  await openActions(page, "Renamed.png", "Preview");
  await expect(viewer(page).getByRole("main")).toHaveAttribute(
    "data-preview-kind",
    "none",
  );
  await expect(viewer(page)).toContainText("Unknown file type");
  await expect(
    viewer(page).getByRole("button", { name: "Download file" }),
  ).toBeEnabled();
});

test("catalog loading, errors and retry stay inside the viewer", async ({
  page,
}) => {
  await mockApi(page);
  let release = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let fail = true;
  await page.route("**/api/files/types", async (route) => {
    await gate;
    return fail
      ? route.fulfill({ status: 503, json: { error: "Catalog unavailable" } })
      : route.fallback();
  });
  await page.goto("/");
  await row(page, "Notes.txt").dblclick();
  await expect(viewer(page).getByRole("status")).toContainText(
    "Loading preview information",
  );
  release();
  await expect(viewer(page).getByRole("alert")).toContainText(
    "Catalog unavailable",
  );
  await page.screenshot({ path: test.info().outputPath("preview-error.png") });
  fail = false;
  await viewer(page)
    .getByRole("button", { name: "Retry", exact: true })
    .click();
  await expect(
    viewer(page).getByRole("heading", { name: "No preview available" }),
  ).toBeVisible();
});

test("downloads require an explicit viewer action and failures remain visible", async ({
  page,
}) => {
  await mockApi(page);
  let fail = true;
  const calls: string[] = [];
  await page.route("**/api/files/*/download", (route) => {
    calls.push(route.request().headers().authorization);
    return fail
      ? route.fulfill({ status: 403, json: { error: "Access denied" } })
      : route.fallback();
  });
  await page.goto("/");
  await openActions(page, "Notes.txt", "Preview");
  expect(calls).toEqual([]);
  await viewer(page).getByRole("button", { name: "Download file" }).click();
  await expect(viewer(page).getByRole("alert")).toContainText("Access denied");
  fail = false;
  const download = page.waitForEvent("download");
  await viewer(page).getByRole("button", { name: "Download file" }).click();
  expect((await download).suggestedFilename()).toBe("Notes.txt");
  expect(calls).toHaveLength(2);
  expect(calls.every((value) => value.startsWith("Bearer "))).toBe(true);
});

test("closing preview restores the scrolled file list without jumping to the first file", async ({
  page,
}) => {
  await mockApi(
    page,
    true,
    Array.from(
      { length: 40 },
      (_, index) => "Entry " + String(index).padStart(2, "0") + ".txt",
    ),
  );
  await page.goto("/");
  await row(page, "Entry 30.txt").click();
  const scroll = await page.evaluate(() => window.scrollY);
  expect(scroll).toBeGreaterThan(0);
  await page.keyboard.press("Enter");
  await expect(viewer(page)).toHaveAccessibleName("Entry 30.txt");
  await page.keyboard.press("ArrowRight");
  await page.keyboard.press("Escape");
  await expect(row(page, "Entry 30.txt")).toBeFocused();
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(scroll);
  await expect(row(page, "Entry 30.txt")).toHaveAttribute(
    "aria-selected",
    "true",
  );
});
