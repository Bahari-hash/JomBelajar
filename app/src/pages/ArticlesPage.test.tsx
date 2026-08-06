import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import ArticlesPage from "@/pages/ArticlesPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const CATEGORY_ID = "11111111-2222-3333-4444-555555555555";
const ARTICLE_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function installArticleAdapter(requests: InternalAxiosRequestConfig[]) {
  const adapter: AxiosAdapter = async (config) => {
    requests.push(config);
    const data =
      config.url === "/article-categories"
        ? {
            items: [
              {
                id: CATEGORY_ID,
                name: "Grammar",
                slug: "grammar",
                description: null,
                isActive: true,
                articleCount: 2,
                createdAt: "2026-08-01T00:00:00Z",
              },
            ],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }
        : {
            items: [
              {
                id: ARTICLE_ID,
                title: "A useful grammar lesson",
                summary: "Learn grammar in context.",
                status: "Published",
                categories: [
                  { id: CATEGORY_ID, name: "Grammar", slug: "grammar" },
                ],
                coverUrl: null,
                author: { id: "author-1", nickname: null, avatarUrl: null },
                publishedAt: "2026-08-06T01:00:00Z",
                updatedAt: "2026-08-06T01:00:00Z",
              },
            ],
            page: 1,
            pageSize: 12,
            totalCount: 60,
            totalPages: 5,
          };
    return {
      data,
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    };
  };
  httpClient.defaults.adapter = adapter;
}

function renderPage(path = "/articles") {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[path]}>
        <LocationProbe />
        <ArticlesPage />
      </MemoryRouter>
    </Provider>,
  );
}

function LocationProbe() {
  const location = useLocation();
  return (
    <output data-testid="location">{`${location.pathname}${location.search}`}</output>
  );
}

describe("ArticlesPage", () => {
  it("requests only after search submission and resets page", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installArticleAdapter(requests);
    renderPage("/articles?page=4");

    expect(
      await screen.findByRole("heading", { name: "A useful grammar lesson" }),
    ).toBeInTheDocument();
    const articleRequestsBeforeTyping = requests.filter(
      (request) => request.url === "/articles",
    ).length;
    const search = screen.getByRole("searchbox", { name: "搜索文章" });
    await user.type(search, "grammar");
    expect(
      requests.filter((request) => request.url === "/articles"),
    ).toHaveLength(articleRequestsBeforeTyping);

    await user.click(screen.getByRole("button", { name: "搜索" }));
    await waitFor(() => {
      const latestRequest = requests
        .filter((request) => request.url === "/articles")
        .at(-1);
      expect(latestRequest?.params).toMatchObject({
        page: 1,
        keyword: "grammar",
      });
    });
  });

  it("applies and clears category filters using backend IDs", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installArticleAdapter(requests);
    renderPage();

    const category = await screen.findByRole("button", { name: /Grammar/ });
    await user.click(category);
    await waitFor(() => {
      const latestRequest = requests
        .filter((request) => request.url === "/articles")
        .at(-1);
      expect(latestRequest?.params).toMatchObject({
        page: 1,
        categoryId: CATEGORY_ID,
      });
    });

    await user.click(screen.getByRole("button", { name: "全部文章" }));
    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/articles");
      expect(screen.getByRole("button", { name: "全部文章" })).toHaveAttribute(
        "aria-pressed",
        "true",
      );
    });
  });

  it("normalizes invalid URL state before querying", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installArticleAdapter(requests);
    renderPage("/articles?page=-2&categoryId=invalid&keyword=%20%20");

    await screen.findByRole("heading", { name: "A useful grammar lesson" });
    const articleRequest = requests.find(
      (request) => request.url === "/articles",
    );
    expect(articleRequest?.params).toMatchObject({ page: 1, pageSize: 12 });
    expect(articleRequest?.params.keyword).toBeUndefined();
    expect(articleRequest?.params.categoryId).toBeUndefined();
  });
});
