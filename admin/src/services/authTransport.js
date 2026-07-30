import { requestApi } from "@/services/httpTransport.js";
import { normalizeAuthUser } from "@/services/roles.js";

function normalizeTokenResponse(value) {
  if (
    !value ||
    typeof value.token !== "string" ||
    !value.token ||
    typeof value.refreshToken !== "string" ||
    !value.refreshToken ||
    !Number.isFinite(value.expiresIn)
  ) {
    throw new Error("API returned an invalid authentication response.");
  }

  return {
    token: value.token,
    refreshToken: value.refreshToken,
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
      signal,
    }),
  );
}

export async function requestRefresh(refreshToken, signal) {
  return normalizeTokenResponse(
    await requestApi({
      path: "/auth/refresh",
      method: "POST",
      body: { refreshToken },
      signal,
    }),
  );
}

export async function requestLogout({ token, refreshToken, signal }) {
  return requestApi({
    path: "/auth/logout",
    method: "POST",
    body: { refreshToken },
    accessToken: token,
    signal,
  });
}
