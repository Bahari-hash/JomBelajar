import { createApi } from "@reduxjs/toolkit/query/react";
import { authSession } from "@/services/authSession.js";
import { requestApi } from "@/services/httpTransport.js";
import { toRtkQueryError } from "@/services/problemDetails.js";
import { tokenVault } from "@/services/tokenVault.js";
import {
  sessionAuthenticated,
  sessionUnauthenticated,
} from "@/store/authSlice.js";

let sessionInvalidated = false;

async function rawBaseQuery(args, api) {
  try {
    return {
      data: await requestApi({
        path: args.url,
        method: args.method,
        body: args.body,
        accessToken: tokenVault.getAccessToken(),
        signal: api.signal,
      }),
    };
  } catch (error) {
    return { error: toRtkQueryError(error) };
  }
}

async function baseQueryWithRefresh(args, api) {
  let result = await rawBaseQuery(args, api);
  if (result.error?.status !== 401) {
    return result;
  }

  try {
    const session = await authSession.refresh();
    sessionInvalidated = false;
    api.dispatch(sessionAuthenticated(session.user));
    result = await rawBaseQuery(args, api);
  } catch {
    if (!sessionInvalidated) {
      sessionInvalidated = true;
      authSession.clear();
      api.dispatch(sessionUnauthenticated("登录状态已失效，请重新登录。"));
      api.dispatch(baseApi.util.resetApiState());
    }
  }

  return result;
}

/** Single RTK Query cache and reauthentication boundary for administrator data. */
export const baseApi = createApi({
  reducerPath: "api",
  baseQuery: baseQueryWithRefresh,
  tagTypes: [
    "AdminUser",
    "AdminArticle",
    "ArticleCategory",
    "Video",
    "VideoCategory",
  ],
  endpoints: () => ({}),
});

export function markApiSessionActive() {
  sessionInvalidated = false;
}

export function clearApiSession(dispatch, message = null) {
  sessionInvalidated = true;
  authSession.clear();
  dispatch(sessionUnauthenticated(message));
  dispatch(baseApi.util.resetApiState());
}
