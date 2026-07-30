export const REFRESH_TOKEN_STORAGE_KEY = "tinylang.admin.refresh-token";

let accessToken = null;

/** Keeps access tokens in memory and the rotating refresh token in this tab's session storage. */
export const tokenVault = Object.freeze({
  getAccessToken() {
    return accessToken;
  },

  getRefreshToken() {
    try {
      return window.sessionStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    } catch {
      return null;
    }
  },

  install(token, refreshToken) {
    if (!token || !refreshToken) {
      throw new Error("Cannot install an incomplete authentication session.");
    }

    try {
      window.sessionStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, refreshToken);
      accessToken = token;
    } catch {
      accessToken = null;
      try {
        window.sessionStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
      } catch {
        // The session is already unusable when storage access is unavailable.
      }
      throw new Error("当前浏览器无法安全保存登录会话。", { cause: "storage" });
    }
  },

  clear() {
    accessToken = null;
    try {
      window.sessionStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
    } catch {
      // Clearing the in-memory access token is still sufficient to stop authenticated requests.
    }
  },
});
