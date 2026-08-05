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

    expect(mapped.message).toBe("请求失败，请稍后重试。");
    expect(mapped.fieldErrors).toEqual({
      Email: "请输入有效的邮箱地址。",
      Bio: "简介不能超过 500 个字符。",
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
});
