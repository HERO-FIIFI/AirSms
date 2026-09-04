import { FormEvent, useState } from "react";
import { ApiError } from "../../api/client";
import { createIncident } from "../../api/incidentsApi";
import type { Incident } from "../../types/incidents";
import {
  incidentCategories,
  incidentSeverities,
  type IncidentCategory,
  type IncidentSeverity,
} from "../../types/incidents";
import { spaced } from "../../utils/format";

type Props = {
  onClose: () => void;
  onCreated: (incident: Incident) => void;
};

export function CreateIncidentModal({ onClose, onCreated }: Props) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState<IncidentCategory>("Technical");
  const [severity, setSeverity] = useState<IncidentSeverity>("Medium");
  const [flightNumber, setFlightNumber] = useState("");
  const [aircraftRegistration, setAircraftRegistration] = useState("");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setError("");

    if (!title.trim() || !description.trim()) {
      setError("Title and description are required.");
      return;
    }

    setSaving(true);
    try {
      const incident = await createIncident({
        title,
        description,
        category,
        severity,
        flightNumber: flightNumber || undefined,
        aircraftRegistration: aircraftRegistration || undefined,
      });
      onCreated(incident);
    } catch (exception) {
      setError(
        exception instanceof ApiError
          ? exception.problem?.detail || exception.message
          : "Unable to report incident.",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="modal-backdrop" role="presentation">
      <section className="modal" role="dialog" aria-modal="true" aria-labelledby="report-title">
        <div className="modal-header">
          <h2 id="report-title">Report Incident</h2>
          <button className="icon-button" onClick={onClose} aria-label="Close">
            x
          </button>
        </div>
        <form className="form" onSubmit={onSubmit}>
          <label>
            Title
            <input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>
          <label>
            Description
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={4}
            />
          </label>
          <div className="form-grid">
            <label>
              Category
              <select
                value={category}
                onChange={(event) => setCategory(event.target.value as IncidentCategory)}
              >
                {incidentCategories.map((item) => (
                  <option key={item} value={item}>
                    {spaced(item)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Severity
              <select
                value={severity}
                onChange={(event) => setSeverity(event.target.value as IncidentSeverity)}
              >
                {incidentSeverities.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="form-grid">
            <label>
              Flight Number
              <input value={flightNumber} onChange={(event) => setFlightNumber(event.target.value)} />
            </label>
            <label>
              Aircraft
              <input
                value={aircraftRegistration}
                onChange={(event) => setAircraftRegistration(event.target.value)}
              />
            </label>
          </div>
          {error && <div className="form-error">{error}</div>}
          <div className="modal-actions">
            <button type="button" className="secondary-button" onClick={onClose}>
              Cancel
            </button>
            <button type="submit" className="primary-button" disabled={saving}>
              {saving ? "Reporting..." : "Report Incident"}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}
