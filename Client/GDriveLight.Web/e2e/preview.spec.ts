import { expect, test, type Page } from "@playwright/test";
import { mockApi, openActions } from "./fixtures";

const row = (page: Page, name: string) =>
  page
    .getByRole("table")
    .getByRole("row")
    .filter({ has: page.getByText(name, { exact: true }) });
const viewer = (page: Page) => page.getByRole("dialog");
const previewText = "Content loaded from the file preview endpoint.";

async function mockTextPreview(page: Page) {
  await page.route("**/api/files/*/preview", (route) =>
    route.fulfill({ contentType: "text/plain", body: previewText }),
  );
}

test("photo preview fits on load and resize, with optional original size", async ({ page }) => {
  await mockApi(page, true, ["Photo.svg"]);
  await page.route("**/api/files/types", (route) => route.fulfill({
    json: [{ id: 1, name: "Image", extension: ".svg", mimeType: "image/svg+xml", previewKind: "image" }],
  }));
  await page.route("**/api/files/*/preview", (route) => route.fulfill({
    contentType: "image/svg+xml",
    body: '<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="1600"><rect width="2400" height="1600" fill="skyblue"/><circle cx="1200" cy="800" r="600" fill="coral"/></svg>',
  }));
  await page.goto("/");
  await openActions(page, "Photo.svg", "Preview");
  const image = viewer(page).getByRole("img", { name: "Photo.svg" });
  const fit = viewer(page).getByRole("button", { name: "Fit to window" });

  async function expectFit() {
    await expect(fit).toHaveAttribute("aria-pressed", "true");
    await expect.poll(() => image.evaluate((img: HTMLImageElement) => {
      const canvas = img.parentElement!;
      const bounds = img.getBoundingClientRect();
      const frame = canvas.getBoundingClientRect();
      const scale = Math.min(canvas.clientWidth / img.naturalWidth, canvas.clientHeight / img.naturalHeight, 1);
      return img.naturalWidth > 0
        && Math.abs(bounds.width - img.naturalWidth * scale) < 1
        && Math.abs(bounds.height - img.naturalHeight * scale) < 1
        && bounds.left >= frame.left - 1 && bounds.right <= frame.right + 1
        && bounds.top >= frame.top - 1 && bounds.bottom <= frame.bottom + 1;
    })).toBe(true);
  }

  await expectFit();
  await page.setViewportSize({ width: 600, height: 500 });
  await expectFit();
  await viewer(page).getByRole("button", { name: "Original size" }).click();
  await expect(viewer(page).getByText("100%", { exact: true })).toBeVisible();
  await expect.poll(async () => (await image.boundingBox())?.width).toBe(2400);
  await fit.click();
  await expectFit();
  await page.screenshot({ path: test.info().outputPath("photo-fit.png") });
});

test("single click selects; double-click opens a full-screen preview without downloading", async ({
  page,
}) => {
  await mockApi(page);
  await mockTextPreview(page);
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
  await expect(viewer(page).locator("pre")).toHaveText(previewText);
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
    viewer(page).getByRole("button", { name: "Copy to clipboard" }),
  ).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(
    viewer(page).getByRole("button", { name: "Close preview" }),
  ).toBeFocused();
  await page.keyboard.press("Tab");
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
  await mockTextPreview(page);
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
  await expect(viewer(page).locator("pre")).toHaveText(previewText);
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
  await mockTextPreview(page);
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
  try {
    await page.goto("/");
    await row(page, "Notes.txt").dblclick();
    await expect(viewer(page).getByRole("status")).toContainText(
      "Loading preview information",
    );
  } finally {
    release();
  }
  await expect(viewer(page).getByRole("alert")).toContainText(
    "Catalog unavailable",
  );
  await page.screenshot({ path: test.info().outputPath("preview-error.png") });
  fail = false;
  await viewer(page)
    .getByRole("button", { name: "Retry", exact: true })
    .click();
  await expect(viewer(page).locator("pre")).toHaveText(previewText);
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
  const fileArea = page.getByTestId("file-selection-area");
  const scroll = await fileArea.evaluate((element) => element.scrollTop);
  expect(scroll).toBeGreaterThan(0);
  await page.keyboard.press("Enter");
  await expect(viewer(page)).toHaveAccessibleName("Entry 30.txt");
  await page.keyboard.press("ArrowRight");
  await page.keyboard.press("Escape");
  await expect(row(page, "Entry 30.txt")).toBeFocused();
  await expect.poll(() => fileArea.evaluate((element) => element.scrollTop)).toBe(scroll);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await expect(row(page, "Entry 30.txt")).toHaveAttribute(
    "aria-selected",
    "true",
  );
});
