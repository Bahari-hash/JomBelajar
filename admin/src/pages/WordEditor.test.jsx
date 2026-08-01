import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const WORD_ID = "11111111-1111-4111-8111-111111111111";

function createdWord() {
  const user = {
    id: "22222222-2222-4222-8222-222222222222",
    nickname: "管理员",
    avatarUrl: null,
  };
  return {
    id: WORD_ID,
    languageTag: "fr",
    headword: "bonjour",
    status: "Draft",
    createdBy: user,
    lastEditor: user,
    publishedAt: null,
    archivedAt: null,
    concurrencyStamp: "33333333-3333-4333-8333-333333333333",
    senses: [],
    pronunciations: [],
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T10:00:00Z",
  };
}

describe("WordEditor", () => {
  it("creates a draft with the exact aggregate request and replaces the route", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(createdWord(), config.method === "POST" ? 201 : 200),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/words/new");

    const heading = await screen.findByRole("heading", {
      level: 1,
      name: "新建单词",
    });
    const header = heading.closest("header");
    expect(within(header).getByRole("link", { name: "返回列表" })).toHaveClass(
      "border",
    );
    expect(within(header).getByRole("button", { name: "保存" })).toBeVisible();
    expect(screen.getAllByRole("button", { name: "保存" })).toHaveLength(1);
    await user.type(await screen.findByLabelText("语言标签 *"), "fr");
    await user.type(screen.getByLabelText("词头 *"), "bonjour");
    await user.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/words/${WORD_ID}`),
    );
    expect(requestMock.mock.calls[0][0]).toMatchObject({
      url: "/admin/words",
      method: "POST",
      data: {
        languageTag: "fr",
        headword: "bonjour",
        senses: [],
        pronunciations: [],
      },
    });
    expect(await screen.findByText("单词草稿已创建。")).toBeVisible();
  });

  it("keeps invalid nested content local and marks required fields", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");
    await user.type(await screen.findByLabelText("语言标签 *"), "fr");
    await user.type(screen.getByLabelText("词头 *"), "bonjour");
    await user.click(screen.getByRole("button", { name: "添加释义" }));
    await user.click(screen.getByRole("button", { name: "保存" }));
    expect(screen.getByText("请先修正标记的字段。")).toBeVisible();
    expect(screen.getByText("请输入释义。")).toBeVisible();
    expect(screen.getByText("请输入释义语言。")).toBeVisible();
  });

  it("preserves server child ids and concurrency stamp when updating", async () => {
    tokenVault.install("access", "refresh");
    const saved = {
      ...createdWord(),
      senses: [
        {
          id: "44444444-4444-4444-8444-444444444444",
          partOfSpeech: "Interjection",
          definition: "你好",
          definitionLanguageTag: "zh-CN",
          usageNote: null,
          sortOrder: 0,
          examples: [
            {
              id: "55555555-5555-4555-8555-555555555555",
              sentence: "Bonjour!",
              languageTag: "fr",
              translation: "你好！",
              translationLanguageTag: "zh-CN",
              audioClipId: null,
              sortOrder: 0,
            },
          ],
        },
      ],
      pronunciations: [
        {
          id: "66666666-6666-4666-8666-666666666666",
          audioClipId: "77777777-7777-4777-8777-777777777777",
          accentTag: null,
          ipa: "bɔ̃.ʒuʁ",
          isDefault: true,
          sortOrder: 0,
        },
      ],
    };
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.method === "PUT" ? { ...saved, headword: "salut" } : saved,
        ),
      ),
    );
    const user = userEvent.setup();
    renderAppAt(`/words/${WORD_ID}`);
    const headword = await screen.findByLabelText("词头 *");
    await user.clear(headword);
    await user.type(headword, "salut");
    await user.click(screen.getByRole("button", { name: "保存" }));
    await screen.findByText("单词修改已保存。");

    const update = requestMock.mock.calls
      .map(([config]) => config)
      .find((config) => config.method === "PUT");
    expect(update.data).toMatchObject({
      concurrencyStamp: saved.concurrencyStamp,
      senses: [
        {
          id: saved.senses[0].id,
          sortOrder: 0,
          examples: [{ id: saved.senses[0].examples[0].id, sortOrder: 0 }],
        },
      ],
      pronunciations: [{ id: saved.pronunciations[0].id, sortOrder: 0 }],
    });
  });
});
