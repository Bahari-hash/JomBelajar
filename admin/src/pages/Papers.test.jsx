import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const PAPER_ID = "11111111-1111-4111-8111-111111111111";
const CATEGORY_ID = "77777777-7777-4777-8777-777777777777";

function paperListItem(overrides = {}) {
  const user = {
    id: "22222222-2222-4222-8222-222222222222",
    nickname: "管理员",
    avatarUrl: null,
  };
  return {
    id: PAPER_ID,
    title: "English basics",
    categories: [{ id: CATEGORY_ID, name: "听力", slug: "listening" }],
    status: "Draft",
    questionCount: 2,
    totalScore: 4,
    passingScore: 2,
    attemptCount: 0,
    createdBy: user,
    lastEditor: user,
    publishedAt: null,
    archivedAt: null,
    createdAt: "2026-08-02T10:00:00Z",
    updatedAt: "2026-08-02T11:00:00Z",
    concurrencyStamp: "33333333-3333-4333-8333-333333333333",
    ...overrides,
  };
}

describe("Papers", () => {
  it("renders the server list and resets applied URL filters", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [paperListItem()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/papers", {
      initialEntry: `/papers?status=Draft&categoryId=${CATEGORY_ID}`,
    });

    expect(
      await screen.findByRole("heading", { level: 1, name: "试卷管理" }),
    ).toBeVisible();
    expect(
      await screen.findByRole("link", { name: "English basics" }),
    ).toBeVisible();
    await user.click(screen.getByRole("button", { name: "重置筛选" }));
    await waitFor(() => expect(router.state.location.search).toBe(""));
  });

  it("applies a selected category only after submitting filter drafts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.startsWith("/admin/paper-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [
              {
                id: CATEGORY_ID,
                name: "听力",
                slug: "listening",
                description: null,
                isActive: true,
                paperCount: 8,
                createdAt: "2026-08-02T10:00:00Z",
              },
            ],
            page: 1,
            pageSize: 20,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      return Promise.resolve(
        axiosResponse({
          items: [paperListItem()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    const user = userEvent.setup();
    const { router } = renderAppAt("/papers");

    expect(
      await screen.findByRole("link", { name: "English basics" }),
    ).toBeVisible();
    await user.click(screen.getByRole("combobox", { name: "分类" }));
    await user.click(await screen.findByRole("option", { name: "听力" }));

    expect(
      requestMock.mock.calls
        .map(([config]) => config.url)
        .filter((url) => url.startsWith("/admin/papers?"))
        .some((url) => url.includes(`categoryId=${CATEGORY_ID}`)),
    ).toBe(false);

    await user.click(screen.getByRole("button", { name: "应用" }));
    await waitFor(() => {
      expect(router.state.location.search).toContain(
        `categoryId=${CATEGORY_ID}`,
      );
      expect(
        requestMock.mock.calls
          .map(([config]) => config.url)
          .filter((url) => url.startsWith("/admin/papers?"))
          .at(-1),
      ).toContain(`categoryId=${CATEGORY_ID}`);
    });
  });

  it("shows history locking in the table", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [paperListItem({ attemptCount: 3 })],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    renderAppAt("/papers");
    expect(await screen.findByText("3 次 · 内容已锁定")).toBeVisible();
  });
});
