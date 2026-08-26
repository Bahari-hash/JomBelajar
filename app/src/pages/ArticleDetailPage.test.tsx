import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import ArticleDetailPage from "@/pages/ArticleDetailPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const ARTICLE_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const CATEGORY_ID = "11111111-2222-3333-4444-555555555555";
const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function renderDetail(path: string) {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/articles/:articleId" element={<ArticleDetailPage />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

describe("ArticleDetailPage", () => {
  it("does not request invalid identifiers", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = async (config) => {
      requests.push(config);
      throw new Error("should not request");
    };
    renderDetail("/articles/not-a-guid");

    expect(
      screen.getByRole("heading", { name: "文章不存在或已下架" }),
    ).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it("renders public article fields and safe content", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    const adapter: AxiosAdapter = async (config) => ({
      data: {
        id: ARTICLE_ID,
        title: "Learning in context",
        summary: "A practical lesson.",
        contentHtml:
          '<p>Safe lesson</p><script>steal()</script><a href="https://example.test">Source</a>',
        categories: [{ id: CATEGORY_ID, name: "Grammar", slug: "grammar" }],
        author: { id: "author-1", nickname: null, avatarUrl: null },
        publishedAt: "2026-08-05T01:00:00Z",
        coverUrl: null,
        createdAt: "2026-08-04T01:00:00Z",
        updatedAt: "2026-08-05T01:00:00Z",
        readingAudioResourceId: AUDIO_ID,
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    });
    httpClient.defaults.adapter = async (config) => {
      requests.push(config);
      return adapter(config);
    };
    renderDetail(`/articles/${ARTICLE_ID}`);

    expect(
      await screen.findByRole("heading", { name: "Learning in context" }),
    ).toBeInTheDocument();
    const articleCard = screen.getByRole("article");
    expect(screen.getByText("JomBelajar 编辑")).toBeInTheDocument();
    expect(
      within(articleCard).getByRole("heading", { name: "Learning in context" }),
    ).toBeInTheDocument();
    expect(within(articleCard).getByText("Safe lesson")).toBeInTheDocument();
    expect(articleCard).not.toContainElement(
      screen.getByRole("link", { name: "返回文章列表" }),
    );
    expect(screen.queryByText("steal()")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Grammar" })).toHaveAttribute(
      "href",
      `/articles?categoryId=${CATEGORY_ID}&page=1`,
    );
    expect(
      screen.getByRole("button", { name: "播放文章朗读" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("slider", { name: "文章朗读进度" }),
    ).toBeInTheDocument();
    expect(requests).toHaveLength(1);
    expect(document.title).toBe("Learning in context | JomBelajar");
  });

  it("does not render an empty audio placeholder without an association", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: {
        id: ARTICLE_ID,
        title: "Silent article",
        summary: null,
        contentHtml: "<p>Text only</p>",
        categories: [],
        author: { id: "author-1", nickname: "Editor", avatarUrl: null },
        publishedAt: "2026-08-05T01:00:00Z",
        coverUrl: null,
        createdAt: "2026-08-04T01:00:00Z",
        updatedAt: "2026-08-05T01:00:00Z",
        readingAudioResourceId: null,
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    renderDetail(`/articles/${ARTICLE_ID}`);

    await screen.findByRole("heading", { name: "Silent article" });
    expect(screen.queryByRole("button", { name: /朗读/ })).toBeNull();
  });
});
