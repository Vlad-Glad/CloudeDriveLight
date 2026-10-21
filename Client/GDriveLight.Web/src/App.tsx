import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import Login from "./Login";
import Drive from "./Drive";
import SyncedFolders from "./SyncedFolders";
import type { ReactNode } from "react";
import { SessionLifecycle, useSession } from "./auth";
function Protected({ children = <Drive /> }: { children?: ReactNode }) {
  const session = useSession();
  const location = useLocation();
  return session ? (
    children
  ) : (
    <Navigate
      to="/login"
      replace
      state={{ from: location.pathname + location.search }}
    />
  );
}
export default function App() {
  const session = useSession();
  return (
    <>
      <SessionLifecycle />
      <Routes>
        <Route
          path="/login"
          element={session ? <Navigate to="/" replace /> : <Login />}
        />
        <Route path="/" element={<Protected />} />
        <Route path="/folders/:folderId" element={<Protected />} />
        <Route
          path="/synced"
          element={<Protected><SyncedFolders /></Protected>}
        />
        <Route
          path="/synced/folders/:folderId"
          element={<Protected><Drive synced /></Protected>}
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
