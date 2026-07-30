import { ApiError } from "@/services/problemDetails.js";
import {
  requestLogin,
  requestLogout,
  requestRefresh,
} from "@/services/authTransport.js";
import { USER_ROLES } from "@/services/roles.js";
import { tokenVault } from "@/services/tokenVault.js";

let refreshPromise = null;

async function logoutWithTimeout(session, timeoutMilliseconds) {
  const controller = new AbortController();
  const timeoutId = window.setTimeout(
    () => controller.abort(),
    timeoutMilliseconds,
  );
  try {
    await requestLogout({
      token: session.token,
      refreshToken: session.refreshToken,
      signal: controller.signal,
    });
  } finally {
    window.clearTimeout(timeoutId);
  }
}

async function bestEffortLogout(session) {
  try {
    await logoutWithTimeout(session, 3000);
  } catch {
    // Local cleanup remains authoritative when the remote session cannot be revoked.
  }
}

async function acceptAdminSession(session) {
  if (session.user.role !== USER_ROLES.ADMIN) {
    await bestEffortLogout(session);
    tokenVault.clear();
    throw new ApiError("仅管理员可以访问管理后台。", {
      status: 403,
      kind: "forbidden",
    });
  }

  try {
    tokenVault.install(session.token, session.refreshToken);
  } catch (error) {
    await bestEffortLogout(session);
    throw error;
  }

  return { user: session.user, expiresIn: session.expiresIn };
}

async function rotateAdminSession(signal) {
  const refreshToken = tokenVault.getRefreshToken();
  if (!refreshToken) {
    throw new ApiError("登录状态已失效，请重新登录。", {
      status: 401,
      kind: "session",
    });
  }

  return acceptAdminSession(await requestRefresh(refreshToken, signal));
}

/** Coordinates login, single-flight refresh, and unconditional local logout. */
export const authSession = Object.freeze({
  async login(credentials, signal) {
    return acceptAdminSession(await requestLogin(credentials, signal));
  },

  hasRefreshToken() {
    return Boolean(tokenVault.getRefreshToken());
  },

  refresh(signal) {
    if (!refreshPromise) {
      refreshPromise = rotateAdminSession(signal).finally(() => {
        refreshPromise = null;
      });
    }

    return refreshPromise;
  },

  async logout() {
    const token = tokenVault.getAccessToken();
    const refreshToken = tokenVault.getRefreshToken();
    try {
      if (token && refreshToken) {
        await logoutWithTimeout({ token, refreshToken }, 5000);
      }
    } catch {
      // Signing out locally must not depend on the remote revocation request succeeding.
    } finally {
      tokenVault.clear();
    }
  },

  clear() {
    tokenVault.clear();
  },
});
