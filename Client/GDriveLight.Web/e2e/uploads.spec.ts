import { test, expect, type Page } from "@playwright/test";
import { mockApi } from "./fixtures";
async function dropFiles(page: Page, names: string[]) {
  const data = await page.evaluateHandle((names) => {
    const data = new DataTransfer();
    names.forEach((name) =>
      data.items.add(new File(["hello cloud"], name, { type: "text/plain" })),
    );
    return data;
  }, names);
  const surface = page.getByTestId("upload-surface");
  await surface.dispatchEvent("dragenter", { dataTransfer: data });
  await expect(page.getByText(/Drop files to upload to/)).toBeVisible();
  await surface.dispatchEvent("drop", { dataTransfer: data });
  await data.dispose();
}
test("drop multiple files, keep the queue destination while browsing and recover a failed upload", async ({
  page,
}) => {
  await mockApi(page);
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  let calls = 0;
  await page.route("**/api/files", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    calls++;
    if (calls === 1) {
      await gate;
      return route.fulfill({
        status: 503,
        json: { error: "Temporary upload failure" },
      });
    }
    return route.fallback();
  });
  try {
    await page.goto("/");
    await page.getByRole("link", { name: "Documents", exact: true }).click();
    await dropFiles(page, ["First.txt", "Second.txt"]);
    await expect(
      page.getByRole("progressbar", { name: "Upload progress for First.txt" }),
    ).toBeVisible();
    await page
      .getByRole("navigation", { name: "Folder path" })
      .getByRole("link", { name: "Home", exact: true })
      .click();
  } finally {
    release();
  }
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue).toContainText("Temporary upload failure");
  await expect(queue).toContainText("To Documents · Complete");
  await queue.getByRole("button", { name: "Retry First.txt" }).click();
  await expect(queue).toContainText("2/2 complete");
  await expect(
    page.getByRole("table").getByText("First.txt", { exact: true }),
  ).toHaveCount(0);
  await queue.getByRole("button", { name: "Clear finished" }).click();
  await expect(queue).toHaveCount(0);
  await page.getByRole("link", { name: "Documents", exact: true }).click();
  await expect(
    page.getByRole("table").getByText("First.txt", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("table").getByText("Second.txt", { exact: true }),
  ).toBeVisible();
});
test("cancel an active and a queued upload without sending the queued file", async ({
  page,
}) => {
  await mockApi(page);
  let calls = 0;
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/files", async (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    calls++;
    await gate;
    await route
      .fulfill({ status: 201, json: { id: "cancelled" } })
      .catch(() => {});
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dropFiles(page, ["Active.txt", "Queued.txt"]);
  await expect.poll(() => calls).toBe(1);
  await page.getByRole("button", { name: "Cancel upload Queued.txt" }).click();
  await page.getByRole("button", { name: "Cancel upload Active.txt" }).click();
  await expect(
    page.getByRole("button", { name: "Retry Active.txt" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Retry Queued.txt" }),
  ).toBeVisible();
  expect(calls).toBe(1);
  release();
});
test("unsupported file errors do not stop the next dropped file", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dropFiles(page, ["Unsupported.exe", "Accepted.txt"]);
  await expect(page.getByRole("region", { name: "Uploads" })).toContainText(
    "File type '.exe' is not supported.",
  );
  await expect(
    page.getByRole("table").getByText("Accepted.txt", { exact: true }),
  ).toBeVisible();
});
test("ignore text drags and reset the overlay when files leave the surface", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  const text = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.setData("text/plain", "text");
    return data;
  });
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragenter", { dataTransfer: text });
  await expect(page.getByText(/Drop files to upload to/)).toHaveCount(0);
  await text.dispose();
  const files = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.items.add(new File(["a"], "a.txt"));
    return data;
  });
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragenter", { dataTransfer: files });
  await expect(page.getByText(/Drop files to upload to/)).toBeVisible();
  await page
    .getByTestId("upload-surface")
    .dispatchEvent("dragleave", { dataTransfer: files });
  await expect(page.getByText(/Drop files to upload to/)).toHaveCount(0);
  await files.dispose();
});
for (const source of ["picker", "drop"] as const) {
  test(`reject an unsupported PDF before sending from ${source}`, async ({
    page,
  }) => {
    await mockApi(page);
    await page.route("**/api/files/types", (route) =>
      route.fulfill({ json: [{ id: 1, name: "Text", extension: ".txt" }] }),
    );
    const uploadedNames: string[] = [];
    page.on("request", (request) => {
      if (
        request.method() === "POST" &&
        new URL(request.url()).pathname === "/api/files"
      ) {
        uploadedNames.push(
          /filename="([^"]+)"/.exec(
            request.postDataBuffer()?.toString() || "",
          )?.[1] || "",
        );
      }
    });
    await page.goto("/");
    await expect(
      page.getByRole("combobox", { name: "File type" }).locator("option"),
    ).toHaveCount(2);
    if (source === "drop")
      await dropFiles(page, ["Report.PDF", "Accepted.TXT"]);
    else
      await page.locator("input[type=file]").setInputFiles([
        {
          name: "Report.PDF",
          mimeType: "application/pdf",
          buffer: Buffer.from("%PDF-1.7"),
        },
        {
          name: "Accepted.TXT",
          mimeType: "text/plain",
          buffer: Buffer.from("hello"),
        },
      ]);
    const queue = page.getByRole("region", { name: "Uploads" });
    const rejected = queue
      .getByRole("listitem")
      .filter({ hasText: "Report.PDF" });
    await expect(rejected).toContainText("Rejected");
    await expect(rejected).toContainText("File type '.pdf' is not supported.");
    await expect(rejected.getByRole("progressbar")).toHaveCount(0);
    await expect(rejected.getByRole("button", { name: /Retry/ })).toHaveCount(
      0,
    );
    await expect(queue).toContainText("1/2 complete");
    expect(uploadedNames).toEqual(["Accepted.TXT"]);
    if (source === "picker")
      await page.screenshot({
        path: test.info().outputPath("rejected-pdf.png"),
        fullPage: true,
      });
  });
}

test("wait for the type catalog before sending a dropped file", async ({
  page,
}) => {
  await mockApi(page);
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/api/files/types", async (route) => {
    await gate;
    return route.fulfill({
      json: [{ id: 1, name: "Text", extension: ".txt" }],
    });
  });
  let uploads = 0;
  page.on("request", (request) => {
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/api/files"
    )
      uploads++;
  });
  await page.goto("/");
  await expect(page.getByRole("table")).toBeVisible();
  await dropFiles(page, ["Report.pdf", "Accepted.txt"]);
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue).toContainText("Waiting");
  await expect(queue.getByRole("progressbar")).toHaveCount(0);
  expect(uploads).toBe(0);
  release();
  await expect(queue).toContainText("File type '.pdf' is not supported.");
  await expect(queue).toContainText("1/2 complete");
  expect(uploads).toBe(1);
});

test("do not upload when the catalog fails and allow retry after recovery", async ({
  page,
}) => {
  await mockApi(page);
  let available = false;
  await page.route("**/api/files/types", (route) =>
    available
      ? route.fallback()
      : route.fulfill({ status: 503, json: { error: "Catalog unavailable" } }),
  );
  let uploads = 0;
  page.on("request", (request) => {
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/api/files"
    )
      uploads++;
  });
  await page.goto("/");
  await expect(page.getByRole("alert")).toContainText(
    "File type filter unavailable",
  );
  await dropFiles(page, ["Accepted.txt"]);
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue).toContainText("Could not verify supported file types");
  expect(uploads).toBe(0);
  available = true;
  await queue.getByRole("button", { name: "Retry Accepted.txt" }).click();
  await expect(queue).toContainText("1/1 complete");
  expect(uploads).toBe(1);
});

test("allow PDF when present in the catalog and still surface server upload errors", async ({
  page,
}) => {
  await mockApi(page);
  let uploads = 0;
  await page.route("**/api/files", (route) => {
    if (route.request().method() !== "POST") return route.fallback();
    uploads++;
    return route.fulfill({
      status: 400,
      json: { error: "Upload permission denied" },
    });
  });
  await page.goto("/");
  await expect(
    page.getByRole("combobox", { name: "File type" }).locator("option"),
  ).toHaveCount(3);
  await dropFiles(page, ["Report.pdf"]);
  const queue = page.getByRole("region", { name: "Uploads" });
  await expect(queue).toContainText("Failed");
  await expect(queue).toContainText("Upload permission denied");
  await expect(
    queue.getByRole("button", { name: "Retry Report.pdf" }),
  ).toBeVisible();
  expect(uploads).toBe(1);
});
