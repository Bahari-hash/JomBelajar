import { useEffect, useState, type ReactNode } from "react";
import { Provider } from "react-redux";
import { authApi, refreshAuthSession } from "@/features/auth/authApi";
import { accountSecurityApi } from "@/features/auth/accountSecurityApi";
import { toApiRequestError } from "@/features/auth/authErrors";
import { readStoredRefreshToken } from "@/features/auth/authStorage";
import {
  clearSession,
  getRefreshToken,
  getSessionSnapshot,
  setSession,
  subscribeToSession,
} from "@/features/auth/sessionStore";
import type {
  CurrentUserProfile,
  UpdateProfileRequest,
} from "@/features/auth/types";
import { configureAuthRefresh } from "@/services/httpClient";
import { AuthContext } from "@/providers/authContext";
import {
  clearAuth,
  setProfile,
  setProfileError,
  setProfileLoading,
  setStatus,
} from "@/store/authSlice";
import { createAppStore } from "@/store/store";
import { useAppDispatch, useAuthState } from "@/store/hooks";

interface AuthProviderProps {
  children: ReactNode;
}

configureAuthRefresh(refreshAuthSession);

type RestoreResult =
  | { kind: "anonymous" }
  | { kind: "authenticated"; profile: CurrentUserProfile }
  | { kind: "profileError"; message: string };

let restorePromise: Promise<RestoreResult> | null = null;

/** Shares one startup refresh across React StrictMode effect replays. */
function restoreStoredSession() {
  restorePromise ??= (async (): Promise<RestoreResult> => {
    const storedRefreshToken = readStoredRefreshToken();
    if (!storedRefreshToken) {
      return { kind: "anonymous" };
    }

    try {
      const response = await refreshAuthSession(storedRefreshToken);
      setSession(response);
      try {
        const profileResponse = await authApi.getCurrentProfile();
        return { kind: "authenticated", profile: profileResponse.data };
      } catch (profileLoadError) {
        if (!getSessionSnapshot().accessToken) {
          return { kind: "anonymous" };
        }
        return {
          kind: "profileError",
          message: toApiRequestError(profileLoadError, "资料加载失败，请重试。")
            .message,
        };
      }
    } catch {
      clearSession();
      return { kind: "anonymous" };
    }
  })().finally(() => {
    restorePromise = null;
  });

  return restorePromise;
}

function AuthSessionProvider({ children }: AuthProviderProps) {
  const dispatch = useAppDispatch();
  const { status, profile, profileStatus, profileError } = useAuthState();

  useEffect(() => {
    let active = true;
    const unsubscribe = subscribeToSession(() => {
      if (!active) {
        return;
      }

      const nextSnapshot = getSessionSnapshot();
      if (!nextSnapshot.accessToken) {
        dispatch(clearAuth());
      }
    });

    void restoreStoredSession().then((result) => {
      if (!active) {
        return;
      }
      if (result.kind === "authenticated") {
        dispatch(setProfile(result.profile));
      } else if (result.kind === "profileError") {
        dispatch(setStatus("authenticated"));
        dispatch(setProfileError(result.message));
      } else {
        dispatch(clearAuth());
      }
    });

    return () => {
      active = false;
      unsubscribe();
    };
  }, [dispatch]);

  const login = async (email: string, password: string) => {
    try {
      const tokenResponse = (await authApi.login(email, password)).data;
      setSession(tokenResponse);
      dispatch(setStatus("authenticated"));
      dispatch(setProfileLoading());
      const profileResponse = await authApi.getCurrentProfile();
      dispatch(setProfile(profileResponse.data));
      return profileResponse.data;
    } catch (error) {
      clearSession();
      dispatch(clearAuth());
      throw toApiRequestError(error, "登录失败，请稍后重试。");
    }
  };

  const register = async (
    email: string,
    password: string,
    verificationCode: string,
  ) => {
    try {
      await authApi.register(email, password, verificationCode);
    } catch (error) {
      throw toApiRequestError(error, "注册失败，请稍后重试。");
    }
  };

  const requestRegisterToken = async (email: string) => {
    try {
      await authApi.requestRegisterToken(email);
    } catch (error) {
      throw toApiRequestError(error, "验证码发送失败，请稍后重试。");
    }
  };

  const refreshProfile = async () => {
    dispatch(setProfileLoading());
    try {
      const response = await authApi.getCurrentProfile();
      dispatch(setProfile(response.data));
      return response.data;
    } catch (error) {
      const requestError = toApiRequestError(error, "资料加载失败，请重试。");
      dispatch(setProfileError(requestError.message));
      throw requestError;
    }
  };

  const updateProfile = async (request: UpdateProfileRequest) => {
    try {
      const response = await authApi.updateProfile(request);
      dispatch(setProfile(response.data));
      return response.data;
    } catch (error) {
      throw toApiRequestError(error, "资料保存失败，请检查后重试。");
    }
  };

  const requestChangeEmailToken = async (newEmail: string) => {
    try {
      await accountSecurityApi.requestChangeEmailToken(newEmail);
    } catch (error) {
      throw toApiRequestError(error, "验证码发送失败，请稍后重试。");
    }
  };

  const changeEmail = async (newEmail: string, verificationCode: string) => {
    try {
      const response = await accountSecurityApi.changeEmail(
        newEmail,
        verificationCode,
      );
      clearSession();
      dispatch(clearAuth());
      return response.data;
    } catch (error) {
      throw toApiRequestError(error, "邮箱修改失败，请检查后重试。");
    }
  };

  const requestResetPasswordToken = async () => {
    try {
      await accountSecurityApi.requestResetPasswordToken();
    } catch (error) {
      throw toApiRequestError(error, "验证码发送失败，请稍后重试。");
    }
  };

  const resetPassword = async (
    newPassword: string,
    verificationCode: string,
  ) => {
    try {
      await accountSecurityApi.resetPassword(newPassword, verificationCode);
      clearSession();
      dispatch(clearAuth());
    } catch (error) {
      throw toApiRequestError(error, "密码重置失败，请检查后重试。");
    }
  };

  const requestForgotPasswordToken = async (email: string) => {
    try {
      await accountSecurityApi.requestForgotPasswordToken(email);
    } catch (error) {
      throw toApiRequestError(error, "验证码发送失败，请稍后重试。");
    }
  };

  const forgotPassword = async (
    email: string,
    newPassword: string,
    verificationCode: string,
  ) => {
    try {
      await accountSecurityApi.forgotPassword(
        email,
        newPassword,
        verificationCode,
      );
    } catch (error) {
      throw toApiRequestError(error, "密码重置失败，请检查后重试。");
    }
  };

  const logout = async () => {
    const refreshToken = getRefreshToken();
    try {
      if (refreshToken) {
        await authApi.logout(refreshToken);
      }
    } catch {
      // Logout is best effort; local credentials must be cleared regardless of the response.
    } finally {
      clearSession();
      dispatch(clearAuth());
    }
  };

  return (
    <AuthContext
      value={{
        status,
        profile,
        profileStatus,
        profileError,
        login,
        register,
        requestRegisterToken,
        refreshProfile,
        updateProfile,
        requestChangeEmailToken,
        changeEmail,
        requestResetPasswordToken,
        resetPassword,
        requestForgotPasswordToken,
        forgotPassword,
        logout,
      }}
    >
      {children}
    </AuthContext>
  );
}

/** Restores and owns the current consumer session without exposing raw credentials. */
export default function AuthProvider({ children }: AuthProviderProps) {
  const [store] = useState(createAppStore);
  return (
    <Provider store={store}>
      <AuthSessionProvider>{children}</AuthSessionProvider>
    </Provider>
  );
}
