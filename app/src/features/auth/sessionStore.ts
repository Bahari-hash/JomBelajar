import type { AuthTokenResponse, UserResponse } from "@/features/auth/types";

interface SessionSnapshot {
  accessToken: string | null;
  user: UserResponse | null;
}

let snapshot: SessionSnapshot = {
  accessToken: null,
  user: null,
};
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((listener) => listener());
}

/** Keeps sensitive access credentials in memory and publishes coarse session changes. */
export function setSession(response: AuthTokenResponse) {
  snapshot = {
    accessToken: response.token,
    user: response.user,
  };
  notify();
}

export function clearSession() {
  snapshot = { accessToken: null, user: null };
  notify();
}

export function getAccessToken() {
  return snapshot.accessToken;
}

export function getSessionSnapshot() {
  return snapshot;
}

export function subscribeToSession(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
