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
    await user.type(await screen.findByLabelText("词头 *"), "bonjour");
    await user.click(screen.getByRole("button", { name: "保存" }));

    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/words/${WORD_ID}`),
    );
    expect(requestMock.mock.calls[0][0]).toMatchObject({
      url: "/admin/words",
      method: "POST",
      data: {
        headword: "bonjour",
        senses: [],
        pronunciations: [],
      },
    });
  });

  it("keeps invalid nested content local and marks required fields", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");
    await user.type(await screen.findByLabelText("词头 *"), "bonjour");
    await user.click(screen.getByRole("button", { name: "添加释义" }));
    await user.click(screen.getByRole("button", { name: "保存" }));
    expect(screen.getByText("请先修正标记的字段。")).toBeVisible();
    expect(screen.getByText("请输入释义。")).toBeVisible();
  });

  it("collapses a completed sense and adds the next sense in place", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");

    await screen.findByLabelText("词头 *");
    await user.click(screen.getByRole("button", { name: "添加释义" }));
    await user.type(screen.getByLabelText("释义 *"), "第一条释义");
    await user.click(
      screen.getByRole("button", { name: "收起并添加下一条释义" }),
    );

    expect(screen.getByRole("button", { name: "展开释义 1" })).toBeVisible();
    expect(screen.getByText("第一条释义")).toBeVisible();
    expect(screen.getByRole("button", { name: "折叠释义 2" })).toBeVisible();
    expect(screen.getAllByLabelText("释义 *")).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: "展开释义 1" }));
    expect(screen.getAllByLabelText("释义 *")).toHaveLength(2);
  });

  it("collapses a completed example and adds the next example in place", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");

    await screen.findByLabelText("词头 *");
    await user.click(screen.getByRole("button", { name: "添加释义" }));
    await user.click(screen.getByRole("button", { name: "添加例句" }));
    await user.type(screen.getByLabelText("例句原文 *"), "第一条例句");
    await user.click(
      screen.getByRole("button", { name: "收起并添加下一条例句" }),
    );

    expect(screen.getByRole("button", { name: "展开例句 1" })).toBeVisible();
    expect(screen.getByText("第一条例句")).toBeVisible();
    expect(screen.getByRole("button", { name: "折叠例句 2" })).toBeVisible();
    expect(screen.getAllByLabelText("例句原文 *")).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: "展开例句 1" }));
    expect(screen.getAllByLabelText("例句原文 *")).toHaveLength(2);
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
          usageNote: null,
          sortOrder: 0,
          examples: [
            {
              id: "55555555-5555-4555-8555-555555555555",
              sentence: "Bonjour!",
              translation: "你好！",
              sortOrder: 0,
            },
          ],
        },
      ],
      pronunciations: [
        {
          id: "66666666-6666-4666-8666-666666666666",
          accentTag: "France",
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

  it("creates pronunciation text fields and a default selection", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          {
            ...createdWord(),
            pronunciations: [
              {
                id: "66666666-6666-4666-8666-666666666666",
                accentTag: "France",
                ipa: "bɔ̃.ʒuʁ",
                isDefault: true,
                sortOrder: 0,
              },
            ],
          },
          201,
        ),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/words/new");
    await user.type(await screen.findByLabelText("词头 *"), "bonjour");
    await user.click(screen.getByRole("button", { name: "添加发音" }));
    await user.type(screen.getByLabelText("口音标签"), "France");
    await user.type(screen.getByLabelText("国际音标（IPA）"), "bɔ̃.ʒuʁ");
    await user.click(screen.getByLabelText("设为默认发音"));
    await user.click(screen.getByRole("button", { name: "保存" }));
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/words/${WORD_ID}`),
    );

    const createRequest = requestMock.mock.calls
      .map(([config]) => config)
      .find((config) => config.url === "/admin/words");
    expect(createRequest.data.pronunciations).toEqual([
      {
        accentTag: "France",
        ipa: "bɔ̃.ʒuʁ",
        isDefault: true,
        sortOrder: 0,
      },
    ]);
  });
});
