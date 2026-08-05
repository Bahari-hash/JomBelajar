const DEFAULT_API_BASE_URL = "/api";

/** Normalizes a deploy-time API base URL and falls back for unsupported schemes. */
export function normalizeApiBaseUrl(value: unknown): string {
  if (typeof value !== "string" || value.trim() === "") {
    return DEFAULT_API_BASE_URL;
  }

  const candidate = value.trim();
  const isRelative = candidate.startsWith("/") && !candidate.startsWith("//");

  if (!isRelative) {
    try {
      const url = new URL(candidate);
      if (url.protocol !== "http:" && url.protocol !== "https:") {
        return DEFAULT_API_BASE_URL;
      }
    } catch {
      return DEFAULT_API_BASE_URL;
    }
  }

  return candidate.replace(/\/+$/, "") || "/";
}

export const apiBaseUrl = normalizeApiBaseUrl(
  import.meta.env.VITE_API_BASE_URL,
);
