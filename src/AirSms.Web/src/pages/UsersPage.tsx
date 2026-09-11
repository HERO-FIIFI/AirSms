import { FormEvent, useEffect, useState } from "react";
import { ApiError } from "../api/client";
import {
  changeUserRole,
  listUsers,
  resetUserPassword,
  setUserActive,
} from "../api/usersApi";
import { useAuth } from "../auth/AuthContext";
import { Badge } from "../components/Badge";
import type { User, UserRole } from "../types/auth";
import { formatDate, spaced } from "../utils/format";

const roles: UserRole[] = ["OperationsAgent", "Supervisor", "Administrator"];

export function UsersPage() {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [resetTarget, setResetTarget] = useState<User | null>(null);

  useEffect(() => {
    listUsers()
      .then(setUsers)
      .catch(() => setError("Unable to load users."))
      .finally(() => setLoading(false));
  }, []);

  function replaceUser(updated: User) {
    setUsers((current) =>
      current.map((candidate) => (candidate.id === updated.id ? updated : candidate)),
    );
  }

  async function runAction(id: string, action: () => Promise<User>, message: string) {
    setError("");
    setNotice("");
    setBusyId(id);
    try {
      replaceUser(await action());
      setNotice(message);
    } catch (exception) {
      setError(
        exception instanceof ApiError
          ? exception.message
          : "Unable to update that user.",
      );
    } finally {
      setBusyId("");
    }
  }

  if (loading) {
    return <div className="empty-state">Loading users...</div>;
  }

  return (
    <section className="page">
      <div className="page-title-row">
        <div>
          <h2>User Administration</h2>
          <p className="muted">
            Manage roles, account access, and credentials for AirSms staff.
          </p>
        </div>
      </div>

      {error && <div className="form-error">{error}</div>}
      {notice && <div className="notice">{notice}</div>}

      {users.length === 0 ? (
        <div className="empty-state">No user accounts found.</div>
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((row) => {
                const isSelf = row.id === currentUser?.id;
                const disabled = busyId === row.id;

                return (
                  <tr key={row.id}>
                    <td>
                      {row.firstName} {row.lastName}
                      {isSelf && <span className="self-tag">You</span>}
                    </td>
                    <td>{row.email}</td>
                    <td>
                      <select
                        aria-label={`Role for ${row.email}`}
                        value={row.role}
                        disabled={disabled || isSelf}
                        onChange={(event) =>
                          runAction(
                            row.id,
                            () => changeUserRole(row.id, event.target.value as UserRole),
                            `Role updated for ${row.email}.`,
                          )
                        }
                      >
                        {roles.map((role) => (
                          <option key={role} value={role}>
                            {spaced(role)}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <Badge
                        value={row.isActive ? "Active" : "Inactive"}
                        tone={row.isActive ? "low" : "closed"}
                      />
                    </td>
                    <td>{formatDate(row.createdAt)}</td>
                    <td>
                      <div className="row-actions">
                        <button
                          type="button"
                          className="secondary-button"
                          disabled={disabled || isSelf}
                          onClick={() =>
                            runAction(
                              row.id,
                              () => setUserActive(row.id, !row.isActive),
                              `${row.email} is now ${row.isActive ? "inactive" : "active"}.`,
                            )
                          }
                        >
                          {row.isActive ? "Deactivate" : "Activate"}
                        </button>
                        <button
                          type="button"
                          className="secondary-button"
                          disabled={disabled}
                          onClick={() => setResetTarget(row)}
                        >
                          Reset password
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {resetTarget && (
        <ResetPasswordModal
          user={resetTarget}
          onClose={() => setResetTarget(null)}
          onDone={(updated) => {
            replaceUser(updated);
            setResetTarget(null);
            setNotice(`Password reset for ${updated.email}.`);
          }}
        />
      )}
    </section>
  );
}

function ResetPasswordModal({
  user,
  onClose,
  onDone,
}: {
  user: User;
  onClose: () => void;
  onDone: (updated: User) => void;
}) {
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (password.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    if (password !== confirmation) {
      setError("Passwords do not match.");
      return;
    }

    setSaving(true);
    let updated: User;
    try {
      updated = await resetUserPassword(user.id, password);
    } catch (exception) {
      setError(
        exception instanceof ApiError
          ? exception.message
          : "Unable to reset that password.",
      );
      setSaving(false);
      return;
    }

    // onDone unmounts this modal, so it runs last and no state is set afterwards.
    onDone(updated);
  }

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="modal-header">
          <h2>Reset password</h2>
          <button type="button" className="icon-button" onClick={onClose} aria-label="Close">
            &times;
          </button>
        </div>
        <p className="muted">
          Set a new password for <strong>{user.email}</strong>. Share it over a trusted
          channel and ask them to change it after signing in.
        </p>
        <form className="form" onSubmit={onSubmit}>
          <label>
            New password
            <input
              type="password"
              value={password}
              autoComplete="new-password"
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>
          <label>
            Confirm password
            <input
              type="password"
              value={confirmation}
              autoComplete="new-password"
              onChange={(event) => setConfirmation(event.target.value)}
            />
          </label>
          {error && <div className="form-error">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="secondary-button" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="primary-button" disabled={saving}>
              {saving ? "Saving..." : "Reset password"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
