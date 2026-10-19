import { expireSession, getSession } from "./session";
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}
export async function errorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json();
    if (body.errors) return Object.values(body.errors).flat().join(" ");
    return (
      body.error ||
      body.detail ||
      body.title ||
      `Request failed (${response.status}).`
    );
  } catch {
    return response.status === 413
      ? "This file exceeds the server upload limit."
      : `Request failed (${response.status}). Please try again.`;
  }
}
export function buildApiUrl(path: string): string {
  const rawBase = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim();
  const cleanPath = path.replace(/^\/+/, "");
  if (!rawBase) {
    return `/api/${cleanPath}`;
  }
  const baseWithoutTrailingSlash = rawBase.replace(/\/+$/, "");
  if (baseWithoutTrailingSlash.endsWith("/api")) {
    return `${baseWithoutTrailingSlash}/${cleanPath}`;
  }
  return `${baseWithoutTrailingSlash}/api/${cleanPath}`;
}

export async function apiResponse(
  path: string,
  options: RequestInit = {},
  authenticated = true,
): Promise<Response> {
  const token = authenticated ? getSession()?.token : undefined;
  const headers = new Headers(options.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (options.body && !(options.body instanceof FormData))
    headers.set("Content-Type", "application/json");
  let response: Response;
  try {
    response = await fetch(buildApiUrl(path), { ...options, headers });
  } catch (error) {
    if (error instanceof Error && error.name === "AbortError") throw error;
    throw new ApiError(
      "Cannot reach the server. Check your connection and try again.",
      0,
    );
  }
  if (!response.ok) {
    if (response.status === 401 && token) expireSession(token);
    throw new ApiError(await errorMessage(response), response.status);
  }
  return response;
}
export async function request<T>(
  path: string,
  options: RequestInit = {},
  authenticated = true,
): Promise<T> {
  const response = await apiResponse(path, options, authenticated);
  if (response.status === 204 || response.headers.get("content-length") === "0")
    return undefined as T;
  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}
export const authenticate = (
  mode: "login" | "register",
  credentials: { email: string; password: string; firstName?: string },
) =>
  request<{ token: string }>(
    `auth/${mode}`,
    { method: "POST", body: JSON.stringify(credentials) },
    false,
  );
