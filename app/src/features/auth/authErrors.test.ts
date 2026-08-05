import axios, { AxiosHeaders } from "axios";
import { describe, expect, it } from "vitest";
import { toApiRequestError } from "@/features/auth/authErrors";

describe("auth error mapping", () => {
  it("maps stable Problem Details codes and field messages", () => {
    const error = new axios.AxiosError(
      "bad request",
      "ERR_BAD_REQUEST",
      undefined,
      undefined,
      {
        status: 400,
        statusText: "Bad Request",
        headers: {},
        config: { headers: new AxiosHeaders() },
        data: {
          errorCode: "RequestValidationFailed",
          errors: { Email: ["EmailFormatInvalid"], Bio: ["BioLengthLimit"] },
        },
      },
    );
    const mapped = toApiRequestError(error);

    expect(mapped.message).toBe("请检查表单中的填写内容。");
    expect(mapped.fieldErrors).toEqual({
      Email: "请输入有效的邮箱地址。",
      Bio: "简介不能超过 500 个字符。",
    });
  });

  it("keeps single field messages and resolves field names case-insensitively", () => {
    const error = new axios.AxiosError(
      "validation failed",
      "ERR_BAD_REQUEST",
      undefined,
      undefined,
      {
        status: 400,
        statusText: "Bad Request",
        headers: {},
        config: { headers: new AxiosHeaders() },
        data: {
          errorCode: "RequestValidationFailed",
          errors: { NewPassword: "密码至少需要 8 个字符。" },
        },
      },
    );

    const mapped = toApiRequestError(error);

    expect(mapped.message).toBe("请检查表单中的填写内容。");
    expect(mapped.fieldErrors).toEqual({
      NewPassword: "密码至少需要 8 个字符。",
    });
  });

  it("does not expose unknown response detail or non-Axios errors", () => {
    const error = new axios.AxiosError(
      "internal stack",
      "ERR_BAD_REQUEST",
      undefined,
      undefined,
      {
        status: 500,
        statusText: "Server Error",
        headers: {},
        config: { headers: new AxiosHeaders() },
        data: { errorCode: "UnexpectedError", detail: "database secret" },
      },
    );
    expect(toApiRequestError(error, "请重试").message).toBe("请重试");
    expect(
      toApiRequestError(new Error("password=secret"), "通用错误").message,
    ).toBe("通用错误");
  });

  it("maps rate limits to an actionable retry message", () => {
    const error = new axios.AxiosError(
      "too many requests",
      "ERR_BAD_REQUEST",
      undefined,
      undefined,
      {
        status: 429,
        statusText: "Too Many Requests",
        headers: { "retry-after": "45" },
        config: { headers: new AxiosHeaders() },
        data: { code: 429, retryAfter: 32 },
      },
    );

    const mapped = toApiRequestError(error);

    expect(mapped.status).toBe(429);
    expect(mapped.retryAfterSeconds).toBe(32);
    expect(mapped.message).toBe("请求过于频繁，请在 32 秒后重试。");
  });

  it("uses Retry-After when a rate-limit body has no retry duration", () => {
    const error = new axios.AxiosError(
      "too many requests",
      "ERR_BAD_REQUEST",
      undefined,
      undefined,
      {
        status: 429,
        statusText: "Too Many Requests",
        headers: { "retry-after": "18" },
        config: { headers: new AxiosHeaders() },
        data: { code: 429 },
      },
    );

    expect(toApiRequestError(error).message).toBe(
      "请求过于频繁，请在 18 秒后重试。",
    );
  });

  it("distinguishes timeouts and unavailable networks", () => {
    const timeout = new axios.AxiosError("timeout", "ECONNABORTED");
    const network = new axios.AxiosError("network", "ERR_NETWORK");

    expect(toApiRequestError(timeout).message).toBe(
      "请求超时，请检查网络连接后重试。",
    );
    expect(toApiRequestError(network).message).toBe(
      "网络连接失败，请检查网络后重试。",
    );
  });
});
