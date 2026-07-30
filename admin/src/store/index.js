import { configureStore } from "@reduxjs/toolkit";
import { baseApi } from "@/services/baseApi.js";
import { authReducer } from "@/store/authSlice.js";

/** Creates an isolated store for the auth state and single RTK Query cache boundary. */
export function createAppStore(preloadedState) {
  return configureStore({
    reducer: {
      auth: authReducer,
      [baseApi.reducerPath]: baseApi.reducer,
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(baseApi.middleware),
    preloadedState,
  });
}

export const store = createAppStore();
