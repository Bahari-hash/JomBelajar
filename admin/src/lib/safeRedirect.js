/** Accepts only same-origin router paths and rejects protocol-relative destinations. */
export function getSafeRedirect(value, fallback = "/") {
  return typeof value === "string" &&
    value.startsWith("/") &&
    !value.startsWith("//")
    ? value
    : fallback;
}
