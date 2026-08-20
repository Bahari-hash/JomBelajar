import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { Provider } from "react-redux";
import { describe, expect, it } from "vitest";
import { routes } from "@/router";
import { AuthContext } from "@/providers/authContext";
import ThemeProvider from "@/providers/ThemeProvider";
import { createAuthContextValue } from "@/test/authTestUtils";
import { createAppStore } from "@/store/store";

const profile = {
  id: "user-1",
  email: "user@example.test",
  role: "User" as const,
  nickname: null,
  avatarUrl: null,
  bio: null,
  createdAt: "2026-08-01T00:00:00Z",
};

describe("auth route boundaries", () => {
  it("redirects anonymous profile access to login with an internal return path", async () => {
    const router = createMemoryRouter(routes, { initialEntries: ["/profile"] });
    render(
      <Provider store={createAppStore()}>
        <ThemeProvider>
          <AuthContext value={createAuthContextValue()}>
            <RouterProvider router={router} />
          </AuthContext>
        </ThemeProvider>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "登录 JomBelajar" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/login");
    expect(router.state.location.search).toBe("?returnTo=%2Fprofile");
  });

  it("sends an authenticated user to the safe return path, rejecting an external URL", async () => {
    const router = createMemoryRouter(routes, {
      initialEntries: ["/login?returnTo=https%3A%2F%2Fevil.example%2Fsteal"],
    });
    render(
      <Provider store={createAppStore()}>
        <ThemeProvider>
          <AuthContext
            value={createAuthContextValue({
              status: "authenticated",
              profile,
              profileStatus: "ready",
            })}
          >
            <RouterProvider router={router} />
          </AuthContext>
        </ThemeProvider>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "个人资料" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/profile");
  });

  it("redirects an authenticated user away from anonymous password recovery", async () => {
    const router = createMemoryRouter(routes, {
      initialEntries: ["/forgot-password"],
    });
    render(
      <Provider store={createAppStore()}>
        <ThemeProvider>
          <AuthContext
            value={createAuthContextValue({
              status: "authenticated",
              profile,
              profileStatus: "ready",
            })}
          >
            <RouterProvider router={router} />
          </AuthContext>
        </ThemeProvider>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "个人资料" }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/profile");
  });
});
