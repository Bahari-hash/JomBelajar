import { describe, expect, it, vi } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { usersApi } from "@/services/usersApi.js";
import { createAppStore } from "@/store/index.js";
import {
  adminTokenResponse,
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";

const EMPTY_PAGE = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
};

describe("baseApi reauthentication", () => {
  it("uses one refresh for concurrent 401 responses and retries each request once", async () => {
    tokenVault.install("old-access", "old-refresh");
    let resolveRefresh;
    const requestMock = mockHttpClient((config) => {
      if (config.url === "/auth/refresh") {
        return new Promise((resolve) => {
          resolveRefresh = () => resolve(axiosResponse(adminTokenResponse()));
        });
      }
      if (config.headers?.Authorization === "Bearer old-access") {
        return Promise.reject(axiosHttpError(undefined, 401));
      }
      return Promise.resolve(axiosResponse(EMPTY_PAGE));
    });
    const store = createAppStore();

    const first = store.dispatch(
      usersApi.endpoints.getAdminUsers.initiate({ page: 1, pageSize: 20 }),
    );
    const second = store.dispatch(
      usersApi.endpoints.getAdminUsers.initiate({ page: 2, pageSize: 20 }),
    );

    await vi.waitFor(() => {
      expect(
        requestMock.mock.calls.filter(([config]) => config.url === "/auth/refresh"),
      ).toHaveLength(1);
    });
    resolveRefresh();
    await expect(Promise.all([first.unwrap(), second.unwrap()])).resolves.toHaveLength(2);

    expect(
      requestMock.mock.calls.filter(([config]) =>
        config.url.includes("/admin/users?"),
      ),
    ).toHaveLength(4);
    first.unsubscribe();
    second.unsubscribe();
  });

  it("returns 403 without attempting refresh", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.reject(axiosHttpError(undefined, 403)),
    );
    const store = createAppStore();

    const request = store.dispatch(
      usersApi.endpoints.getAdminUsers.initiate({ page: 1, pageSize: 20 }),
    );

    await expect(request.unwrap()).rejects.toMatchObject({ status: 403 });
    expect(requestMock).toHaveBeenCalledTimes(1);
    request.unsubscribe();
  });
});
