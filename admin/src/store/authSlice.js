import { createSlice } from "@reduxjs/toolkit";

export const AUTH_STATUS = Object.freeze({
  BOOTSTRAPPING: "bootstrapping",
  AUTHENTICATED: "authenticated-admin",
  UNAUTHENTICATED: "unauthenticated",
  FORBIDDEN: "forbidden",
});

const initialState = {
  status: AUTH_STATUS.BOOTSTRAPPING,
  user: null,
  message: null,
};

/** Stores only display-safe authentication state; credentials and tokens remain outside Redux. */
const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    sessionAuthenticated(state, action) {
      state.status = AUTH_STATUS.AUTHENTICATED;
      state.user = action.payload;
      state.message = null;
    },
    sessionUnauthenticated(state, action) {
      state.status = AUTH_STATUS.UNAUTHENTICATED;
      state.user = null;
      state.message = action.payload ?? null;
    },
    sessionForbidden(state, action) {
      state.status = AUTH_STATUS.FORBIDDEN;
      state.user = null;
      state.message = action.payload ?? "仅管理员可以访问管理后台。";
    },
  },
});

export const {
  sessionAuthenticated,
  sessionForbidden,
  sessionUnauthenticated,
} = authSlice.actions;
export const authReducer = authSlice.reducer;
