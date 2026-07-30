import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { usersApi } from "@/services/usersApi.js";
import { createAppStore } from "@/store/index.js";
import {
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
  userListItem,
} from "@/test/http.js";

describe("usersApi mutations", () => {
  it("uses the exact server paths, methods, and request bodies", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse(undefined, 204)),
    );
    const store = createAppStore();
    const userId = "22222222-2222-2222-2222-222222222222";

    await store.dispatch(usersApi.endpoints.banUser.initiate({ userId, reason: "abuse" })).unwrap();
    await store.dispatch(usersApi.endpoints.unbanUser.initiate({ userId })).unwrap();
    requestMock.mockResolvedValueOnce(axiosResponse({ userId, role: "Admin" }));
    await store.dispatch(usersApi.endpoints.updateUserRole.initiate({ userId, role: "Admin" })).unwrap();
    await store.dispatch(usersApi.endpoints.revokeUserSessions.initiate({ userId })).unwrap();

    const calls = requestMock.mock.calls.map(([config]) => ({
      url: config.url,
      method: config.method,
      data: config.data,
    }));
    expect(calls).toEqual([
      { url: `/admin/users/${userId}/ban`, method: "POST", data: { reason: "abuse" } },
      { url: `/admin/users/${userId}/unban`, method: "POST", data: undefined },
      { url: `/admin/users/${userId}/role`, method: "POST", data: { role: "Admin" } },
      { url: `/auth/admin/users/${userId}/revoke`, method: "POST", data: undefined },
    ]);
  });

  it("keeps cached user data when a mutation fails", async () => {
    tokenVault.install("access", "refresh");
    let listRequests = 0;
    mockHttpClient((config) => {
      if (config.url.endsWith("/ban")) {
        return Promise.reject(
          axiosHttpError(
            {
              status: 400,
              detail: "封禁请求无效。",
              errorCode: "BanUserReasonRequired",
            },
            400,
          ),
        );
      }

      listRequests += 1;
      return Promise.resolve(
        axiosResponse({
          items: [userListItem()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    const store = createAppStore();
    const filters = { page: 1, pageSize: 20, keyword: "", role: "", status: "" };
    const subscription = store.dispatch(usersApi.endpoints.getAdminUsers.initiate(filters));
    await subscription.unwrap();

    await expect(
      store
        .dispatch(
          usersApi.endpoints.banUser.initiate({
            userId: "22222222-2222-2222-2222-222222222222",
            reason: "invalid",
          }),
        )
        .unwrap(),
    ).rejects.toMatchObject({
      status: 400,
      errorCode: "BanUserReasonRequired",
    });
    await new Promise((resolve) => window.setTimeout(resolve, 0));

    expect(listRequests).toBe(1);
    subscription.unsubscribe();
  });
});
