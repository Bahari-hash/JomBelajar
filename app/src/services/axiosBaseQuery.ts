import type { AxiosRequestConfig, Method } from "axios";
import type { BaseQueryFn } from "@reduxjs/toolkit/query";
import {
  toApiRequestError,
  type FieldErrors,
} from "@/features/auth/authErrors";
import { httpClient } from "@/services/httpClient";

export interface AxiosQueryArgs {
  url: string;
  method?: Method;
  params?: AxiosRequestConfig["params"];
  data?: AxiosRequestConfig["data"];
  skipAuth?: boolean;
}

export interface ApiQueryError {
  status: number | "FETCH_ERROR" | "CANCELLED";
  message: string;
  code: string | null;
  fieldErrors: FieldErrors;
  retryAfterSeconds: number | null;
}

/** Adapts the shared Axios client to RTK Query without creating another transport. */
export const axiosBaseQuery =
  (): BaseQueryFn<AxiosQueryArgs, unknown, ApiQueryError> =>
  async ({ url, method = "GET", params, data, skipAuth }, api) => {
    try {
      const response = await httpClient({
        url,
        method,
        params,
        data,
        skipAuth,
        signal: api.signal,
      });
      return { data: response.data };
    } catch (error) {
      const requestError = toApiRequestError(error);
      const cancelled = api.signal.aborted;
      return {
        error: {
          status: cancelled
            ? "CANCELLED"
            : (requestError.status ?? "FETCH_ERROR"),
          message: cancelled ? "请求已取消。" : requestError.message,
          code: requestError.code,
          fieldErrors: requestError.fieldErrors,
          retryAfterSeconds: requestError.retryAfterSeconds,
        },
      };
    }
  };
