import { configureStore } from "@reduxjs/toolkit";
import { articleApi } from "@/features/articles/articleApi";
import { videoApi } from "@/features/videos/videoApi";
import authReducer from "@/store/authSlice";

/** Creates the consumer Redux store; sensitive credentials are intentionally outside this state tree. */
export function createAppStore() {
  return configureStore({
    reducer: {
      auth: authReducer,
      [articleApi.reducerPath]: articleApi.reducer,
      [videoApi.reducerPath]: videoApi.reducer,
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(articleApi.middleware, videoApi.middleware),
  });
}

export type AppStore = ReturnType<typeof createAppStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];
