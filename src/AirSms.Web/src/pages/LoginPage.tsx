import { FormEvent, useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { AviationBackdrop } from "../components/AviationBackdrop";
import { useAuth } from "../auth/AuthContext";

export function LoginPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  if (auth.isAuthenticated) {
    return <Navigate to="/incidents" replace />;
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (!email.trim() || !password) {
      setError("Enter both your email address and password.");
      return;
    }

    setLoading(true);
    try {
      await auth.login(email.trim(), password);
      const from = (location.state as { from?: Location } | null)?.from?.pathname;
      navigate(from || "/incidents", { replace: true });
    } catch (exception) {
      setError(
        exception instanceof ApiError && exception.status === 401
          ? "That email and password combination was not recognised."
          : exception instanceof ApiError && exception.status === 429
            ? "Too many sign-in attempts. Wait a moment and try again."
            : "AirSms is not reachable right now. Try again shortly.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="login-page">
      <AviationBackdrop />

      <section className="login-panel">
        <div className="login-lockup">
          <svg className="login-mark" viewBox="0 0 32 32" aria-hidden="true">
            <path d="M6 26 18.5 4h5.2L14.6 26Z" />
            <path d="M13.4 26h5.1l7-15.4h-5.1Z" className="login-mark-fin" />
          </svg>
          <div>
            <div className="login-wordmark">AIRSMS</div>
            <div className="login-wordmark-sub">Airline Safety Management System</div>
          </div>
        </div>

        <div className="login-intro">
          <h1>Sign in</h1>
          <p className="muted">
            Use your AirSms operations credentials to reach the incident register.
          </p>
        </div>

        <form onSubmit={onSubmit} className="form">
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              placeholder="you@airsms.com"
            />
          </label>
          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              placeholder="••••••••"
            />
          </label>
          {error && (
            <div className="form-error" role="alert">
              {error}
            </div>
          )}
          <button type="submit" className="primary-button" disabled={loading}>
            {loading ? "Signing in..." : "Sign in"}
          </button>
        </form>

        <div className="login-footnote">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <rect x="4.8" y="10.4" width="14.4" height="10.2" rx="2" />
            <path d="M8.4 10.4V7.6a3.6 3.6 0 0 1 7.2 0v2.8" />
          </svg>
          <span>Access is granted by role. Contact an administrator for an account.</span>
        </div>
      </section>
    </main>
  );
}
