import { useEffect, useSyncExternalStore } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { getSession, setSession, subscribeSession } from './session';
export function useSession() { return useSyncExternalStore(subscribeSession, getSession); }
export function SessionLifecycle() {
  const session = useSession();
  const client = useQueryClient();
  useEffect(() => {
    if (!session) { client.clear(); return; }
    const expire = () => { if (Date.now() >= session.expiresAt) setSession(null); };
    const timer = window.setInterval(expire, 1000);
    window.addEventListener('focus', expire);
    return () => { clearInterval(timer); window.removeEventListener('focus', expire); };
  }, [session, client]);
  return null;
}
