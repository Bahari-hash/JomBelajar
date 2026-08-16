import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { articleApi } from "@/features/articles/articleApi";
import { setSession } from "@/features/auth/sessionStore";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function responseAdapter(
  handler: (config: InternalAxiosRequestConfig) => unknown,
): AxiosAdapter {
  return async (config) => ({
    data: handler(config),
    status: 200,
    statusText: "OK",
    headers: new AxiosHeaders(),
    config,
  });
}

describe("articleApi", () => {
  it("uses authenticated Axios contracts for article discovery", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = responseAdapter((config) => {
      requests.push(config);
      if (config.url === "/articles/article-1") {
        return {
          id: "article-1",
          readingAudioResourceId: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
        };
      }
      return { items: [], page: 1, pageSize: 12, totalCount: 0, totalPages: 0 };
    });
    setSession({
      token: "sensitive-access-token",
      expiresIn: 300,
      user: { id: "user-1", email: "user@example.test", role: "User" },
    });
    const store = createAppStore();

    await store
      .dispatch(
        articleApi.endpoints.getArticles.initiate({
          page: 2,
          pageSize: 12,
          keyword: "grammar",
          categoryId: "category-1",
        }),
      )
      .unwrap();
    await store
      .dispatch(
        articleApi.endpoints.getArticleCategories.initiate({
          page: 1,
          pageSize: 100,
          keyword: "language",
        }),
      )
      .unwrap();
    const article = await store
      .dispatch(articleApi.endpoints.getArticle.initiate("article-1"))
      .unwrap();

    expect(article.readingAudioResourceId).toBe(
      "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    );
    expect(requests).toHaveLength(3);
    expect(requests[0]).toMatchObject({
      url: "/articles",
      method: "get",
      params: {
        page: 2,
        pageSize: 12,
        keyword: "grammar",
        categoryId: "category-1",
      },
    });
    expect(requests[1]).toMatchObject({
      url: "/article-categories",
      method: "get",
      params: { page: 1, pageSize: 100, keyword: "language" },
    });
    expect(requests[2]).toMatchObject({
      url: "/articles/article-1",
      method: "get",
    });
    expect(
      requests.every(
        (request) =>
          request.headers.Authorization === "Bearer sensitive-access-token",
      ),
    ).toBe(true);
  });

  it("maps Problem Details into a safe RTK Query error", async () => {
    httpClient.defaults.adapter = async (config) => {
      const response = {
        data: {
          errorCode: "ArticleNotFound",
          detail: "internal storage path",
        },
        status: 404,
        statusText: "Not Found",
        headers: new AxiosHeaders(),
        config,
      };
      throw new AxiosError(
        "internal request details",
        "ERR_BAD_REQUEST",
        config,
        undefined,
        response,
      );
    };
    const store = createAppStore();

    await expect(
      store
        .dispatch(
          articleApi.endpoints.getArticle.initiate(
            "11111111-2222-3333-4444-555555555555",
          ),
        )
        .unwrap(),
    ).rejects.toMatchObject({
      status: 404,
      code: "ArticleNotFound",
      message: "文章不存在或已下架。",
    });
  });
});
