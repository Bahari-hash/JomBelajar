import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const WORD_ID = "11111111-1111-4111-8111-111111111111";
const CONCURRENCY_STAMP = "33333333-3333-4333-8333-333333333333";

function wordListItem(overrides = {}) {
  return {
    id: WORD_ID,
    headword: "bonjour",
    primaryPartOfSpeech: "Interjection",
    primaryDefinition: "你好",
    senseCount: 1,
    exampleCount: 2,
    hasAudio: true,
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T11:00:00Z",
    concurrencyStamp: CONCURRENCY_STAMP,
    ...overrides,
  };
}

function wordPage(items = [wordListItem()]) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: items.length ? 1 : 0,
  };
}

describe("Words", () => {
  it("renders the new list contract and only exposes edit and delete actions", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(wordPage())));
    const user = userEvent.setup();
    const { router } = renderAppAt("/words", {
      initialEntry: "/words?status=Draft&language=fr",
    });

    expect(
      await screen.findByRole("heading", { level: 1, name: "单词管理" }),
    ).toBeVisible();
    const wordLink = await screen.findByRole("link", { name: "bonjour" });
    const row = wordLink.closest("tr");
    expect(row).not.toBeNull();
    expect(within(row).getByText("你好")).toBeVisible();
    expect(within(row).getByText("已关联")).toBeVisible();
    expect(row).not.toHaveTextContent("草稿");
    await waitFor(() => expect(router.state.location.search).toBe(""));
    expect(screen.getByRole("link", { name: "批量导入" })).toHaveAttribute(
      "href",
      "/words/batch",
    );

    await user.click(
      within(row).getByRole("button", { name: "管理单词 bonjour" }),
    );
    expect(screen.getByRole("menuitem", { name: "编辑" })).toBeVisible();
    expect(screen.getByRole("menuitem", { name: "永久删除" })).toBeVisible();
    for (const label of ["发布", "下架", "归档"])
      expect(screen.queryByRole("menuitem", { name: label })).toBeNull();
  });

  it("shows a navigation notice once and clears history state", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(wordPage([]))));
    const { router } = renderAppAt("/words", {
      initialEntry: {
        pathname: "/words",
        state: { notice: "已批量创建 2 个单词。" },
      },
    });

    expect(await screen.findByText("已批量创建 2 个单词。")).toBeVisible();
    await waitFor(() => expect(router.state.location.state).toBeNull());
  });

  it("deletes a word with its concurrency stamp", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      config.method === "DELETE"
        ? Promise.resolve(axiosResponse(undefined, 204))
        : Promise.resolve(axiosResponse(wordPage())),
    );
    const user = userEvent.setup();
    renderAppAt("/words");
    const wordLink = await screen.findByRole("link", { name: "bonjour" });
    await user.click(
      within(wordLink.closest("tr")).getByRole("button", {
        name: "管理单词 bonjour",
      }),
    );
    await user.click(screen.getByRole("menuitem", { name: "永久删除" }));
    expect(screen.getByRole("heading", { name: "删除单词" })).toBeVisible();
    await user.click(screen.getByRole("button", { name: "确认删除" }));

    await waitFor(() =>
      expect(
        requestMock.mock.calls.some(
          ([config]) =>
            config.url === `/admin/words/${WORD_ID}` &&
            config.method === "DELETE" &&
            config.data.concurrencyStamp === CONCURRENCY_STAMP,
        ),
      ).toBe(true),
    );
    expect(await screen.findByText("单词已删除")).toBeVisible();
  });

  it("shows a filtered empty state separately", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(wordPage([]))));
    renderAppAt("/words", { initialEntry: "/words?keyword=missing" });
    expect(await screen.findByText("没有符合条件的单词")).toBeVisible();
    expect(screen.getByRole("button", { name: "清除筛选" })).toBeVisible();
  });
});
