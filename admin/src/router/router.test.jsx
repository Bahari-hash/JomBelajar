import { screen, within } from "@testing-library/react";
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

  it("opens the static word batch route", async () => {
    renderAppAt("/words/batch");

    expect(
      await screen.findByRole("heading", { level: 1, name: "批量导入单词" }),
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
    expect(screen.getByRole("link", { name: "视频管理" })).toBeVisible();
    expect(screen.getByRole("link", { name: "视频分类" })).toBeVisible();
    expect(screen.getByRole("link", { name: "单词管理" })).toBeVisible();
  });

  it("opens the bottom system settings module from the sidebar", async () => {
    renderAppAt("/settings");

    expect(
      await screen.findByRole("heading", { level: 1, name: "系统设置" }),
    ).toBeVisible();
    const navigation = screen.getByRole("navigation", { name: "主导航" });
    expect(
      within(navigation).getByRole("link", { name: "系统设置" }),
    ).toHaveAttribute("href", "/settings");
    expect(screen.getByText("暂无系统设置项")).toBeVisible();
  });
});
