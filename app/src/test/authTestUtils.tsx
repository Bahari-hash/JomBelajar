import type { AuthContextValue } from "@/providers/authContext";

/** Creates a complete controllable auth context for isolated component behavior tests. */
export function createAuthContextValue(
  overrides: Partial<AuthContextValue> = {},
): AuthContextValue {
  return {
    status: "anonymous",
    profile: null,
    profileStatus: "idle",
    profileError: null,
    login: async () => {
      throw new Error("login not configured");
    },
    register: async () => undefined,
    requestRegisterToken: async () => undefined,
    refreshProfile: async () => {
      throw new Error("refreshProfile not configured");
    },
    updateProfile: async () => {
      throw new Error("updateProfile not configured");
    },
    requestChangeEmailToken: async () => undefined,
    changeEmail: async () => {
      throw new Error("changeEmail not configured");
    },
    requestResetPasswordToken: async () => undefined,
    resetPassword: async () => undefined,
    requestDeleteAccountToken: async () => undefined,
    deleteAccount: async () => undefined,
    requestForgotPasswordToken: async () => undefined,
    forgotPassword: async () => undefined,
    logout: async () => undefined,
    ...overrides,
  };
}
