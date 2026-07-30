import { configureStore } from "@reduxjs/toolkit";

/** Stable empty root state until the first real cross-page state owner is introduced. */
const INITIAL_STATE = Object.freeze({});

function rootReducer(state = INITIAL_STATE) {
  return state;
}

/** TinyLang admin Redux store; feature reducers are added only with real consumers. */
export const store = configureStore({
  reducer: rootReducer,
});
