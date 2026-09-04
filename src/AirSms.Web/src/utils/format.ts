export function formatDate(value: string | null): string {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

export function spaced(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2");
}
