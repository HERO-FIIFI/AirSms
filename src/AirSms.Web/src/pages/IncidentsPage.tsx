import { useEffect, useState } from "react";
import { ApiError } from "../api/client";
import { listIncidents } from "../api/incidentsApi";
import { useAuth } from "../auth/AuthContext";
import { CreateIncidentModal } from "../features/incidents/CreateIncidentModal";
import { IncidentTable } from "../features/incidents/IncidentTable";
import type {
  Incident,
  IncidentCategory,
  IncidentSeverity,
  IncidentStatus,
} from "../types/incidents";
import {
  incidentCategories,
  incidentSeverities,
  incidentStatuses,
} from "../types/incidents";
import { spaced } from "../utils/format";

const pageSize = 20;

export function IncidentsPage() {
  const { user } = useAuth();
  const [status, setStatus] = useState<IncidentStatus | "">("");
  const [severity, setSeverity] = useState<IncidentSeverity | "">("");
  const [category, setCategory] = useState<IncidentCategory | "">("");
  const [page, setPage] = useState(1);
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const canPreviewWorkflow = user?.role === "Supervisor" || user?.role === "Administrator";

  useEffect(() => {
    let ignore = false;
    setLoading(true);
    setError("");

    listIncidents({
      page,
      pageSize,
      status: status || undefined,
      severity: severity || undefined,
      category: category || undefined,
    })
      .then((items) => {
        if (!ignore) {
          setIncidents(items);
        }
      })
      .catch((exception) => {
        if (!ignore) {
          setError(
            exception instanceof ApiError
              ? exception.problem?.detail || exception.message
              : "Unable to load incidents.",
          );
        }
      })
      .finally(() => {
        if (!ignore) {
          setLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [category, page, severity, status]);

  function resetFilters(next: () => void) {
    next();
    setPage(1);
  }

  function onCreated(incident: Incident) {
    setShowCreate(false);
    setStatus("");
    setSeverity("");
    setCategory("");
    setPage(1);
    setIncidents((current) => [incident, ...current].slice(0, pageSize));
  }

  return (
    <section className="page">
      <div className="page-title-row">
        <div>
          <h2>Incidents</h2>
          <p className="muted">Newest reported incidents are shown first.</p>
        </div>
        <button className="primary-button" onClick={() => setShowCreate(true)}>
          Report Incident
        </button>
      </div>

      {canPreviewWorkflow && (
        <div className="notice">Workflow actions are available through the API and will be added here next.</div>
      )}

      <div className="filters" aria-label="Incident filters">
        <label>
          Status
          <select value={status} onChange={(event) => resetFilters(() => setStatus(event.target.value as IncidentStatus | ""))}>
            <option value="">All</option>
            {incidentStatuses.map((item) => (
              <option key={item} value={item}>
                {spaced(item)}
              </option>
            ))}
          </select>
        </label>
        <label>
          Severity
          <select value={severity} onChange={(event) => resetFilters(() => setSeverity(event.target.value as IncidentSeverity | ""))}>
            <option value="">All</option>
            {incidentSeverities.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </label>
        <label>
          Category
          <select value={category} onChange={(event) => resetFilters(() => setCategory(event.target.value as IncidentCategory | ""))}>
            <option value="">All</option>
            {incidentCategories.map((item) => (
              <option key={item} value={item}>
                {spaced(item)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {error && <div className="form-error">{error}</div>}
      {loading ? <div className="empty-state">Loading incidents...</div> : <IncidentTable incidents={incidents} />}

      <div className="pagination">
        <button className="secondary-button" onClick={() => setPage((value) => value - 1)} disabled={page === 1}>
          Previous
        </button>
        <span>Page {page}</span>
        <button className="secondary-button" onClick={() => setPage((value) => value + 1)} disabled={incidents.length < pageSize}>
          Next
        </button>
      </div>

      {showCreate && <CreateIncidentModal onClose={() => setShowCreate(false)} onCreated={onCreated} />}
    </section>
  );
}
