type BadgeProps = {
  value: string;
  tone?: "neutral" | "low" | "medium" | "high" | "critical" | "closed";
};

export function Badge({ value, tone = "neutral" }: BadgeProps) {
  return <span className={`badge badge-${tone}`}>{value}</span>;
}
