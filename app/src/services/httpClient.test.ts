import axios, { type AxiosResponse } from "axios";
import { describe, expect, it } from "vitest";
import {
  clearSession,
  getAccessToken,
  setSession,
} from "@/features/auth/sessionStore";
import { configureAuthRefresh, httpClient } from "@/services/httpClient";
import type { AuthTokenResponse } from "@/features/auth/types";
import { authApi } from "@/features/auth/authApi";

describe("httpClient", () => {
  it("uses the configured API base and requests JSON responses", () => {
    expect(httpClient.defaults.baseURL).toBe("/api");
    expect(httpClient.defaults.headers.Accept).toBe("application/json");
  });

  it("shares one refresh request for concurrent 401 responses and retries once", async () => {
    const originalAdapter = httpClient.defaults.adapter;
    const initial: AuthTokenResponse = {
      token: "access-old",
      refreshToken: "refresh-old",
      expiresIn: 60,
      user: { id: "user-1", email: "user@example.test", role: "User" },
    };
    const next: AuthTokenResponse = {
      ...initial,
      token: "access-new",
      refreshToken: "refresh-new",
    };
    setSession(initial);
    let requestCount = 0;
    let refreshCount = 0;
    const release: { current: () => void } = { current: () => undefined };
    const refreshGate = new Promise<void>((resolve) => {
      release.current = resolve;
    });

    configureAuthRefresh(async (refreshToken) => {
      refreshCount += 1;
      expect(refreshToken).toBe("refresh-old");
      await refreshGate;
      return next;
    });
    httpClient.defaults.adapter = async (config) => {
      requestCount += 1;
      if (!config.authRetry) {
        const response: AxiosResponse = {
          data: { errorCode: "TokenInvalid" },
          status: 401,
          statusText: "Unauthorized",
          headers: {},
          config,
        };
        throw new axios.AxiosError(
          "unauthorized",
          "ERR_BAD_REQUEST",
          config,
          undefined,
          response,
        );
      }
      expect(config.headers.Authorization).toBe("Bearer access-new");
      return {
        data: { ok: true },
        status: 200,
        statusText: "OK",
        headers: {},
        config,
      };
    };

    const first = httpClient.get("/protected/one");
    const second = httpClient.get("/protected/two");
    release.current();
    await expect(Promise.all([first, second])).resolves.toHaveLength(2);
    expect(refreshCount).toBe(1);
    expect(requestCount).toBe(4);
    expect(getAccessToken()).toBe("access-new");
    httpClient.defaults.adapter = originalAdapter;
  });

  it("clears credentials when refresh fails and never retries the refresh request", async () => {
    const originalAdapter = httpClient.defaults.adapter;
    setSession({
      token: "access",
      refreshToken: "refresh",
      expiresIn: 60,
      user: { id: "user-1", email: "user@example.test", role: "User" },
    });
    configureAuthRefresh(async () => {
      throw new Error("refresh failed");
    });
    httpClient.defaults.adapter = async (config) => {
      const response: AxiosResponse = {
        data: {},
        status: 401,
        statusText: "Unauthorized",
        headers: {},
        config,
      };
      throw new axios.AxiosError(
        "unauthorized",
        "ERR_BAD_REQUEST",
        config,
        undefined,
        response,
      );
    };

    await expect(httpClient.get("/protected")).rejects.toThrow(
      "refresh failed",
    );
    expect(getAccessToken()).toBeNull();
    expect(sessionStorage.getItem("tinylang.auth.session.v1")).toBeNull();
    clearSession();
    httpClient.defaults.adapter = originalAdapter;
  });

  it("does not refresh an explicitly unauthenticated request", async () => {
    const originalAdapter = httpClient.defaults.adapter;
    let refreshCount = 0;
    configureAuthRefresh(async () => {
      refreshCount += 1;
      throw new Error("must not run");
    });
    httpClient.defaults.adapter = async (config) => {
      const response: AxiosResponse = {
        data: {},
        status: 401,
        statusText: "Unauthorized",
        headers: {},
        config,
      };
      throw new axios.AxiosError(
        "unauthorized",
        "ERR_BAD_REQUEST",
        config,
        undefined,
        response,
      );
    };

    await expect(
      httpClient.post("/auth/refresh", {}, { skipAuth: true }),
    ).rejects.toThrow();
    expect(refreshCount).toBe(0);
    httpClient.defaults.adapter = originalAdapter;
  });

  it("does not attach the consumer bearer token to an OSS presigned upload", async () => {
    const originalAdapter = httpClient.defaults.adapter;
    setSession({
      token: "access-secret",
      refreshToken: "refresh-secret",
      expiresIn: 60,
      user: { id: "user-1", email: "user@example.test", role: "User" },
    });
    const file = new File(["avatar"], "avatar.png", { type: "image/png" });
    httpClient.defaults.adapter = async (config) => {
      expect(config.url).toBe("https://storage.example.test/avatar");
      expect(config.headers.Authorization).toBeUndefined();
      return { data: {}, status: 200, statusText: "OK", headers: {}, config };
    };

    await authApi.uploadToPresignedUrl(
      "https://storage.example.test/avatar",
      file,
    );
    httpClient.defaults.adapter = originalAdapter;
  });
});
