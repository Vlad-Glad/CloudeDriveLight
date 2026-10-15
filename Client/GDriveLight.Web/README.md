# GDriveLight Web

Browser client for the existing ASP.NET Core API. React, TypeScript, Vite, React Router, TanStack Query and Radix primitives. The colors, typography, navigation, table and dialogs follow the WPF Desktop client. UI labels use English, matching Desktop.

## Run locally

Use Node.js 22.14+ (or a compatible newer LTS) and npm 11. The npm 10 peer resolver can fail on the Vitest dependency tree; use `npx --yes npm@11 ci` without changing your global npm.

From `Client/GDriveLight.Web`:

```powershell
npx --yes npm@11 ci
Copy-Item .env.example .env.local
npm run dev
```

Open http://127.0.0.1:5173. Start the existing backend separately, for example from the repository root:

```powershell
dotnet run --project Server/GDriveLight.Api --launch-profile http
```

The API needs its existing database and JWT configuration. This client does not copy credentials or change the backend. `API_PROXY_TARGET` in `.env.local` defaults to `http://localhost:5229`. It is a Vite server setting, not a browser-exposed secret. All browser requests use relative `/api` paths through the development proxy. Use a trusted certificate if targeting an HTTPS development API.

## Features

- Sign in, registration, profile and logout.
- Folder URLs, browser back/forward, breadcrumbs, global search, file type filter and sorting.
- Create, rename, move and permanently delete files/folders. Folder deletion includes its descendants and requires confirmation.
- Authenticated downloads and multiple-file uploads through New → Upload files or drag-and-drop onto the file area. Uploads keep their original destination when navigating to another folder.
- Upload progress, per-file cancellation and explicit retry. 100% means bytes have been sent; completion waits for the API response. Dropped directories are rejected with a helpful message. Closing/reloading the tab ends its queue; navigating between cloud folders preserves it.
- Responsive layout, keyboard menus/dialogs, empty/loading/error states, retry controls and a session-scoped upload queue.

Synchronization folders and device management are intentionally outside this browser client's scope.

## API behavior

JWTs are stored in sessionStorage for this tab, with a memory-only fallback when storage is blocked. Claims are decoded only for display and expiry; the API validates authorization. Logout clears the query cache. Expired JWTs and HTTP 401 return the user to sign-in. The current API has no refresh-token endpoint.

The file type catalog comes from `/api/files/types`. Unsupported types, duplicate names and permission failures use the server's error response. Uploads do not overwrite existing files. Cancelling stops the client request but cannot roll back a file already committed by the server. After cancellation or a connection failure, check the destination before retrying. Upload size limits belong to the API and reverse proxy; HTTP 413 is shown as an upload error. Downloads currently buffer the response as a Blob, so very large downloads consume browser memory.

## Verification

```powershell
npm test
npm run build
npm run test:e2e
```

Playwright runs desktop and mobile layouts with isolated mocked API data, covering authentication, folder navigation, search/filtering, CRUD, upload/download, error recovery and keyboard focus. Windows uses installed Microsoft Edge; on other platforms run `npx playwright install chromium` first. Browser tests use port 5174, separate from preview on 5173. Reports, screenshots and traces are ignored in Git.

## Production

```powershell
npm run build
```

Serve `dist/` as static assets. Route `/api/*` to the .NET API on the same origin, and return `index.html` for client routes such as `/folders/<id>` and `/login`. `deploy/nginx.conf` is an example; adjust its root directory and upstream, and configure HTTPS on your production ingress. Keep API and reverse-proxy upload limits consistent. The Vite development proxy is not part of the production build. `vite preview` is only a static build preview and does not proxy API requests.

## Structure

- `session.ts`, `auth.tsx`, `api.ts`: session lifecycle and API boundary.
- `drive-api.ts`, `operations-api.ts`: DTOs, queries and mutations.
- `Drive.tsx`, `Layout.tsx`, `FileIcon.tsx`: file browser UI.
- `OperationDialog.tsx`: create/rename/move/delete dialogs.
- `useUploads.ts`, `UploadQueue.tsx`: upload orchestration and status UI.
- CSS modules and `tokens.css`: Desktop-derived visual system.
