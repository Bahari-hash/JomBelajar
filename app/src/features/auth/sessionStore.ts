import type { AuthTokenResponse, UserResponse } from "@/features/auth/types";
import {
  clearStoredRefreshToken,
  writeStoredRefreshToken,
} from "@/features/auth/authStorage";

interface SessionSnapshot {
  accessToken: string | null;
  refreshToken: string | null;
  user: UserResponse | null;
}

let snapshot: SessionSnapshot = {
  accessToken: null,
  refreshToken: null,
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
    refreshToken: response.refreshToken,
    user: response.user,
  };
  writeStoredRefreshToken(response.refreshToken);
  notify();
}

export function setRefreshToken(refreshToken: string) {
  snapshot = { ...snapshot, refreshToken };
  writeStoredRefreshToken(refreshToken);
  notify();
}

export function clearSession() {
  snapshot = { accessToken: null, refreshToken: null, user: null };
  clearStoredRefreshToken();
  notify();
}

export function getAccessToken() {
  return snapshot.accessToken;
}

export function getRefreshToken() {
  return snapshot.refreshToken;
}

export function getSessionSnapshot() {
  return snapshot;
}

export function subscribeToSession(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
