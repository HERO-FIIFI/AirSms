export type UserRole = "OperationsAgent" | "Supervisor" | "Administrator";

export type User = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type LoginResponse = {
  accessToken: string;
  expiresAt: string;
  user: User;
};
