import { describe, expect, it } from "vitest";
import type { InternalAxiosRequestConfig } from "axios";
import {
  ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
  accountSecurityApi,
} from "@/features/auth/accountSecurityApi";
import { httpClient } from "@/services/httpClient";

describe("accountSecurityApi", () => {
  it("maps authenticated and anonymous security requests to backend contracts", async () => {
    const originalAdapter = httpClient.defaults.adapter;
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = async (config) => {
      requests.push(config);
      return {
        data: config.url === "/users/me/change-email"
          ? { id: "user-1", email: "new@example.test" }
          : undefined,
        status: config.method === "post" ? 200 : 204,
        statusText: "OK",
        headers: {},
        config,
      };
    };

    await accountSecurityApi.requestChangeEmailToken("new@example.test");
    await accountSecurityApi.changeEmail("new@example.test", "123456");
    await accountSecurityApi.requestResetPasswordToken();
    await accountSecurityApi.resetPassword("new-password", "123456");
    await accountSecurityApi.requestDeleteAccountToken();
    await accountSecurityApi.deleteAccount("123456");
    await accountSecurityApi.requestForgotPasswordToken("user@example.test");
    await accountSecurityApi.forgotPassword(
      "user@example.test",
      "new-password",
      "123456",
    );

    expect(requests.map(({ method, url }) => [method, url])).toEqual([
      ["post", "/auth/change-email-token"],
      ["put", "/users/me/change-email"],
      ["post", "/auth/reset-password-token"],
      ["put", "/users/me/reset-password"],
      ["post", "/auth/delete-account-token"],
      ["delete", "/users/me/delete-account"],
      ["post", "/auth/forgot-password-token"],
      ["put", "/auth/forgot-password"],
    ]);
    expect(JSON.parse(requests[1]!.data)).toEqual({
      newEmail: "new@example.test",
      verificationCode: "123456",
    });
    expect(JSON.parse(requests[5]!.data)).toEqual({
      verificationCode: "123456",
    });
    expect(JSON.parse(requests[7]!.data)).toEqual({
      email: "user@example.test",
      newPassword: "new-password",
      verificationCode: "123456",
    });
    expect(requests[1]!.skipAuthRefresh).toBe(true);
    expect(requests[5]!.skipAuthRefresh).toBe(true);
    expect(requests[7]!.skipAuth).toBe(true);
    expect(requests.every(({ timeout }) =>
      timeout === ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS)).toBe(true);
    httpClient.defaults.adapter = originalAdapter;
  });
});
