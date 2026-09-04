import { Link } from "react-router-dom";
import { Badge } from "../../components/Badge";
import type { Incident, IncidentSeverity, IncidentStatus } from "../../types/incidents";
import { formatDate, spaced } from "../../utils/format";

type Props = {
  incidents: Incident[];
};

const severityTone: Record<IncidentSeverity, "low" | "medium" | "high" | "critical"> = {
  Low: "low",
  Medium: "medium",
  High: "high",
  Critical: "critical",
};

const statusTone: Record<IncidentStatus, "neutral" | "low" | "medium" | "high" | "closed"> = {
  Open: "high",
  Assigned: "medium",
  InProgress: "medium",
  Resolved: "low",
  Closed: "closed",
};

export function IncidentTable({ incidents }: Props) {
  if (incidents.length === 0) {
    return <div className="empty-state">No incidents match the current view.</div>;
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Title</th>
            <th>Category</th>
            <th>Severity</th>
            <th>Status</th>
            <th>Flight</th>
            <th>Aircraft</th>
            <th>Reported</th>
            <th>Assigned</th>
          </tr>
        </thead>
        <tbody>
          {incidents.map((incident) => (
            <tr key={incident.id}>
              <td>
                <Link to={`/incidents/${incident.id}`} className="table-link">
                  {incident.title}
                </Link>
              </td>
              <td>{spaced(incident.category)}</td>
              <td>
                <Badge value={incident.severity} tone={severityTone[incident.severity]} />
              </td>
              <td>
                <Badge value={spaced(incident.status)} tone={statusTone[incident.status]} />
              </td>
              <td>{incident.flightNumber || "-"}</td>
              <td>{incident.aircraftRegistration || "-"}</td>
              <td>{formatDate(incident.reportedAt)}</td>
              <td className="mono">{incident.assignedToUserId || "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
