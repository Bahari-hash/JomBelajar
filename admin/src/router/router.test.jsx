import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderAppAt } from "@/test/renderApp.jsx";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { tokenVault } from "@/services/tokenVault.js";

describe("admin routes", () => {
  it("renders the dashboard on direct root access", async () => {
    renderAppAt("/");

    expect(
      await screen.findByRole("heading", { level: 1, name: "工作台" }),
    ).toBeVisible();
    expect(screen.getByText("TinyLang 管理后台")).toBeVisible();
  });

  it("renders a 404 for an unknown path and returns to the dashboard", async () => {
    const user = userEvent.setup();
    renderAppAt("/missing-page");

    expect(
      await screen.findByRole("heading", { level: 1, name: "页面未找到" }),
    ).toBeVisible();
    await user.click(screen.getByRole("link", { name: "返回工作台" }));

    expect(
      await screen.findByRole("heading", { level: 1, name: "工作台" }),
    ).toBeVisible();
  });

  it("directly opens the protected article editor and shared content navigation", async () => {
    tokenVault.clear();
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [],
          page: 1,
          pageSize: 100,
          totalCount: 0,
          totalPages: 0,
        }),
      ),
    );
    renderAppAt("/articles/new");

    expect(
      await screen.findByRole("heading", { level: 1, name: "新建文章" }),
    ).toBeVisible();
    expect(screen.getByText("内容管理")).toBeVisible();
    expect(screen.getByRole("link", { name: "文章管理" })).toBeVisible();
    expect(screen.getByRole("link", { name: "文章分类" })).toBeVisible();
  });
});
