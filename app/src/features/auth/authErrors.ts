import axios from "axios";

export type FieldErrors = Record<string, string>;

const errorMessages: Record<string, string> = {
  RequestValidationFailed: "请检查表单中的填写内容。",
  EmailRequired: "请输入邮箱。",
  EmailFormatInvalid: "请输入有效的邮箱地址。",
  EmailLengthLimit: "邮箱长度不能超过 100 个字符。",
  EmailAlreadyExists: "该邮箱已被注册。",
  EmailUnchanged: "新邮箱不能与当前邮箱相同。",
  PasswordRequired: "请输入密码。",
  PasswordLengthMinimum: "密码至少需要 8 个字符。",
  PasswordLengthLimit: "密码不能超过 50 个字符。",
  VerificationCodeRequired: "请输入验证码。",
  VerificationCodeLengthLimit: "验证码必须为 6 位。",
  VerificationCodeFormatInvalid: "验证码必须是 6 位数字。",
  VerificationCodeInvalid: "验证码错误或已失效，请重新获取。",
  InvalidCredentials: "邮箱或密码错误。",
  RefreshTokenInvalid: "登录状态已失效，请重新登录。",
  TokenInvalid: "登录状态已失效，请重新登录。",
  UserNotFound: "当前用户资料不可用，请重新登录。",
  NicknameLengthLimit: "昵称不能超过 60 个字符。",
  BioLengthLimit: "简介不能超过 500 个字符。",
  AvatarUrlLengthLimit: "头像链接不能超过 500 个字符。",
  AvatarUrlFormatInvalid: "头像链接必须是 HTTP 或 HTTPS 地址。",
  ArticleNotFound: "文章不存在或已下架。",
  PageInvalid: "页码无效，请重新选择。",
  PageSizeInvalid: "分页数量无效，请重试。",
  KeywordLengthLimit: "搜索关键词不能超过 200 个字符。",
  KeywordInvalid: "搜索关键词包含无效字符。",
  ArticleCategoryInvalid: "文章分类无效，请重新选择。",
  VideoNotFound: "视频不存在或已下架。",
  VideoProgressInvalid: "播放进度无效，请重新加载视频后继续观看。",
  VideoCategoryIdsInvalid: "视频分类无效，请重新选择。",
};

export class ApiRequestError extends Error {
  readonly code: string | null;
  readonly status: number | null;
  readonly fieldErrors: FieldErrors;
  readonly retryAfterSeconds: number | null;

  constructor(
    message: string,
    options: {
      code?: string | null;
      status?: number | null;
      fieldErrors?: FieldErrors;
      retryAfterSeconds?: number | null;
    } = {},
  ) {
    super(message);
    this.name = "ApiRequestError";
    this.code = options.code ?? null;
    this.status = options.status ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
    this.retryAfterSeconds = options.retryAfterSeconds ?? null;
  }
}

function parseRetryAfterSeconds(value: unknown) {
  const seconds =
    typeof value === "number"
      ? value
      : typeof value === "string" && value.trim() !== ""
        ? Number(value)
        : Number.NaN;
  return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : null;
}

function mapFieldErrors(errors: unknown): FieldErrors {
  if (typeof errors !== "object" || errors === null) {
    return {};
  }

  return Object.entries(errors).reduce<FieldErrors>(
    (result, [field, value]) => {
      const messages = Array.isArray(value)
        ? value.filter((item): item is string => typeof item === "string")
        : typeof value === "string"
          ? [value]
          : [];
      if (messages.length > 0) {
        result[field] = messages
          .map((item) => errorMessages[item] ?? item)
          .join(" ");
      }
      return result;
    },
    {},
  );
}

/** Converts ASP.NET Problem Details into safe, user-facing error data. */
export function toApiRequestError(
  error: unknown,
  fallbackMessage = "请求失败，请稍后重试。",
) {
  if (error instanceof ApiRequestError) {
    return error;
  }

  if (!axios.isAxiosError(error)) {
    return new ApiRequestError(fallbackMessage);
  }

  const data: unknown = error.response?.data;
  const problem = typeof data === "object" && data !== null ? data : {};
  const code =
    "errorCode" in problem && typeof problem.errorCode === "string"
      ? problem.errorCode
      : null;
  const fieldErrors = "errors" in problem ? mapFieldErrors(problem.errors) : {};
  const status = error.response?.status ?? null;
  const responseRetryAfter =
    "retryAfter" in problem ? parseRetryAfterSeconds(problem.retryAfter) : null;
  const retryAfterSeconds =
    responseRetryAfter ??
    parseRetryAfterSeconds(
      error.response?.headers["retry-after"] ??
        error.response?.headers["Retry-After"],
    );
  let safeMessage = code
    ? (errorMessages[code] ?? fallbackMessage)
    : fallbackMessage;

  if (status === 429) {
    safeMessage = retryAfterSeconds
      ? `请求过于频繁，请在 ${retryAfterSeconds} 秒后重试。`
      : "请求过于频繁，请稍后重试。";
  } else if (error.code === "ECONNABORTED" || error.code === "ETIMEDOUT") {
    safeMessage = "请求超时，请检查网络连接后重试。";
  } else if (!error.response && error.code === "ERR_NETWORK") {
    safeMessage = "网络连接失败，请检查网络后重试。";
  }

  return new ApiRequestError(safeMessage, {
    code,
    status,
    fieldErrors,
    retryAfterSeconds,
  });
}

export function getErrorMessage(error: unknown, fallbackMessage?: string) {
  return toApiRequestError(error, fallbackMessage).message;
}

export function getFieldError(errors: FieldErrors, field: string) {
  const direct = errors[field];
  if (direct) {
    return direct;
  }

  const pascalCase = `${field[0]?.toUpperCase() ?? ""}${field.slice(1)}`;
  if (errors[pascalCase]) {
    return errors[pascalCase];
  }

  const normalizedField = field.toLowerCase();
  return Object.entries(errors).find(
    ([key, value]) => key.toLowerCase() === normalizedField && value,
  )?.[1];
}

/** Removes one field's stale error while preserving unrelated validation feedback. */
export function clearFieldError(errors: FieldErrors, field: string) {
  const normalizedField = field.toLowerCase();
  return Object.fromEntries(
    Object.entries(errors).filter(
      ([key]) => key.toLowerCase() !== normalizedField,
    ),
  );
}
