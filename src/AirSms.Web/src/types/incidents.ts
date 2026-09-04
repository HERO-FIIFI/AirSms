export type IncidentCategory =
  | "FlightOperations"
  | "Technical"
  | "CustomerService"
  | "GroundOperations"
  | "Security"
  | "Other";

export type IncidentSeverity = "Low" | "Medium" | "High" | "Critical";

export type IncidentStatus =
  | "Open"
  | "Assigned"
  | "InProgress"
  | "Resolved"
  | "Closed";

export type Incident = {
  id: string;
  title: string;
  description: string;
  category: IncidentCategory;
  severity: IncidentSeverity;
  status: IncidentStatus;
  flightNumber: string | null;
  aircraftRegistration: string | null;
  reportedByUserId: string;
  assignedToUserId: string | null;
  reportedAt: string;
  resolvedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
};

export type CreateIncidentRequest = {
  title: string;
  description: string;
  category: IncidentCategory;
  severity: IncidentSeverity;
  flightNumber?: string;
  aircraftRegistration?: string;
};

export type ListIncidentFilters = {
  status?: IncidentStatus;
  severity?: IncidentSeverity;
  category?: IncidentCategory;
  page: number;
  pageSize: number;
};

export const incidentCategories: IncidentCategory[] = [
  "FlightOperations",
  "Technical",
  "CustomerService",
  "GroundOperations",
  "Security",
  "Other",
];

export const incidentSeverities: IncidentSeverity[] = [
  "Low",
  "Medium",
  "High",
  "Critical",
];

export const incidentStatuses: IncidentStatus[] = [
  "Open",
  "Assigned",
  "InProgress",
  "Resolved",
  "Closed",
];
