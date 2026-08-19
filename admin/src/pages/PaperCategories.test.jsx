import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const CATEGORY_ID = "11111111-1111-4111-8111-111111111111";
const category = {
  id: CATEGORY_ID,
  name: "听写",
  slug: "dictation",
  description: "听写专项",
  isActive: true,
  paperCount: 0,
  createdAt: "2026-08-19T10:00:00Z",
};
const page = {
  items: [category],
  page: 1,
  pageSize: 20,
  totalCount: 1,
  totalPages: 1,
};

describe("PaperCategories", () => {
  it("lists categories and creates a new category", async () => {
    tokenVault.install("access", "refresh");
    const requests = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.method === "POST"
            ? { ...category, name: config.data.name, slug: config.data.slug }
            : page,
          config.method === "POST" ? 201 : 200,
        ),
      ),
    );
    const user = userEvent.setup();
    renderAppAt("/paper-categories");
    expect(
      await screen.findByRole("heading", { name: "试卷分类" }),
    ).toBeVisible();
    expect(await screen.findByText("听写")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "新建分类" }));
    await user.type(screen.getByLabelText("名称 *"), "语法");
    await user.clear(screen.getByLabelText("Slug *"));
    await user.type(screen.getByLabelText("Slug *"), "grammar");
    await user.click(screen.getByRole("button", { name: "保存" }));
    await waitFor(() =>
      expect(
        requests.mock.calls.some(
          ([config]) =>
            config.url === "/admin/paper-categories" &&
            config.method === "POST" &&
            config.data.name === "语法",
        ),
      ).toBe(true),
    );
  });

  it("prevents deleting a category which is still in use", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({ ...page, items: [{ ...category, paperCount: 3 }] }),
      ),
    );
    const user = userEvent.setup();
    renderAppAt("/paper-categories");
    await screen.findByText("听写");
    await user.click(screen.getByRole("button", { name: "管理分类 听写" }));
    expect(screen.getByRole("menuitem", { name: "删除" })).toHaveAttribute(
      "aria-disabled",
      "true",
    );
  });
});
