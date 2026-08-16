import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const WORD_ID = "11111111-1111-4111-8111-111111111111";

function wordListItem() {
  return {
    id: WORD_ID,
    headword: "bonjour",
    status: "Draft",
    primaryPartOfSpeech: "Interjection",
    primaryDefinition: "你好",
    senseCount: 1,
    exampleCount: 2,
    pronunciationCount: 1,
    createdBy: {
      id: "22222222-2222-4222-8222-222222222222",
      nickname: "管理员",
      avatarUrl: null,
    },
    lastEditor: {
      id: "22222222-2222-4222-8222-222222222222",
      nickname: "管理员",
      avatarUrl: null,
    },
    publishedAt: null,
    archivedAt: null,
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T11:00:00Z",
    concurrencyStamp: "33333333-3333-4333-8333-333333333333",
  };
}

describe("Words", () => {
  it("renders the server list and resets applied URL filters", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [wordListItem()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/words", {
      initialEntry: "/words?status=Draft&language=fr",
    });

    expect(
      await screen.findByRole("heading", { level: 1, name: "单词管理" }),
    ).toBeVisible();
    expect(await screen.findByRole("link", { name: "bonjour" })).toBeVisible();
    expect(screen.getByText("你好")).toBeVisible();
    expect(screen.queryByRole("link", { name: "批量录入" })).toBeNull();
    await user.click(screen.getByRole("button", { name: "重置筛选" }));
    await waitFor(() => expect(router.state.location.search).toBe(""));
  });

  it("shows a filtered empty state separately", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0,
        }),
      ),
    );
    renderAppAt("/words", { initialEntry: "/words?keyword=missing" });
    expect(await screen.findByText("没有符合条件的单词")).toBeVisible();
    expect(screen.getByRole("button", { name: "清除筛选" })).toBeVisible();
  });
});
