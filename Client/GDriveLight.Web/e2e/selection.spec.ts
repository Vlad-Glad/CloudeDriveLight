import { test, expect, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";
const row = (page: Page, name: string) =>
  page
    .getByRole("table")
    .getByRole("row")
    .filter({ has: page.getByText(name, { exact: true }) });
const selected = (page: Page) =>
  page.getByRole("table").getByRole("row", { selected: true });
async function prepare(page: Page) {
  await mockApi(page, true, ["Alpha.txt", "Beta.txt", "Gamma.txt"]);
  await page.goto("/");
  await expect(row(page, "Gamma.txt")).toBeVisible();
}

test("select ranges and individual files without checkboxes, then clear with a blank click", async ({
  page,
}) => {
  await prepare(page);
  await expect(page.getByRole("checkbox")).toHaveCount(0);
  await row(page, "Alpha.txt").click();
  await row(page, "Gamma.txt").click({ modifiers: ["Shift"] });
  await expect(selected(page)).toHaveCount(3);
  await row(page, "Notes.txt").click({ modifiers: ["Control"] });
  await expect(selected(page)).toHaveCount(4);
  await row(page, "Beta.txt").click({ modifiers: ["Control"] });
  await expect(row(page, "Beta.txt")).toHaveAttribute("aria-selected", "false");
  await expect(selected(page)).toHaveCount(3);
  await page.getByTestId("file-selection-area").click({
    position: {
      x: 8,
      y:
        (await page.getByTestId("file-selection-area").boundingBox())!.height -
        8,
    },
  });
  await expect(selected(page)).toHaveCount(0);
});

test("Shift-click selects folders without opening them, while an ordinary click opens a folder", async ({
  page,
}) => {
  await prepare(page);
  await row(page, "Alpha.txt").click();
  await page
    .getByRole("link", { name: "Archive", exact: true })
    .click({ modifiers: ["Shift"] });
  await expect(page).toHaveURL("/");
  await expect(selected(page)).toHaveCount(3);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(page).toHaveURL(/folders\/folder-1/);
});

test("keyboard selection and right-click keep actions scoped to the highlighted group", async ({
  page,
}) => {
  await prepare(page);
  await row(page, "Alpha.txt").click();
  await page.keyboard.press("Shift+ArrowDown");
  await expect(selected(page)).toHaveCount(2);
  await row(page, "Beta.txt").click({ button: "right" });
  await expect(selected(page)).toHaveCount(2);
  await expect(
    page.getByRole("menuitem", { name: "Rename", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("menuitem", { name: "Delete", exact: true }).click();
  await expect(page.getByRole("dialog")).toContainText("2 selected items");
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await row(page, "Alpha.txt").focus();
  await page.keyboard.press("Control+a");
  await expect(selected(page)).toHaveCount(6);
  await page.keyboard.press("Escape");
  await expect(selected(page)).toHaveCount(0);
});

test("dragging a selected row moves the complete selection", async ({
  page,
}, info) => {
  await prepare(page);
  await row(page, "Alpha.txt").click();
  await row(page, "Beta.txt").click({ modifiers: ["Shift"] });
  if (info.project.name === "desktop")
    await row(page, "Alpha.txt").dragTo(row(page, "Documents"));
  else {
    const data = await page.evaluateHandle(() => new DataTransfer());
    await row(page, "Alpha.txt").dispatchEvent("dragstart", {
      dataTransfer: data,
    });
    await row(page, "Documents").dispatchEvent("dragover", {
      dataTransfer: data,
    });
    await row(page, "Documents").dispatchEvent("drop", { dataTransfer: data });
    await data.dispose();
  }
  await expect(page.getByRole("status")).toContainText("2 items moved.");
  await expect(page.getByText(/Move .* into a folder/)).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(row(page, "Alpha.txt")).toBeVisible();
  await expect(row(page, "Beta.txt")).toBeVisible();
});

test("a partial group move retries only the failed file", async ({ page }) => {
  await prepare(page);
  let fail = true;
  const calls: string[] = [];
  page.on("request", (request) => {
    if (request.method() === "PUT" && request.url().endsWith("/move"))
      calls.push(new URL(request.url()).pathname);
  });
  await page.route("**/api/files/extra-1/move", (route) =>
    fail
      ? route.fulfill({ status: 400, json: { error: "Destination conflict" } })
      : route.fallback(),
  );
  await row(page, "Alpha.txt").click();
  await row(page, "Beta.txt").click({ modifiers: ["Shift"] });
  await page
    .getByRole("button", { name: "Move selected items", exact: true })
    .click();
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Documents", exact: true })
    .click();
  await page.getByRole("button", { name: "Move here" }).click();
  await expect(page.getByRole("dialog").getByRole("alert")).toContainText(
    "1 completed; 1 failed",
  );
  fail = false;
  await page.getByRole("button", { name: "Move here" }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  expect(calls.filter((path) => path.includes("extra-0"))).toHaveLength(1);
  expect(calls.filter((path) => path.includes("extra-1"))).toHaveLength(2);
});

test("delete the selected files only after confirming the group action", async ({
  page,
}) => {
  await prepare(page);
  await row(page, "Alpha.txt").click();
  await row(page, "Beta.txt").click({ modifiers: ["Shift"] });
  await page
    .getByRole("button", { name: "Delete selected items", exact: true })
    .click();
  await expect(page.getByRole("dialog")).toContainText("2 selected items");
  await page
    .getByRole("button", { name: "Delete permanently", exact: true })
    .click();
  await expect(row(page, "Alpha.txt")).toHaveCount(0);
  await expect(row(page, "Beta.txt")).toHaveCount(0);
  await expect(row(page, "Gamma.txt")).toBeVisible();
});

test("download a highlighted group only through its explicit download action", async ({
  page,
}) => {
  await prepare(page);
  const names: string[] = [];
  page.on("download", (download) => names.push(download.suggestedFilename()));
  await row(page, "Alpha.txt").click();
  await row(page, "Beta.txt").click({ modifiers: ["Shift"] });
  expect(names).toHaveLength(0);
  await page
    .getByRole("button", { name: "Download selected files", exact: true })
    .click();
  await expect.poll(() => names.length).toBe(2);
  expect(names).toEqual(["Alpha.txt", "Beta.txt"]);
});

test("draw a mouse selection rectangle from blank space", async ({ page }) => {
  await prepare(page);
  await page.getByTestId("file-selection-area").scrollIntoViewIfNeeded();
  const area = await page.getByTestId("file-selection-area").boundingBox();
  const gamma = await row(page, "Gamma.txt").boundingBox();
  await page.mouse.move(area!.x + 8, area!.y + area!.height - 8);
  await page.mouse.down();
  await page.mouse.move(area!.x + area!.width - 12, gamma!.y + 8, { steps: 8 });
  await expect(selected(page)).toHaveCount(2);
  await page.screenshot({
    path: test.info().outputPath("windows-selection.png"),
    fullPage: true,
  });
  await page.mouse.up();
  await expect(row(page, "Gamma.txt")).toHaveAttribute("aria-selected", "true");
  await expect(row(page, "Notes.txt")).toHaveAttribute("aria-selected", "true");
});
