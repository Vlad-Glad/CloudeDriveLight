export interface Session { token: string; user: { id: string; email: string; firstName: string }; expiresAt: number }
const storageKey = 'gdrivelight.session';
export function parseSession(token: string): Session | null {
  try {
    const encoded = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    const claims = JSON.parse(new TextDecoder().decode(bytes));
    if (typeof claims.sub !== 'string' || typeof claims.exp !== 'number' || claims.exp * 1000 <= Date.now()) return null;
    return { token, user: { id: claims.sub, email: claims.email || '', firstName: claims.firstName || 'User' }, expiresAt: claims.exp * 1000 };
  } catch { return null; }
}
function restore() { try { return parseSession(sessionStorage.getItem(storageKey) || ''); } catch { return null; } }
let current = restore();
const listeners = new Set<() => void>();
export const getSession = () => current;
export const subscribeSession = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; };
export function setSession(token: string | null) {
  const next = token ? parseSession(token) : null;
  if (token && !next) throw new Error('The server returned an invalid or expired session. Please sign in again.');
  current = next;
  try { if (next) sessionStorage.setItem(storageKey, next.token); else sessionStorage.removeItem(storageKey); } catch { /* Memory-only session when browser storage is unavailable. */ }
  listeners.forEach(listener => listener());
}
export function expireSession(token: string) { if (current?.token === token) setSession(null); }
