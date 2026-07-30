import dayjs from "dayjs";

/** Formats DateTimeOffset values in the administrator's local browser timezone. */
export function formatDateTime(value) {
  if (!value) return "从未";
  const date = dayjs(value);
  return date.isValid() ? date.format("YYYY-MM-DD HH:mm") : "未知";
}
