import { configureStore } from "@reduxjs/toolkit";
import { articleApi } from "@/features/articles/articleApi";
import { paperApi } from "@/features/papers/paperApi";
import { wordStudyApi } from "@/features/wordStudy/wordStudyApi";
import { videoApi } from "@/features/videos/videoApi";
import authReducer from "@/store/authSlice";

/** Creates the consumer Redux store; sensitive credentials are intentionally outside this state tree. */
export function createAppStore() {
  return configureStore({
    reducer: {
      auth: authReducer,
      [articleApi.reducerPath]: articleApi.reducer,
      [paperApi.reducerPath]: paperApi.reducer,
      [videoApi.reducerPath]: videoApi.reducer,
      [wordStudyApi.reducerPath]: wordStudyApi.reducer,
    },
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(
        articleApi.middleware,
        paperApi.middleware,
        videoApi.middleware,
        wordStudyApi.middleware,
      ),
    enhancers: (getDefaultEnhancers) =>
      import.meta.env.MODE === "test"
        ? getDefaultEnhancers({ autoBatch: { type: "timer", timeout: 0 } })
        : getDefaultEnhancers(),
  });
}

export type AppStore = ReturnType<typeof createAppStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];
