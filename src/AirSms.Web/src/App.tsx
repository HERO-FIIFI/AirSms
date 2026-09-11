import { Navigate, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./auth/ProtectedRoute";
import { AppLayout } from "./layouts/AppLayout";
import { IncidentDetailPage } from "./pages/IncidentDetailPage";
import { IncidentsPage } from "./pages/IncidentsPage";
import { LoginPage } from "./pages/LoginPage";
import { NotificationsPage } from "./pages/NotificationsPage";
import { UsersPage } from "./pages/UsersPage";
import { useAuth } from "./auth/AuthContext";

export function App() {
  const auth = useAuth();

  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<Navigate to={auth.isAuthenticated ? "/incidents" : "/login"} replace />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route path="/incidents" element={<IncidentsPage />} />
          <Route path="/incidents/:id" element={<IncidentDetailPage />} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route
            path="/users"
            element={
              auth.user?.role === "Administrator"
                ? <UsersPage />
                : <Navigate to="/incidents" replace />
            }
          />
        </Route>
      </Route>
    </Routes>
  );
}
