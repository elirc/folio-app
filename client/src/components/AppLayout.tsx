import { Link, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export function AppLayout() {
  const { member, logout } = useAuth();
  const navigate = useNavigate();

  function signOut() {
    logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <Link to="/" className="brand">
          <img src="/folio.svg" alt="" width={22} height={22} />
          <span>Folio</span>
        </Link>
        {member && (
          <div className="app-user">
            <span className="app-user-name">
              {member.name} <span className="app-user-role">· {member.role}</span>
            </span>
            <button type="button" className="share-btn" onClick={signOut}>
              Sign out
            </button>
          </div>
        )}
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  );
}
