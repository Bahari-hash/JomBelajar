import axios from "axios";
import { apiBaseUrl } from "@/lib/env";
import {
  clearSession,
  getAccessToken,
  setSession,
} from "@/features/auth/sessionStore";
import type { AuthTokenResponse } from "@/features/auth/types";

declare module "axios" {
  interface AxiosRequestConfig {
    skipAuth?: boolean;
    authRetry?: boolean;
    skipAuthRefresh?: boolean;
  }
}

/** Shared Axios transport for all TinyLang API services. */
export const httpClient = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    Accept: "application/json",
  },
});

type RefreshHandler = () => Promise<AuthTokenResponse>;
let refreshHandler: RefreshHandler | null = null;
let refreshPromise: Promise<AuthTokenResponse> | null = null;

/** Installs the single refresh operation used by the Axios response interceptor. */
export function configureAuthRefresh(handler: RefreshHandler) {
  refreshHandler = handler;
}

httpClient.interceptors.request.use((config) => {
  const accessToken = getAccessToken();
  if (accessToken && !config.skipAuth) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});

httpClient.interceptors.response.use(
  (response) => response,
  async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401) {
      return Promise.reject(error);
    }

    const originalRequest = error.config;
    if (originalRequest?.skipAuthRefresh) {
      return Promise.reject(error);
    }
    if (
      !originalRequest ||
      originalRequest.skipAuth ||
      originalRequest.authRetry ||
      !refreshHandler
    ) {
      if (!originalRequest?.skipAuth) {
        clearSession();
      }
      return Promise.reject(error);
    }

    refreshPromise ??= refreshHandler()
      .then((response) => {
        setSession(response);
        return response;
      })
      .catch((refreshError: unknown) => {
        clearSession();
        throw refreshError;
      })
      .finally(() => {
        refreshPromise = null;
      });

    try {
      const response = await refreshPromise;
      originalRequest.authRetry = true;
      originalRequest.headers.Authorization = `Bearer ${response.token}`;
      return httpClient(originalRequest);
    } catch (refreshError) {
      return Promise.reject(refreshError);
    }
  },
);
