import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { adminArticle, axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";
import { tokenVault } from "@/services/tokenVault.js";

const PROCESSING_AUDIO = {
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  name: "lesson.mp3",
  status: "Processing",
  durationSeconds: null,
  lastFailureCode: null,
};

const UPLOADING_AUDIO = {
  id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
  name: "uploaded.mp3",
  status: "Uploading",
  durationSeconds: null,
  lastFailureCode: null,
};

vi.mock("@/features/articles/ArticleReadingAudioControl.jsx", () => ({
  ArticleReadingAudioControl: ({ value, onChange, disabled }) => (
    <div aria-label="文章朗读音频">
      <p>{value ? `${value.name} ${value.status}` : "未关联朗读音频"}</p>
      <button
        type="button"
        disabled={disabled}
        onClick={() => onChange(PROCESSING_AUDIO)}
      >
        模拟选择已有音频
      </button>
      <button
        type="button"
        disabled={disabled}
        onClick={() => onChange(UPLOADING_AUDIO)}
      >
        模拟上传初始化
      </button>
      <button
        type="button"
        disabled={disabled || !value}
        onClick={() => onChange(null)}
      >
        模拟解除关联
      </button>
    </div>
  ),
}));

function categoriesResponse() {
  return {
    items: [],
    page: 1,
    pageSize: 100,
    totalCount: 0,
    totalPages: 0,
  };
}

async function fillRequiredFields(user) {
  await user.type(
    await screen.findByLabelText(/^标题/, {}, { timeout: 3000 }),
    "新文章",
  );
  await user.type(screen.getByLabelText(/^Markdown 正文/), "# 正文");
}

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
        return Promise.resolve(axiosResponse(categoriesResponse()));
      if (config.url === "/admin/articles" && config.method === "POST")
        return Promise.resolve(axiosResponse(saved, 201));
      return Promise.resolve(axiosResponse(saved));
    });
    const { router } = renderAppAt("/articles/new");

    await fillRequiredFields(user);
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
      readingAudioResourceId: null,
    });
  });

  it("saves a selected Processing audio without waiting for Ready", async () => {
    tokenVault.clear();
    const user = userEvent.setup();
    const saved = adminArticle({
      title: "新文章",
      contentMarkdown: "# 正文",
      contentHtml: "<h1>正文</h1>",
      readingAudio: PROCESSING_AUDIO,
    });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("article-categories"))
        return Promise.resolve(axiosResponse(categoriesResponse()));
      return Promise.resolve(
        axiosResponse(saved, config.method === "POST" ? 201 : 200),
      );
    });
    renderAppAt("/articles/new");

    await fillRequiredFields(user);
    await user.click(screen.getByRole("button", { name: "模拟选择已有音频" }));
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);

    await expect
      .poll(
        () =>
          requestMock.mock.calls.find(
            ([config]) =>
              config.url === "/admin/articles" && config.method === "POST",
          )?.[0].data.readingAudioResourceId,
      )
      .toBe(PROCESSING_AUDIO.id);
  });

  it("saves an Uploading audio immediately after upload initialization", async () => {
    tokenVault.clear();
    const user = userEvent.setup();
    const saved = adminArticle({
      title: "新文章",
      contentMarkdown: "# 正文",
      contentHtml: "<h1>正文</h1>",
      readingAudio: UPLOADING_AUDIO,
    });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("article-categories"))
        return Promise.resolve(axiosResponse(categoriesResponse()));
      return Promise.resolve(
        axiosResponse(saved, config.method === "POST" ? 201 : 200),
      );
    });
    renderAppAt("/articles/new");

    await fillRequiredFields(user);
    await user.click(screen.getByRole("button", { name: "模拟上传初始化" }));
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);

    await expect
      .poll(
        () =>
          requestMock.mock.calls.find(
            ([config]) =>
              config.url === "/admin/articles" && config.method === "POST",
          )?.[0].data.readingAudioResourceId,
      )
      .toBe(UPLOADING_AUDIO.id);
  });

  it("replaces and unlinks reading audio when editing a draft", async () => {
    tokenVault.clear();
    const user = userEvent.setup();
    const articleId = adminArticle().id;
    let current = adminArticle({ readingAudio: UPLOADING_AUDIO });
    const updateBodies = [];
    mockHttpClient((config) => {
      if (config.url.includes("article-categories"))
        return Promise.resolve(axiosResponse(categoriesResponse()));
      if (
        config.url === `/admin/articles/${articleId}` &&
        config.method === "PUT"
      ) {
        updateBodies.push(config.data);
        current = adminArticle({
          readingAudio:
            config.data.readingAudioResourceId === null
              ? null
              : PROCESSING_AUDIO,
        });
      }
      return Promise.resolve(axiosResponse(current));
    });
    renderAppAt(`/articles/${articleId}/edit`);

    await screen.findByText("uploaded.mp3 Uploading");
    await user.click(screen.getByRole("button", { name: "模拟选择已有音频" }));
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);
    await expect.poll(() => updateBodies.length).toBe(1);
    expect(updateBodies[0].readingAudioResourceId).toBe(PROCESSING_AUDIO.id);

    await screen.findByText("lesson.mp3 Processing");
    await user.click(screen.getByRole("button", { name: "模拟解除关联" }));
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);
    await expect.poll(() => updateBodies.length).toBe(2);
    expect(updateBodies[1].readingAudioResourceId).toBeNull();
  });

  it("publishes a draft while its reading audio is still Processing", async () => {
    tokenVault.clear();
    const user = userEvent.setup();
    const articleId = adminArticle().id;
    let current = adminArticle({ readingAudio: PROCESSING_AUDIO });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("article-categories"))
        return Promise.resolve(axiosResponse(categoriesResponse()));
      if (config.url === `/admin/articles/${articleId}/publish`) {
        current = adminArticle({
          status: "Published",
          publishedAt: "2026-08-16T09:00:00Z",
          readingAudio: PROCESSING_AUDIO,
        });
      }
      return Promise.resolve(axiosResponse(current));
    });
    renderAppAt(`/articles/${articleId}/edit`);

    await screen.findByText("lesson.mp3 Processing");
    await user.click(screen.getByRole("button", { name: "发布" }));
    await user.click(screen.getByRole("button", { name: "确认发布" }));

    await expect
      .poll(() =>
        requestMock.mock.calls.some(
          ([config]) =>
            config.url === `/admin/articles/${articleId}/publish` &&
            config.method === "POST",
        ),
      )
      .toBe(true);
  });
});
