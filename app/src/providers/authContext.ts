import { createContext } from "react";
import type {
  CurrentUserProfile,
  UpdateProfileRequest,
} from "@/features/auth/types";
import type { ChangeEmailResponse } from "@/features/auth/accountSecurityApi";

export type AuthStatus = "loading" | "anonymous" | "authenticated";
export type ProfileStatus = "idle" | "loading" | "ready" | "error";

export interface AuthContextValue {
  status: AuthStatus;
  profile: CurrentUserProfile | null;
  profileStatus: ProfileStatus;
  profileError: string | null;
  login: (email: string, password: string) => Promise<CurrentUserProfile>;
  register: (
    email: string,
    password: string,
    verificationCode: string,
  ) => Promise<void>;
  requestRegisterToken: (email: string) => Promise<void>;
  refreshProfile: () => Promise<CurrentUserProfile>;
  updateProfile: (request: UpdateProfileRequest) => Promise<CurrentUserProfile>;
  requestChangeEmailToken: (newEmail: string) => Promise<void>;
  changeEmail: (
    newEmail: string,
    verificationCode: string,
  ) => Promise<ChangeEmailResponse>;
  requestResetPasswordToken: () => Promise<void>;
  resetPassword: (
    newPassword: string,
    verificationCode: string,
  ) => Promise<void>;
  requestDeleteAccountToken: () => Promise<void>;
  deleteAccount: (verificationCode: string) => Promise<void>;
  requestForgotPasswordToken: (email: string) => Promise<void>;
  forgotPassword: (
    email: string,
    newPassword: string,
    verificationCode: string,
  ) => Promise<void>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
