const STORAGE_KEY = "tinylang.auth.session.v1";

interface StoredSession {
  refreshToken: string;
}

/** Persists only the rotatable refresh token for the lifetime of the browser tab. */
export function readStoredRefreshToken(
  storage: Pick<Storage, "getItem"> = sessionStorage,
) {
  try {
    const rawValue = storage.getItem(STORAGE_KEY);
    if (!rawValue) {
      return null;
    }

    const parsed: unknown = JSON.parse(rawValue);
    if (
      typeof parsed === "object" &&
      parsed !== null &&
      "refreshToken" in parsed &&
      typeof parsed.refreshToken === "string" &&
      parsed.refreshToken.length > 0
    ) {
      return parsed.refreshToken;
    }
  } catch {
    return null;
  }

  return null;
}

export function writeStoredRefreshToken(
  refreshToken: string,
  storage: Pick<Storage, "setItem"> = sessionStorage,
) {
  try {
    const value: StoredSession = { refreshToken };
    storage.setItem(STORAGE_KEY, JSON.stringify(value));
  } catch {
    // Private browsing can deny sessionStorage; the access token remains memory-only.
  }
}

export function clearStoredRefreshToken(
  storage: Pick<Storage, "removeItem"> = sessionStorage,
) {
  try {
    storage.removeItem(STORAGE_KEY);
  } catch {
    // Clearing an unavailable store is intentionally best effort.
  }
}

export const authStorageKey = STORAGE_KEY;
