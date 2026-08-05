import { configureStore } from "@reduxjs/toolkit";
import authReducer from "@/store/authSlice";

/** Creates the consumer Redux store; sensitive credentials are intentionally outside this state tree. */
export function createAppStore() {
  return configureStore({
    reducer: { auth: authReducer },
  });
}

export type AppStore = ReturnType<typeof createAppStore>;
export type RootState = ReturnType<AppStore["getState"]>;
export type AppDispatch = AppStore["dispatch"];
