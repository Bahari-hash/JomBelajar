import { httpClient } from "@/services/httpClient";

/** Bounds account-security calls so a stalled network cannot leave a form pending forever. */
export const ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS = 15_000;

export interface ChangeEmailResponse {
  id: string;
  email: string;
}

/** Defines authenticated account-security and anonymous recovery requests. */
export const accountSecurityApi = {
  requestChangeEmailToken: (newEmail: string) =>
    httpClient.post<void>(
      "/auth/change-email-token",
      { newEmail },
      { timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS },
    ),
  changeEmail: (newEmail: string, verificationCode: string) =>
    httpClient.put<ChangeEmailResponse>(
      "/users/me/change-email",
      { newEmail, verificationCode },
      {
        skipAuthRefresh: true,
        timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
      },
    ),
  requestResetPasswordToken: () =>
    httpClient.post<void>("/auth/reset-password-token", undefined, {
      timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
    }),
  resetPassword: (newPassword: string, verificationCode: string) =>
    httpClient.put<void>(
      "/users/me/reset-password",
      { newPassword, verificationCode },
      {
        skipAuthRefresh: true,
        timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
      },
    ),
  requestDeleteAccountToken: () =>
    httpClient.post<void>("/auth/delete-account-token", undefined, {
      timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
    }),
  deleteAccount: (verificationCode: string) =>
    httpClient.delete<void>("/users/me/delete-account", {
      data: { verificationCode },
      skipAuthRefresh: true,
      timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
    }),
  requestForgotPasswordToken: (email: string) =>
    httpClient.post<void>(
      "/auth/forgot-password-token",
      { email },
      {
        skipAuth: true,
        timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
      },
    ),
  forgotPassword: (
    email: string,
    newPassword: string,
    verificationCode: string,
  ) =>
    httpClient.put<void>(
      "/auth/forgot-password",
      { email, newPassword, verificationCode },
      {
        skipAuth: true,
        timeout: ACCOUNT_SECURITY_REQUEST_TIMEOUT_MS,
      },
    ),
};
