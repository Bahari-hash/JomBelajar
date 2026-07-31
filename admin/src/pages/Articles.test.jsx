import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import {
  articleCategory,
  articleListItem,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";
import { tokenVault } from "@/services/tokenVault.js";

describe("Articles", () => {
  it("restores URL filters and renders the real editor list response", async () => {
    tokenVault.clear();
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("pageSize=100"))
        return Promise.resolve(
          axiosResponse({
            items: [articleCategory()],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      return Promise.resolve(
        axiosResponse({
          items: [articleListItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      );
    });
    renderAppAt("/articles?page=2&keyword=grammar&status=Draft");

    expect(
      await screen.findByRole("heading", { level: 1, name: "文章管理" }),
    ).toBeVisible();
    const title = await screen.findByRole("link", {
      name: "Grammar essentials",
    });
    expect(within(title.closest("tr")).getByText("草稿")).toBeVisible();
    expect(screen.getByText("第 2 / 2 页")).toBeVisible();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url ===
          "/admin/articles?page=2&pageSize=20&keyword=grammar&status=Draft",
      ),
    ).toBe(true);
  });

  it("applies the selected category to the article list request", async () => {
    const user = userEvent.setup();
    const categoryId = "0198c8d0-1234-7abc-8def-0123456789ab";
    const requestMock = mockHttpClient((config) => {
      if (config.url.startsWith("/admin/article-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [articleCategory({ id: categoryId })],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      return Promise.resolve(
        axiosResponse({
          items: [articleListItem()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    renderAppAt("/articles");
    const category = await screen.findByRole("combobox", { name: "分类" });

    await user.click(category);
    await user.click(await screen.findByRole("option", { name: "语法" }));
    await user.click(screen.getByRole("button", { name: "应用" }));

    await waitFor(() =>
      expect(
        requestMock.mock.calls.some(
          ([config]) =>
            config.url ===
            `/admin/articles?page=1&pageSize=20&categoryId=${categoryId}`,
        ),
      ).toBe(true),
    );
  });
});
