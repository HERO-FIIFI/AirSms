import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ApiError } from "../api/client";
import { getIncident } from "../api/incidentsApi";
import { Badge } from "../components/Badge";
import type { Incident } from "../types/incidents";
import { formatDate, spaced } from "../utils/format";

export function IncidentDetailPage() {
  const { id } = useParams();
  const [incident, setIncident] = useState<Incident | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

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

  return (
    <section className="page">
      <Link to="/incidents" className="back-link">Back to incidents</Link>
      <div className="detail-header">
        <div>
          <p className="eyebrow">Incident</p>
          <h2>{incident.title}</h2>
        </div>
        <Badge value={spaced(incident.status)} />
      </div>
      <div className="detail-grid">
        <Detail label="Description" value={incident.description} wide />
        <Detail label="Severity" value={incident.severity} />
        <Detail label="Category" value={spaced(incident.category)} />
        <Detail label="Flight Number" value={incident.flightNumber || "-"} />
        <Detail label="Aircraft" value={incident.aircraftRegistration || "-"} />
        <Detail label="Reported At" value={formatDate(incident.reportedAt)} />
        <Detail label="Resolved At" value={formatDate(incident.resolvedAt)} />
        <Detail label="Reporter ID" value={incident.reportedByUserId} mono />
        <Detail label="Assigned User ID" value={incident.assignedToUserId || "-"} mono />
      </div>
    </section>
  );
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
