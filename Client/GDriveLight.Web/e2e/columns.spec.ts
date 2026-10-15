import { test, expect, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";

const table = (page: Page) => page.getByRole("table", { name: "Files and folders" });
const openColumns = (page: Page) => page.getByRole("button", { name: "Show or hide columns" }).click();

test("hide metadata together or individually while retaining names, actions and selection", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  const fileRow = table(page).getByRole("row").filter({ hasText: "Notes.txt" });
  await fileRow.click();
  await openColumns(page);
  await expect(page.getByRole("menuitemcheckbox")).toHaveCount(3);
  await expect(page.getByRole("menuitemcheckbox", { name: "Name", exact: true })).toHaveCount(0);
  await page.getByRole("menuitem", { name: "Hide all columns", exact: true }).click();
  await expect(table(page).getByRole("columnheader")).toHaveText(["Name", "Actions"]);
  await expect(fileRow.getByRole("cell")).toHaveCount(2);
  await expect(fileRow).toHaveAttribute("aria-selected", "true");
  await expect(fileRow.getByRole("button", { name: "Actions for Notes.txt" })).toBeVisible();

  await openColumns(page);
  const sizeOption = page.getByRole("menuitemcheckbox", { name: "File size", exact: true });
  await expect(sizeOption).not.toBeChecked();
  await sizeOption.click();
  await expect(sizeOption).toBeChecked();
  await page.keyboard.press("Escape");
  await expect(table(page).getByRole("columnheader")).toHaveText(["Name", "File size", "Actions"]);
  await expect(fileRow.getByRole("cell")).toHaveCount(3);
  await expect(fileRow).toContainText("1 KB");
  await expect(page.getByRole("button", { name: "Show or hide columns" })).toBeFocused();

  await openColumns(page);
  await page.getByRole("menuitem", { name: "Show all columns", exact: true }).click();
  await expect(table(page).getByRole("columnheader")).toHaveText(["Name", "Date modified", "Date created", "File size", "Actions"]);
  await expect(fileRow.getByRole("cell")).toHaveCount(5);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test("hiding the sorted column returns to name sorting and choices survive navigation and reload", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  await table(page).getByRole("button", { name: "File size", exact: true }).click();
  await expect(table(page).getByRole("columnheader", { name: "File size" })).toHaveAttribute("aria-sort", "ascending");
  await openColumns(page);
  await page.getByRole("menuitemcheckbox", { name: "File size", exact: true }).click();
  await page.keyboard.press("Escape");
  await expect(table(page).getByRole("columnheader", { name: "Name", exact: true })).toHaveAttribute("aria-sort", "ascending");
  await expect(table(page).getByRole("columnheader", { name: "File size" })).toHaveCount(0);
  await table(page).getByRole("link", { name: "Documents", exact: true }).click();
  await expect(page.getByRole("heading", { name: "This folder is empty" })).toBeVisible();
  await page.getByRole("link", { name: "Home", exact: true }).last().click();
  await page.reload();
  await expect(table(page).getByRole("columnheader")).toHaveText(["Name", "Date modified", "Date created", "Actions"]);
});

for (const storage of ["malformed", "blocked"] as const) {
  test(`column controls work with ${storage} browser storage`, async ({ page }) => {
    await mockApi(page);
    await page.addInitScript((mode) => {
      if (mode === "malformed") localStorage.setItem("gdrivelight.columns.v2", "invalid json");
      else Object.defineProperty(window, "localStorage", { get() { throw new Error("Storage blocked"); } });
    }, storage);
    await page.goto("/");
    await expect(table(page).getByRole("columnheader")).toHaveCount(5);
    await openColumns(page);
    await page.getByRole("menuitem", { name: "Hide all columns", exact: true }).click();
    await expect(table(page).getByRole("columnheader")).toHaveText(["Name", "Actions"]);
  });
}
