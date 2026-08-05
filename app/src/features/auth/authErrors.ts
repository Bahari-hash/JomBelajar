import axios from "axios";

export type FieldErrors = Record<string, string>;

const errorMessages: Record<string, string> = {
  EmailRequired: "请输入邮箱。",
  EmailFormatInvalid: "请输入有效的邮箱地址。",
  EmailLengthLimit: "邮箱长度不能超过 100 个字符。",
  EmailAlreadyExists: "该邮箱已被注册。",
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
};

export class ApiRequestError extends Error {
  readonly code: string | null;
  readonly status: number | null;
  readonly fieldErrors: FieldErrors;

  constructor(
    message: string,
    options: {
      code?: string | null;
      status?: number | null;
      fieldErrors?: FieldErrors;
    } = {},
  ) {
    super(message);
    this.name = "ApiRequestError";
    this.code = options.code ?? null;
    this.status = options.status ?? null;
    this.fieldErrors = options.fieldErrors ?? {};
  }
}

function mapFieldErrors(errors: unknown): FieldErrors {
  if (typeof errors !== "object" || errors === null) {
    return {};
  }

  return Object.entries(errors).reduce<FieldErrors>(
    (result, [field, value]) => {
      if (
        Array.isArray(value) &&
        value.every((item) => typeof item === "string")
      ) {
        result[field] = value
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
  const safeMessage = code
    ? (errorMessages[code] ?? fallbackMessage)
    : fallbackMessage;
  return new ApiRequestError(safeMessage, {
    code,
    status: error.response?.status ?? null,
    fieldErrors,
  });
}

export function getErrorMessage(error: unknown, fallbackMessage?: string) {
  return toApiRequestError(error, fallbackMessage).message;
}

export function getFieldError(errors: FieldErrors, field: string) {
  return (
    errors[field] ?? errors[`${field[0]?.toUpperCase() ?? ""}${field.slice(1)}`]
  );
}
