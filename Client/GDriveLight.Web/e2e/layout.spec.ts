import { test, expect, type Locator } from "@playwright/test";
import { mockApi } from "./fixtures";

const position = (element: Locator) => element.evaluate((node) => {
  const bounds = node.getBoundingClientRect();
  return { x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height };
});

test("only files scroll while navigation, search, controls and table headings stay in place", async ({ page }) => {
  await mockApi(page, true, Array.from({ length: 80 }, (_, i) => `Entry ${String(i).padStart(2, "0")}.txt`));
  await page.goto("/");
  const files = page.getByTestId("file-selection-area");
  await expect(files).toBeVisible();
  await expect(page.getByRole("progressbar", { name: "Storage used" })).toBeVisible();
  const sidebar = page.getByRole("complementary", { name: "Drive navigation" });
  const search = page.getByRole("textbox", { name: "Search files and folders" });
  const columns = page.getByRole("button", { name: "Show or hide columns" });
  const before = await Promise.all([position(sidebar), position(search), position(columns)]);
  const header = page.getByRole("columnheader", { name: "Name", exact: true });
  const headingTop = (await position(header)).y;
  const area = await files.boundingBox();
  expect(area!.height).toBeGreaterThan(100);
  await page.mouse.move(area!.x + 80, area!.y + area!.height / 2);
  await page.mouse.wheel(0, 700);
  await expect.poll(() => files.evaluate((node) => node.scrollTop)).toBeGreaterThan(100);
  expect(await Promise.all([position(sidebar), position(search), position(columns)])).toEqual(before);
  expect((await position(header)).y).toBeCloseTo(headingTop, 0);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);

  // At the end of the list, wheel input must not start scrolling the page.
  await files.evaluate((node) => { node.scrollTop = node.scrollHeight; });
  await page.mouse.wheel(0, 700);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  expect(await position(search)).toEqual(before[1]);
  expect(await page.evaluate(() => document.documentElement.scrollHeight <= innerHeight)).toBe(true);
});

test("keyboard navigation scrolls the list and short windows retain a usable file area", async ({ page }) => {
  await mockApi(page, true, Array.from({ length: 60 }, (_, i) => `Entry ${String(i).padStart(2, "0")}.txt`));
  await page.goto("/");
  const files = page.getByTestId("file-selection-area");
  await files.focus();
  await page.keyboard.press("End");
  const last = page.getByRole("row").filter({ hasText: "Notes.txt" });
  await expect(last).toBeFocused();
  await expect.poll(() => files.evaluate((node) => node.scrollTop)).toBeGreaterThan(0);
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
  await page.setViewportSize({ width: page.viewportSize()!.width, height: 650 });
  await expect(files).toBeVisible();
  const area = await files.boundingBox();
  expect(area!.height).toBeGreaterThan(80);
  expect(area!.y + area!.height).toBeLessThanOrEqual(650);
  expect(await page.evaluate(() => document.documentElement.scrollHeight <= innerHeight)).toBe(true);
});
