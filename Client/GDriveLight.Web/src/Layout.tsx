import * as Menu from "@radix-ui/react-dropdown-menu";
import { Cloud, Home, LogOut } from "lucide-react";
import { Link } from "react-router-dom";
import { type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSession } from "./auth";
import { setSession } from "./session";
import styles from "./App.module.css";
export function Profile() {
  const session = useSession()!;
  const client = useQueryClient();
  return (
    <Menu.Root>
      <Menu.Trigger className="avatar" aria-label="User profile">
        {session.user.firstName.slice(0, 1).toUpperCase()}
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Content className="menu profile" align="end" sideOffset={8}>
          <div className="profileInfo">
            <strong>{session.user.firstName}</strong>
            <span>{session.user.email}</span>
          </div>
          <Menu.Separator className="separator" />
          <Menu.Item
            className="menuItem"
            onSelect={() => {
              setSession(null);
              client.clear();
            }}
          >
            <LogOut size={18} />
            Log out
          </Menu.Item>
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
export function Shell({
  children,
  actions,
}: {
  children: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <>
      <a className="skipLink" href="#main">
        Skip to files
      </a>
      <div className={styles.shell}>
        <aside className={styles.sidebar}>
          <Link className={styles.brand} to="/">
            <Cloud size={32} />
            <span>GDriveLight</span>
          </Link>
          {actions}
          <nav aria-label="Main navigation">
            <Link className={styles.navItem} to="/">
              <Home size={20} />
              Home
            </Link>
          </nav>
          <p className={styles.sidebarNote}>A little space for everything.</p>
        </aside>
        <main id="main" className={styles.surface} tabIndex={-1}>
          {children}
        </main>
      </div>
    </>
  );
}
