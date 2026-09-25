const HAS_OFFSET = /(?:Z|[+-]\d{2}:?\d{2})$/i;

export function parseApiDate(value) {
  if (!value) return null;
  let raw = String(value).trim();
  if (!raw) return null;
  if (!raw.includes("T")) raw = raw.replace(" ", "T");
  const iso = HAS_OFFSET.test(raw) ? raw : `${raw}Z`;
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDate(value, options = {}) {
  const date = parseApiDate(value);
  if (!date) return "";
  const base = { dateStyle: "medium", timeStyle: "short", ...options };
  return date.toLocaleString("es-PE", base);
}

export function formatRangeDate(value) {
  const date = parseApiDate(value);
  if (!date) return "";
  return date.toLocaleString("es-PE", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function formatTime(value) {
  const date = parseApiDate(value);
  if (!date) return "";
  return date.toLocaleTimeString("es-PE", { hour: "2-digit", minute: "2-digit" });
}
