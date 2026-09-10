import { useEffect, useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { getUnreadNotificationCount } from "../api/notificationsApi";
import { useAuth } from "../auth/AuthContext";

export function AppLayout() {
  const { user, logout } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);

  useEffect(() => {
    let ignore = false;

    getUnreadNotificationCount()
      .then((result) => {
        if (!ignore) {
          setUnreadCount(result.count);
        }
      })
      .catch(() => {
        if (!ignore) {
          setUnreadCount(0);
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div>
          <div className="brand">AirSms</div>
          <div className="brand-subtitle">Airline Service Management</div>
        </div>
        <nav className="nav">
          <NavLink to="/incidents">Incidents</NavLink>
          <NavLink to="/notifications" className="nav-row">
            <span>Notifications</span>
            {unreadCount > 0 && <span className="count-badge">{unreadCount}</span>}
          </NavLink>
        </nav>
        <div className="user-panel">
          <div className="user-name">
            {user?.firstName} {user?.lastName}
          </div>
          <div className="muted">{user?.role}</div>
          <button className="secondary-button full" onClick={logout}>
            Logout
          </button>
        </div>
      </aside>
      <main className="main">
        <header className="topbar">
          <div>
            <h1>Incident Management</h1>
          </div>
          <div className="identity">
            {user?.email}
            {unreadCount > 0 && <span className="topbar-badge">{unreadCount} unread</span>}
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
