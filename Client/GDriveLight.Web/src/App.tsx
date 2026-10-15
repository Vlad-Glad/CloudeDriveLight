import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import Login from "./Login";
import Drive from "./Drive";
import { SessionLifecycle, useSession } from "./auth";
function Protected() {
  const session = useSession();
  const location = useLocation();
  return session ? (
    <Drive />
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
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  );
}
