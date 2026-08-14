import { requestApi } from "@/services/httpTransport.js";
import { normalizeAuthUser } from "@/services/roles.js";

function normalizeTokenResponse(value) {
  if (
    !value ||
    typeof value.token !== "string" ||
    !value.token ||
    !Number.isFinite(value.expiresIn)
  ) {
    throw new Error("API returned an invalid authentication response.");
  }

  return {
    token: value.token,
    expiresIn: value.expiresIn,
    user: normalizeAuthUser(value.user),
  };
}

/** Token-bearing auth calls stay outside Redux so credentials never enter actions or cache. */
export async function requestLogin(credentials, signal) {
  return normalizeTokenResponse(
    await requestApi({
      path: "/auth/login",
      method: "POST",
      body: credentials,
      withCredentials: true,
      signal,
    }),
  );
}

export async function requestRefresh(signal) {
  return normalizeTokenResponse(
    await requestApi({
      path: "/auth/refresh",
      method: "POST",
      body: {},
      withCredentials: true,
      signal,
    }),
  );
}

export async function requestLogout({ token, signal }) {
  return requestApi({
    path: "/auth/logout",
    method: "POST",
    body: {},
    accessToken: token,
    withCredentials: true,
    signal,
  });
}
