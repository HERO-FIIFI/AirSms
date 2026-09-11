import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiError } from "../api/client";
import {
  assignIncident,
  closeIncident,
  getIncident,
  resolveIncident,
  startIncident,
} from "../api/incidentsApi";
import { listUsers } from "../api/usersApi";
import { useAuth } from "../auth/AuthContext";
import { Badge } from "../components/Badge";
import type { User } from "../types/auth";
import type { Incident, IncidentStatus } from "../types/incidents";
import { formatDate, spaced } from "../utils/format";

// Mirrors the domain state machine in Incident.cs. Anything not listed here is
// rejected server-side with a 409, so the UI simply does not offer it.
const canAssign: IncidentStatus[] = ["Open", "Assigned", "InProgress"];

export function IncidentDetailPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [users, setUsers] = useState<User[]>([]);
  const [assignee, setAssignee] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [actionError, setActionError] = useState("");

  const canRunWorkflow =
    user?.role === "Supervisor" || user?.role === "Administrator";

  useEffect(() => {
    if (!id) {
      return;
    }

    getIncident(id)
      .then(setIncident)
      .catch((exception) => {
        setError(
          exception instanceof ApiError && exception.status === 404
            ? "Incident was not found."
            : "Unable to load incident.",
        );
      })
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    if (!canRunWorkflow) {
      return;
    }

    let ignore = false;
    listUsers()
      .then((result) => {
        if (!ignore) {
          setUsers(result.filter((candidate) => candidate.isActive));
        }
      })
      .catch(() => {
        if (!ignore) {
          setUsers([]);
        }
      });

    return () => {
      ignore = true;
    };
  }, [canRunWorkflow]);

  const runAction = useCallback(
    async (name: string, action: () => Promise<Incident>) => {
      setActionError("");
      setBusy(name);
      try {
        setIncident(await action());
      } catch (exception) {
        setActionError(
          exception instanceof ApiError
            ? exception.message
            : "Unable to complete that action.",
        );
      } finally {
        setBusy("");
      }
    },
    [],
  );

  const assignableUsers = useMemo(
    () => [...users].sort((a, b) => a.email.localeCompare(b.email)),
    [users],
  );

  if (loading) {
    return <div className="empty-state">Loading incident...</div>;
  }

  if (error || !incident) {
    return (
      <section className="page">
        <Link to="/incidents" className="back-link">Back to incidents</Link>
        <div className="form-error">{error || "Incident was not found."}</div>
      </section>
    );
  }

  const status = incident.status;
  const showAssign = canRunWorkflow && canAssign.includes(status);
  const showStart = canRunWorkflow && status === "Assigned";
  const showResolve = canRunWorkflow && status === "InProgress";
  const showClose = canRunWorkflow && status === "Resolved";
  const hasAnyAction = showAssign || showStart || showResolve || showClose;

  return (
    <section className="page">
      <Link to="/incidents" className="back-link">Back to incidents</Link>
      <div className="detail-header">
        <div>
          <h2>{incident.title}</h2>
        </div>
        <Badge value={spaced(incident.status)} />
      </div>

      {canRunWorkflow && (
        <div className="workflow-bar">
          <div className="workflow-heading">
            <span className="workflow-label">Workflow</span>
            <span className="muted">{workflowHint(status)}</span>
          </div>

          {hasAnyAction ? (
            <div className="workflow-actions">
              {showAssign && (
                <div className="workflow-assign">
                  <select
                    aria-label="Assign to"
                    value={assignee}
                    onChange={(event) => setAssignee(event.target.value)}
                  >
                    <option value="">Select assignee...</option>
                    {assignableUsers.map((candidate) => (
                      <option key={candidate.id} value={candidate.id}>
                        {candidate.firstName} {candidate.lastName} ({candidate.email})
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="secondary-button"
                    disabled={!assignee || busy !== ""}
                    onClick={() =>
                      runAction("assign", () => assignIncident(incident.id, assignee))
                    }
                  >
                    {busy === "assign"
                      ? "Assigning..."
                      : status === "Open"
                        ? "Assign"
                        : "Reassign"}
                  </button>
                </div>
              )}

              {showStart && (
                <button
                  type="button"
                  className="primary-button"
                  disabled={busy !== ""}
                  onClick={() => runAction("start", () => startIncident(incident.id))}
                >
                  {busy === "start" ? "Starting..." : "Start work"}
                </button>
              )}

              {showResolve && (
                <button
                  type="button"
                  className="primary-button"
                  disabled={busy !== ""}
                  onClick={() => runAction("resolve", () => resolveIncident(incident.id))}
                >
                  {busy === "resolve" ? "Resolving..." : "Resolve"}
                </button>
              )}

              {showClose && (
                <button
                  type="button"
                  className="primary-button"
                  disabled={busy !== ""}
                  onClick={() => runAction("close", () => closeIncident(incident.id))}
                >
                  {busy === "close" ? "Closing..." : "Close incident"}
                </button>
              )}
            </div>
          ) : (
            <div className="muted">This incident is closed. No further action is available.</div>
          )}

          {actionError && <div className="form-error">{actionError}</div>}
        </div>
      )}

      <div className="detail-grid">
        <Detail label="Description" value={incident.description} wide />
        <Detail label="Severity" value={incident.severity} />
        <Detail label="Category" value={spaced(incident.category)} />
        <Detail label="Flight Number" value={incident.flightNumber || "-"} />
        <Detail label="Aircraft" value={incident.aircraftRegistration || "-"} />
        <Detail label="Reported At" value={formatDate(incident.reportedAt)} />
        <Detail label="Resolved At" value={formatDate(incident.resolvedAt)} />
        <Detail label="Reporter ID" value={incident.reportedByUserId} mono />
        <Detail
          label="Assigned To"
          value={describeUser(users, incident.assignedToUserId)}
          mono={!users.length}
        />
      </div>
    </section>
  );
}

function workflowHint(status: IncidentStatus): string {
  switch (status) {
    case "Open":
      return "Assign this incident to move it forward.";
    case "Assigned":
      return "Start work, or reassign to a different owner.";
    case "InProgress":
      return "Resolve once the issue has been addressed.";
    case "Resolved":
      return "Close to complete the incident record.";
    default:
      return "This incident has completed its lifecycle.";
  }
}

function describeUser(users: User[], userId: string | null): string {
  if (!userId) {
    return "-";
  }

  const match = users.find((candidate) => candidate.id === userId);
  return match ? `${match.firstName} ${match.lastName} (${match.email})` : userId;
}

function Detail({
  label,
  value,
  wide = false,
  mono = false,
}: {
  label: string;
  value: string;
  wide?: boolean;
  mono?: boolean;
}) {
  return (
    <div className={wide ? "detail-item detail-wide" : "detail-item"}>
      <div className="muted">{label}</div>
      <div className={mono ? "mono" : undefined}>{value}</div>
    </div>
  );
}
