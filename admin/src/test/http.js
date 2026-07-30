import { AxiosError, AxiosHeaders } from "axios";
import { vi } from "vitest";
import { httpClient } from "@/services/httpTransport.js";

/** Builds Axios-compatible responses and errors for isolated API contract tests. */
export function axiosResponse(data, status = 200) {
  return {
    data,
    status,
    statusText: status === 204 ? "No Content" : "OK",
    headers: new AxiosHeaders({ "Content-Type": "application/json" }),
    config: {},
  };
}

export function axiosHttpError(data, status) {
  return new AxiosError(
    `Request failed with status code ${status}`,
    AxiosError.ERR_BAD_RESPONSE,
    {},
    null,
    axiosResponse(data, status),
  );
}

export function mockHttpClient(implementation) {
  return vi.spyOn(httpClient, "request").mockImplementation(implementation);
}

export function adminTokenResponse(overrides = {}) {
  return {
    token: "access-token",
    refreshToken: "refresh-token",
    expiresIn: 900,
    user: {
      id: "11111111-1111-1111-1111-111111111111",
      email: "admin@example.test",
      role: 2,
    },
    ...overrides,
  };
}

export function userListItem(overrides = {}) {
  return {
    id: "22222222-2222-2222-2222-222222222222",
    username: "alice",
    email: "alice@example.test",
    role: 1,
    nickname: "Alice",
    avatarUrl: null,
    isBanned: false,
    bannedAt: null,
    bannedReason: null,
    isDeleted: false,
    deletedAt: null,
    createdAt: "2026-07-29T08:00:00+00:00",
    updatedAt: "2026-07-29T08:00:00+00:00",
    lastLoginAt: null,
    activeSessionCount: 1,
    ...overrides,
  };
}
