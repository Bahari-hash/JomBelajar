import { CanceledError } from "axios";
import { describe, expect, it, vi } from "vitest";
import { authSession } from "@/services/authSession.js";
import { tokenVault } from "@/services/tokenVault.js";
import {
  adminTokenResponse,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";

describe("authSession", () => {
  it("installs an admin session without involving Redux", async () => {
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse(adminTokenResponse())),
    );

    const session = await authSession.login({
      email: "admin@example.test",
      password: "secret-password",
    });

    expect(session.user).toEqual({
      id: "11111111-1111-1111-1111-111111111111",
      email: "admin@example.test",
      role: "Admin",
    });
    expect(tokenVault.getAccessToken()).toBe("access-token");
    const [config] = requestMock.mock.calls[0];
    expect(config.method).toBe("POST");
    expect(config.data).toEqual({
      email: "admin@example.test",
      password: "secret-password",
    });
  });

  it("best-effort logs out a non-admin response and rejects access", async () => {
    const nonAdminResponse = adminTokenResponse({
      user: {
        id: "33333333-3333-3333-3333-333333333333",
        email: "user@example.test",
        role: "User",
      },
    });
    const requestMock = mockHttpClient(vi.fn());
    requestMock
      .mockResolvedValueOnce(axiosResponse(nonAdminResponse))
      .mockResolvedValueOnce(axiosResponse(undefined, 204));

    await expect(
      authSession.login({
        email: "user@example.test",
        password: "secret-password",
      }),
    ).rejects.toMatchObject({ status: 403 });

    expect(requestMock).toHaveBeenCalledTimes(2);
    expect(requestMock.mock.calls[1][0]).toMatchObject({
      url: "/auth/logout",
      headers: { Authorization: "Bearer access-token" },
    });
    expect(tokenVault.getAccessToken()).toBeNull();
  });

  it("shares one rotating refresh request across concurrent callers", async () => {
    tokenVault.install("old-access");
    let resolveRefresh;
    const requestMock = mockHttpClient(
      () =>
        new Promise((resolve) => {
          resolveRefresh = () => resolve(axiosResponse(adminTokenResponse()));
        }),
    );

    const first = authSession.refresh();
    const second = authSession.refresh();
    expect(requestMock).toHaveBeenCalledTimes(1);
    resolveRefresh();

    await expect(Promise.all([first, second])).resolves.toHaveLength(2);
    expect(tokenVault.getAccessToken()).toBe("access-token");
    expect(requestMock.mock.calls[0][0].data).toEqual({});
  });

  it("clears the local session when remote logout times out", async () => {
    vi.useFakeTimers();
    tokenVault.install("access-token");
    mockHttpClient(
      (config) =>
        new Promise((_resolve, reject) => {
          config.signal.addEventListener("abort", () => {
            reject(new CanceledError());
          });
        }),
    );

    const logoutExpectation = expect(
      authSession.logout(),
    ).resolves.toBeUndefined();
    await vi.advanceTimersByTimeAsync(5000);

    await logoutExpectation;
    expect(tokenVault.getAccessToken()).toBeNull();
    vi.useRealTimers();
  });
});
