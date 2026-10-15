import { type Page, expect } from "@playwright/test";
export async function mockApi(
  page: Page,
  signedIn = true,
  extraFiles: string[] = [],
) {
  const token = `header.${Buffer.from(JSON.stringify({ sub: "user-1", firstName: "Alex", email: "alex@example.com", exp: Date.now() / 1000 + 3600 })).toString("base64url")}.signature`;
  if (signedIn)
    await page.addInitScript(
      (value) => sessionStorage.setItem("gdrivelight.session", value),
      token,
    );
  let folders = [
    {
      id: "folder-1",
      name: "Documents",
      parentFolderId: null as string | null,
      ownerId: "user-1",
    },
    {
      id: "folder-2",
      name: "Archive",
      parentFolderId: null as string | null,
      ownerId: "user-1",
    },
  ];
  let files = [
    {
      id: "file-1",
      name: "Notes.txt",
      folderId: null as string | null,
      ownerId: "user-1",
      fileTypeId: 1,
      sizeBytes: 1024,
      uploadedAtUtc: "2026-10-10T10:00:00Z",
      editedAtUtc: null,
      contentHash: "hash",
      fileUrl: "",
    },
  ];
  files.push(
    ...extraFiles.map((name, index) => ({
      ...files[0],
      id: `extra-${index}`,
      name,
    })),
  );
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();
    const json = (body: unknown, status = 200) =>
      route.fulfill({ status, json: body });
    if (path.startsWith("/api/auth/")) return json({ token });
    if (!request.headers().authorization?.startsWith("Bearer "))
      return json({ error: "Unauthorized" }, 401);
    if (path === "/api/files/types")
      return json([
        {
          id: 1,
          name: "Text",
          extension: ".txt",
          mimeType: "text/plain",
          previewKind: "text",
        },
        {
          id: 2,
          name: "PDF",
          extension: ".pdf",
          mimeType: "application/pdf",
          previewKind: "pdf",
        },
      ]);
    if (path.endsWith("/download"))
      return route.fulfill({
        contentType: "text/plain",
        body: "Saved content",
        headers: { "Content-Disposition": 'attachment; filename="Notes.txt"' },
      });
    if (path.endsWith("/path")) {
      const id = path.split("/")[3];
      const chain = [];
      let current = folders.find((folder) => folder.id === id);
      while (current) {
        chain.unshift(current);
        current = folders.find(
          (folder) => folder.id === current?.parentFolderId,
        );
      }
      return chain.length
        ? json(chain)
        : json({ error: "Folder not found" }, 404);
    }
    if (path === "/api/folders" && method === "GET")
      return json(
        folders.filter(
          (folder) =>
            folder.parentFolderId === url.searchParams.get("parentFolderId"),
        ),
      );
    if (path === "/api/files" && method === "GET")
      return json(
        files.filter(
          (file) => file.folderId === url.searchParams.get("folderId"),
        ),
      );
    if (path.endsWith("/search")) {
      const name = (url.searchParams.get("name") || "").toLowerCase();
      return json(
        (path.includes("/folders/") ? folders : files).filter((item) =>
          item.name.toLowerCase().includes(name),
        ),
      );
    }
    if (path === "/api/folders" && method === "POST") {
      const body = request.postDataJSON();
      const id = `folder-${Date.now()}`;
      folders.push({
        id,
        name: body.name,
        parentFolderId: body.parentFolderId,
        ownerId: "user-1",
      });
      return json({ folderId: id });
    }
    if (path === "/api/files" && method === "POST") {
      const body = request.postDataBuffer()?.toString() || "";
      const name = /filename="([^"]+)"/.exec(body)?.[1] || "Upload.txt";
      const parent = /name="folderId"\r\n\r\n([^\r]+)/.exec(body)?.[1] || null;
      if (name.endsWith(".exe"))
        return json({ error: "File type '.exe' is not supported." }, 400);
      files.push({
        ...files[0],
        id: `file-${Date.now()}`,
        name,
        folderId: parent,
      });
      return json({ id: files.at(-1)!.id }, 201);
    }
    const id = path.split("/")[3];
    const item = path.includes("/folders/")
      ? folders.find((item) => item.id === id)
      : files.find((item) => item.id === id);
    if (!item) return json({ error: "Not found" }, 404);
    if (method === "DELETE") {
      folders = folders.filter((item) => item.id !== id);
      files = files.filter((item) => item.id !== id);
      return route.fulfill({ status: 204 });
    }
    const body = request.postDataJSON();
    if (path.endsWith("/rename")) item.name = body.newName;
    if (path.endsWith("/move")) {
      if ("parentFolderId" in item)
        item.parentFolderId = body.newParentFolderId;
      else item.folderId = body.newFolderId;
    }
    return route.fulfill({ status: 204 });
  });
}
export async function openActions(page: Page, name: string, action: string) {
  await page
    .getByRole("button", { name: `Actions for ${name}`, exact: true })
    .click();
  await page.getByRole("menuitem", { name: action, exact: true }).click();
}
export async function signIn(page: Page) {
  await page.goto("/");
  await page.getByLabel("Email address").fill("alex@example.com");
  await page.getByLabel("Password", { exact: true }).fill("Example123!");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("table")).toBeVisible();
}
