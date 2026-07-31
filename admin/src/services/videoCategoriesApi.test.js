import { describe, expect, it } from "vitest";
import { videoCategoriesApi } from "@/services/videoCategoriesApi.js";
import { normalizeVideoCategory } from "@/services/videoContracts.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient, videoCategory } from "@/test/http.js";

describe("videoCategoriesApi", () => {
  it("uses administrator list and mutation contracts", async () => {
    tokenVault.install("access", "refresh");
    const category = videoCategory();
    const requestMock = mockHttpClient((config) => {
      if (config.method === "GET") {
        return Promise.resolve(
          axiosResponse({
            items: [category],
            page: 1,
            pageSize: 20,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      }
      if (config.url.endsWith("/videos")) {
        return Promise.resolve(
          axiosResponse({ categoryId: category.id, removedVideoCount: 1 }),
        );
      }
      return Promise.resolve(
        axiosResponse(
          config.method === "DELETE" ? undefined : category,
          config.method === "DELETE" ? 204 : 200,
        ),
      );
    });
    const store = createAppStore();
    const result = await store
      .dispatch(
        videoCategoriesApi.endpoints.getVideoCategories.initiate({
          page: 1,
          pageSize: 20,
          keyword: "听力",
          includeInactive: true,
        }),
      )
      .unwrap();
    expect(result.items[0].createdAt).toBe("2026-07-31T08:00:00+00:00");
    await store
      .dispatch(
        videoCategoriesApi.endpoints.createVideoCategory.initiate({
          name: "听力",
          slug: "listening",
          description: null,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoCategoriesApi.endpoints.updateVideoCategory.initiate({
          categoryId: category.id,
          name: "听力",
          slug: "listening",
          description: null,
          isActive: false,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoCategoriesApi.endpoints.clearVideoCategory.initiate({
          categoryId: category.id,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoCategoriesApi.endpoints.deleteVideoCategory.initiate({
          categoryId: category.id,
        }),
      )
      .unwrap();
    expect(
      requestMock.mock.calls
        .filter(([config], index) => index === 0 || config.method !== "GET")
        .map(([config]) => ({
          url: config.url,
          method: config.method,
          data: config.data,
        })),
    ).toEqual([
      {
        url: "/admin/video-categories?page=1&pageSize=20&includeInactive=true&keyword=%E5%90%AC%E5%8A%9B",
        method: "GET",
        data: undefined,
      },
      {
        url: "/admin/video-categories",
        method: "POST",
        data: { name: "听力", slug: "listening", description: null },
      },
      {
        url: `/admin/video-categories/${category.id}`,
        method: "PUT",
        data: {
          name: "听力",
          slug: "listening",
          description: null,
          isActive: false,
        },
      },
      {
        url: `/admin/video-categories/${category.id}/videos`,
        method: "DELETE",
        data: undefined,
      },
      {
        url: `/admin/video-categories/${category.id}`,
        method: "DELETE",
        data: undefined,
      },
    ]);
  });

  it("rejects an invalid category creation timestamp", () => {
    expect(() =>
      normalizeVideoCategory(videoCategory({ createdAt: "not-a-date" })),
    ).toThrow("API returned invalid video category createdAt.");
  });
});
