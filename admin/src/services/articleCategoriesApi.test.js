import { describe, expect, it } from "vitest";
import { articleCategoriesApi } from "@/services/articleCategoriesApi.js";
import { normalizeArticleCategory } from "@/services/articleContracts.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { articleCategory, axiosResponse, mockHttpClient } from "@/test/http.js";

describe("articleCategoriesApi", () => {
  it("loads every administrator option page with includeInactive", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      const second = config.url.includes("page=2");
      return Promise.resolve(
        axiosResponse({
          items: [
            articleCategory(
              second
                ? {
                    id: "77777777-7777-4777-8777-777777777777",
                    name: "停用",
                    slug: "inactive",
                    isActive: false,
                  }
                : {},
            ),
          ],
          page: second ? 2 : 1,
          pageSize: 100,
          totalCount: 2,
          totalPages: 2,
        }),
      );
    });
    const store = createAppStore();
    const categories = await store
      .dispatch(
        articleCategoriesApi.endpoints.getAllArticleCategoryOptions.initiate(),
      )
      .unwrap();
    expect(categories).toHaveLength(2);
    expect(categories[0].createdAt).toBe("2026-07-31T08:00:00+00:00");
    expect(requestMock.mock.calls.map(([config]) => config.url)).toEqual([
      "/admin/article-categories?page=1&pageSize=100&includeInactive=true",
      "/admin/article-categories?page=2&pageSize=100&includeInactive=true",
    ]);
  });

  it("rejects an invalid category creation timestamp", () => {
    expect(() =>
      normalizeArticleCategory(articleCategory({ createdAt: "not-a-date" })),
    ).toThrow("API returned invalid createdAt.");
  });

  it("keeps clear and delete as separate endpoint calls", async () => {
    tokenVault.install("access", "refresh");
    const category = articleCategory();
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/articles")
            ? { categoryId: category.id, removedArticleCount: 3 }
            : undefined,
          config.url.endsWith("/articles") ? 200 : 204,
        ),
      ),
    );
    const store = createAppStore();
    await store
      .dispatch(
        articleCategoriesApi.endpoints.clearArticleCategory.initiate({
          categoryId: category.id,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        articleCategoriesApi.endpoints.deleteArticleCategory.initiate({
          categoryId: category.id,
        }),
      )
      .unwrap();
    expect(
      requestMock.mock.calls.map(([config]) => ({
        url: config.url,
        method: config.method,
      })),
    ).toEqual([
      {
        url: `/admin/article-categories/${category.id}/articles`,
        method: "DELETE",
      },
      { url: `/admin/article-categories/${category.id}`, method: "DELETE" },
    ]);
  });
});
