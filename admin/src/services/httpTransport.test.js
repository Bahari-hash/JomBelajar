import { AxiosError, AxiosHeaders, CanceledError } from "axios";
import { describe, expect, it, vi } from "vitest";
import { httpClient, requestApi } from "@/services/httpTransport.js";

function axiosResponse(data, status = 200, contentType = "application/json") {
  return {
    data,
    status,
    statusText: status === 204 ? "No Content" : "OK",
    headers: new AxiosHeaders({ "Content-Type": contentType }),
    config: {},
  };
}

function axiosHttpError(data, status) {
  const response = axiosResponse(data, status);
  return new AxiosError(
    `Request failed with status code ${status}`,
    AxiosError.ERR_BAD_RESPONSE,
    {},
    null,
    response,
  );
}

describe("httpTransport", () => {
  it("maps the shared request contract to Axios config", async () => {
    const controller = new AbortController();
    const requestMock = vi
      .spyOn(httpClient, "request")
      .mockResolvedValue(axiosResponse({ ok: true }));

    await expect(
      requestApi({
        path: "/admin/users",
        method: "POST",
        body: { role: "Admin" },
        accessToken: "access-token",
        signal: controller.signal,
      }),
    ).resolves.toEqual({ ok: true });

    expect(httpClient.defaults.baseURL).toBe("/api");
    expect(httpClient.getUri({ url: "/auth/login" })).toBe("/api/auth/login");
    expect(requestMock).toHaveBeenCalledWith({
      url: "/admin/users",
      method: "POST",
      data: { role: "Admin" },
      headers: { Authorization: "Bearer access-token" },
      withCredentials: false,
      signal: controller.signal,
    });
  });

  it("returns undefined for 204 and non-JSON success responses", async () => {
    const requestMock = vi.spyOn(httpClient, "request");
    requestMock.mockResolvedValueOnce(axiosResponse(undefined, 204));
    requestMock.mockResolvedValueOnce(
      axiosResponse("plain text", 200, "text/plain"),
    );

    await expect(requestApi({ path: "/first" })).resolves.toBeUndefined();
    await expect(requestApi({ path: "/second" })).resolves.toBeUndefined();
  });

  it("normalizes Axios Problem Details and field errors", async () => {
    vi.spyOn(httpClient, "request").mockRejectedValue(
      axiosHttpError(
        {
          detail: "请求验证失败。",
          errorCode: "RequestValidationFailed",
          errors: {
            Email: ["邮箱格式无效。"],
            ContentMarkdown: ["文章正文不能为空。"],
          },
        },
        400,
      ),
    );

    await expect(requestApi({ path: "/auth/login" })).rejects.toMatchObject({
      status: 400,
      detail: "请求验证失败。",
      errorCode: "RequestValidationFailed",
      fieldErrors: {
        email: ["邮箱格式无效。"],
        contentMarkdown: ["文章正文不能为空。"],
      },
      kind: "http",
    });
  });

  it("keeps empty HTTP, network, and cancellation failures safe", async () => {
    const requestMock = vi.spyOn(httpClient, "request");
    requestMock.mockRejectedValueOnce(axiosHttpError(undefined, 403));
    requestMock.mockRejectedValueOnce(
      new AxiosError("Network Error", AxiosError.ERR_NETWORK),
    );
    requestMock.mockRejectedValueOnce(new CanceledError());

    await expect(requestApi({ path: "/forbidden" })).rejects.toMatchObject({
      status: 403,
      kind: "http",
    });
    await expect(requestApi({ path: "/network" })).rejects.toMatchObject({
      status: "FETCH_ERROR",
      kind: "network",
    });
    await expect(requestApi({ path: "/cancelled" })).rejects.toMatchObject({
      status: "FETCH_ERROR",
      kind: "aborted",
    });
  });
});
