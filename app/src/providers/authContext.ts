import { createContext } from "react";
import type {
  CurrentUserProfile,
  UpdateProfileRequest,
} from "@/features/auth/types";

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
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
