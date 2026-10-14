import * as Menu from '@radix-ui/react-dropdown-menu';
import { Cloud, Home, LogOut } from 'lucide-react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import Login from './Login';
import { SessionLifecycle, useSession } from './auth';
import { setSession } from './session';
import styles from './App.module.css';
export function Profile() {
  const session = useSession()!;
  const client = useQueryClient();
  return <Menu.Root><Menu.Trigger className="avatar" aria-label="User profile">{session.user.firstName.slice(0, 1).toUpperCase()}</Menu.Trigger><Menu.Portal><Menu.Content className="menu profile" align="end" sideOffset={8}><div className="profileInfo"><strong>{session.user.firstName}</strong><span>{session.user.email}</span></div><Menu.Separator className="separator" /><Menu.Item className="menuItem" onSelect={() => { setSession(null); client.clear(); }}><LogOut size={18} />Log out</Menu.Item></Menu.Content></Menu.Portal></Menu.Root>;
}
export function Shell({ children, actions }: { children: ReactNode; actions?: ReactNode }) {
  return <div className={styles.shell}><aside className={styles.sidebar}><Link className={styles.brand} to="/"><Cloud size={32} /><span>GDriveLight</span></Link>{actions}<nav aria-label="Main navigation"><Link className={styles.navItem} to="/"><Home size={20} />Home</Link></nav><p className={styles.sidebarNote}>A little space for everything.</p></aside><main id="main" className={styles.surface}>{children}</main></div>;
}
function Protected() {
  const session = useSession();
  const location = useLocation();
  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <Shell><header className={styles.header}><h1>Home</h1><Profile /></header><div className={styles.empty}><Cloud size={48} /><h2>Your cloud, in one place</h2><p>Keep your files organized and close at hand.</p></div></Shell>;
}
export default function App() {
  const session = useSession();
  return <><SessionLifecycle /><Routes><Route path="/login" element={session ? <Navigate to="/" replace /> : <Login />} /><Route path="*" element={<Protected />} /></Routes></>;
}
