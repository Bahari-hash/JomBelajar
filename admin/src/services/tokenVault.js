let accessToken = null;

/** Keeps access tokens in memory; the refresh token is browser-owned as an HttpOnly cookie. */
export const tokenVault = Object.freeze({
  getAccessToken() {
    return accessToken;
  },

  install(token) {
    if (!token) {
      throw new Error("Cannot install an incomplete authentication session.");
    }

    accessToken = token;
  },

  clear() {
    accessToken = null;
  },
});
