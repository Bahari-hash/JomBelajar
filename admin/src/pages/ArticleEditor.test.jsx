import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { adminArticle, axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";
import { tokenVault } from "@/services/tokenVault.js";

describe("ArticleEditor", () => {
  it("creates a draft with the exact payload and replaces the route", async () => {
    tokenVault.clear();
    const user = userEvent.setup();
    const saved = adminArticle({
      title: "新文章",
      contentMarkdown: "# 正文",
      contentHtml: "<h1>正文</h1>",
    });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("article-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.url === "/admin/articles" && config.method === "POST")
        return Promise.resolve(axiosResponse(saved, 201));
      return Promise.resolve(axiosResponse(saved));
    });
    const { router } = renderAppAt("/articles/new");

    await user.type(
      await screen.findByLabelText(/^标题/, {}, { timeout: 3000 }),
      "新文章",
    );
    await user.type(screen.getByLabelText(/^Markdown 正文/), "# 正文");
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);

    await expect
      .poll(() => router.state.location.pathname)
      .toBe(`/articles/${saved.id}/edit`);
    const createCall = requestMock.mock.calls.find(
      ([config]) =>
        config.url === "/admin/articles" && config.method === "POST",
    );
    expect(createCall[0].data).toEqual({
      title: "新文章",
      summary: null,
      contentMarkdown: "# 正文",
      categoryIds: [],
      coverMediaResourceId: null,
      bodyMediaResourceIds: [],
    });
  });
});
