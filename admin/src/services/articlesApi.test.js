import { describe, expect, it } from "vitest";
import { articlesApi } from "@/services/articlesApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import {
  articleListItem,
  axiosResponse,
  adminArticle,
  mockHttpClient,
} from "@/test/http.js";

describe("articlesApi", () => {
  it("encodes list filters and normalizes strict string-enum data", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [articleListItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      ),
    );
    const store = createAppStore();
    const request = store.dispatch(
      articlesApi.endpoints.getAdminArticles.initiate({
        page: 2,
        pageSize: 20,
        keyword: "grammar",
        categoryId: "44444444-4444-4444-8444-444444444444",
        status: "Draft",
      }),
    );
    await expect(request.unwrap()).resolves.toMatchObject({
      items: [{ status: "Draft" }],
      totalPages: 2,
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/articles?page=2&pageSize=20&keyword=grammar&categoryId=44444444-4444-4444-8444-444444444444&status=Draft",
    );
    request.unsubscribe();
  });

  it("uses exact create, update, preview and state mutation contracts", async () => {
    tokenVault.install("access", "refresh");
    const response = adminArticle();
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/preview")
            ? { contentHtml: "<h1>Preview</h1>" }
            : response,
          config.method === "DELETE"
            ? 204
            : config.method === "POST" && config.url === "/admin/articles"
              ? 201
              : 200,
        ),
      ),
    );
    const store = createAppStore();
    const articleId = response.id;
    const body = {
      title: "Title",
      summary: null,
      contentMarkdown: "# Body",
      categoryIds: [],
      coverMediaResourceId: null,
      bodyMediaResourceIds: [],
    };
    await store
      .dispatch(articlesApi.endpoints.createArticle.initiate(body))
      .unwrap();
    await store
      .dispatch(
        articlesApi.endpoints.updateArticle.initiate({
          articleId,
          ...body,
          concurrencyStamp: response.concurrencyStamp,
        }),
      )
      .unwrap();
    await store
      .dispatch(articlesApi.endpoints.previewArticle.initiate("# Preview"))
      .unwrap();
    await store
      .dispatch(articlesApi.endpoints.publishArticle.initiate({ articleId }))
      .unwrap();
    await store
      .dispatch(articlesApi.endpoints.unpublishArticle.initiate({ articleId }))
      .unwrap();
    await store
      .dispatch(articlesApi.endpoints.archiveArticle.initiate({ articleId }))
      .unwrap();
    expect(
      requestMock.mock.calls.map(([config]) => ({
        url: config.url,
        method: config.method,
        data: config.data,
      })),
    ).toEqual([
      { url: "/admin/articles", method: "POST", data: body },
      {
        url: `/admin/articles/${articleId}`,
        method: "PUT",
        data: { ...body, concurrencyStamp: response.concurrencyStamp },
      },
      {
        url: "/admin/articles/preview",
        method: "POST",
        data: { contentMarkdown: "# Preview" },
      },
      {
        url: `/admin/articles/${articleId}/publish`,
        method: "POST",
        data: undefined,
      },
      {
        url: `/admin/articles/${articleId}/unpublish`,
        method: "POST",
        data: undefined,
      },
      {
        url: `/admin/articles/${articleId}`,
        method: "DELETE",
        data: undefined,
      },
    ]);
  });
});
