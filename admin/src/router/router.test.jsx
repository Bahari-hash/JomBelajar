import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderAppAt } from "@/test/renderApp.jsx";

describe("admin routes", () => {
  it("renders the dashboard on direct root access", () => {
    renderAppAt("/");

    expect(screen.getByRole("heading", { level: 1, name: "工作台" })).toBeVisible();
    expect(screen.getByText("TinyLang 管理后台")).toBeVisible();
  });

  it("renders a 404 for an unknown path and returns to the dashboard", async () => {
    const user = userEvent.setup();
    renderAppAt("/missing-page");

    expect(screen.getByRole("heading", { level: 1, name: "页面未找到" })).toBeVisible();
    await user.click(screen.getByRole("link", { name: "返回工作台" }));

    expect(await screen.findByRole("heading", { level: 1, name: "工作台" })).toBeVisible();
  });
});
