import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { clearSession } from "@/features/auth/sessionStore";
import ThemeProvider from "@/providers/ThemeProvider";
import AuthProvider from "@/providers/AuthProvider";
import RouteErrorPage from "@/pages/RouteErrorPage";
import { routes } from "@/router";

function renderRoute(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <ThemeProvider>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </ThemeProvider>,
  );
  return router;
}

describe("consumer routes", () => {
  beforeEach(() => clearSession());
  it.each([
    ["/", "今天想练习什么？"],
    ["/articles", "文章"],
    ["/papers", "在线测试"],
    ["/forgot-password", "重置密码"],
    ["/missing", "页面未找到"],
  ])("renders %s inside the shared layout", async (path, heading) => {
    renderRoute(path);

    expect(
      await screen.findByRole("heading", { name: heading, level: 1 }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "TinyLang 首页" }),
    ).toBeInTheDocument();
    expect(screen.getByText("TinyLang 外语学习平台")).toBeInTheDocument();
  });

  it.each([
    ["/videos?page=2", "%2Fvideos%3Fpage%3D2"],
    ["/words", "%2Fwords"],
  ])("protects %s and preserves the return path", async (path, returnTo) => {
    const router = renderRoute(path);
    expect(
      await screen.findByRole("heading", { name: "登录 TinyLang", level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/login");
    expect(router.state.location.search).toContain(
      `returnTo=${returnTo}`,
    );
  });

  it("supports navigation and browser history", async () => {
    const user = userEvent.setup();
    const router = renderRoute("/");
    const articleLinks = await screen.findAllByRole("link", { name: /文章/ });
    await user.click(articleLinks[0]!);

    expect(
      await screen.findByRole("heading", { name: "文章", level: 1 }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/articles");

    await router.navigate(-1);
    expect(
      await screen.findByRole("heading", { name: "今天想练习什么？" }),
    ).toBeInTheDocument();
  });

  it("opens the mobile menu, closes it with Escape, and restores focus", async () => {
    const user = userEvent.setup();
    renderRoute("/");
    const openButton = await screen.findByRole("button", {
      name: "打开导航菜单",
    });

    await user.click(openButton);
    expect(
      screen.getByRole("dialog", { name: "移动导航" }),
    ).toBeInTheDocument();
    const closeButton = screen.getAllByRole("button", {
      name: "关闭导航菜单",
    })[1]!;
    expect(closeButton).toHaveFocus();
    expect(document.body.style.overflow).toBe("hidden");

    await user.keyboard("{Shift>}{Tab}{/Shift}");
    expect(
      screen.getAllByRole("combobox", { name: "主题" }).at(-1),
    ).toHaveFocus();
    await user.tab();
    expect(closeButton).toHaveFocus();

    await user.keyboard("{Escape}");
    expect(
      screen.queryByRole("dialog", { name: "移动导航" }),
    ).not.toBeInTheDocument();
    expect(openButton).toHaveFocus();
    expect(document.body.style.overflow).toBe("");
  });

  it("shows a recoverable route error without exposing response details", async () => {
    const errorRouter = createMemoryRouter(
      [
        {
          path: "/",
          loader: () => {
            throw new Response("internal details", { status: 503 });
          },
          ErrorBoundary: RouteErrorPage,
        },
      ],
      { initialEntries: ["/"] },
    );

    render(
      <ThemeProvider>
        <AuthProvider>
          <RouterProvider router={errorRouter} />
        </AuthProvider>
      </ThemeProvider>,
    );

    expect(
      await screen.findByRole("heading", { name: "暂时无法打开此页面" }),
    ).toBeInTheDocument();
    expect(screen.getByText("503")).toBeInTheDocument();
    expect(screen.queryByText("internal details")).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "重新加载" }),
    ).toBeInTheDocument();
  });
});
