import axios from "axios";

const DEFAULT_ERROR_MESSAGE = "请求暂时无法完成，请稍后重试。";

/** Safe client error shape shared by auth transport and RTK Query. */
export class ApiError extends Error {
  constructor(message, options = {}) {
    super(message);
    this.name = "ApiError";
    this.status = options.status ?? "FETCH_ERROR";
    this.detail = options.detail ?? message;
    this.errorCode = options.errorCode ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
    this.kind = options.kind ?? "request";
  }
}

export function normalizeFieldErrors(errors) {
  if (!errors || typeof errors !== "object" || Array.isArray(errors)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(errors).flatMap(([field, messages]) => {
      if (!Array.isArray(messages)) {
        return [];
      }

      const safeMessages = messages.filter((message) => typeof message === "string");
      return safeMessages.length > 0 ? [[field.toLowerCase(), safeMessages]] : [];
    }),
  );
}

function createResponseError(response) {
  const problem =
    response?.data && typeof response.data === "object" ? response.data : undefined;
  const detail =
    problem && typeof problem.detail === "string"
      ? problem.detail
      : DEFAULT_ERROR_MESSAGE;
  const errorCode =
    problem &&
    typeof problem.errorCode === "string" &&
    problem.errorCode.length > 0 &&
    problem.errorCode.trim() === problem.errorCode
      ? problem.errorCode
      : null;

  return new ApiError(detail, {
    status: response.status,
    detail,
    errorCode,
    fieldErrors: normalizeFieldErrors(problem?.errors),
    kind: "http",
  });
}

export function toApiError(error) {
  if (error instanceof ApiError) {
    return error;
  }

  if (axios.isCancel(error) || error?.name === "AbortError") {
    return new ApiError("请求已取消。", {
      status: "FETCH_ERROR",
      kind: "aborted",
    });
  }

  if (axios.isAxiosError(error) && error.response) {
    return createResponseError(error.response);
  }

  return new ApiError(DEFAULT_ERROR_MESSAGE, {
    status: "FETCH_ERROR",
    kind: "network",
  });
}

export function toRtkQueryError(error) {
  const apiError = toApiError(error);
  return {
    status: apiError.status,
    detail: apiError.detail,
    errorCode: apiError.errorCode,
    fieldErrors: apiError.fieldErrors,
    kind: apiError.kind,
  };
}

export function getErrorMessage(error, fallback = DEFAULT_ERROR_MESSAGE) {
  return typeof error?.detail === "string" ? error.detail : fallback;
}
