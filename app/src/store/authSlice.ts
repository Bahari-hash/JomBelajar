import { createSlice, type PayloadAction } from "@reduxjs/toolkit";
import type { CurrentUserProfile } from "@/features/auth/types";
import type { AuthStatus, ProfileStatus } from "@/providers/authContext";

export interface AuthState {
  status: AuthStatus;
  profile: CurrentUserProfile | null;
  profileStatus: ProfileStatus;
  profileError: string | null;
}

const initialState: AuthState = {
  status: "loading",
  profile: null,
  profileStatus: "idle",
  profileError: null,
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setStatus(state, action: PayloadAction<AuthStatus>) {
      state.status = action.payload;
    },
    setProfileLoading(state) {
      state.profileStatus = "loading";
      state.profileError = null;
    },
    setProfile(state, action: PayloadAction<CurrentUserProfile>) {
      state.profile = action.payload;
      state.profileStatus = "ready";
      state.profileError = null;
      state.status = "authenticated";
    },
    setProfileError(state, action: PayloadAction<string>) {
      state.profileStatus = "error";
      state.profileError = action.payload;
    },
    clearAuth(state) {
      state.status = "anonymous";
      state.profile = null;
      state.profileStatus = "idle";
      state.profileError = null;
    },
  },
});

export const {
  clearAuth,
  setProfile,
  setProfileError,
  setProfileLoading,
  setStatus,
} = authSlice.actions;
export default authSlice.reducer;
