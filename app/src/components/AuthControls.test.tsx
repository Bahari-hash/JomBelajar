import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AuthControls from "@/components/AuthControls";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";

function LocationStatus() {
  const location = useLocation();
  return <output>{location.pathname}</output>;
}

describe("AuthControls", () => {
  it("opens the account menu, restores focus on Escape, and clears session on logout", async () => {
    const user = userEvent.setup();
    const logout = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter initialEntries={["/profile"]}>
        <AuthContext
          value={createAuthContextValue({
            status: "authenticated",
            profile: {
              id: "user-1",
              email: "user@example.test",
              role: "User",
              nickname: "学习者",
              avatarUrl: null,
              bio: null,
              createdAt: "2026-08-01T00:00:00Z",
            },
            profileStatus: "ready",
            logout,
          })}
        >
          <AuthControls />
          <LocationStatus />
        </AuthContext>
      </MemoryRouter>,
    );

    const trigger = screen.getByRole("button", { name: /学习者/ });
    await user.click(trigger);
    expect(screen.getByRole("menu", { name: "用户菜单" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "个人资料" })).toHaveFocus();

    await user.keyboard("{Escape}");
    expect(
      screen.queryByRole("menu", { name: "用户菜单" }),
    ).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();

    await user.click(trigger);
    await user.click(screen.getByRole("menuitem", { name: "退出登录" }));
    expect(logout).toHaveBeenCalledOnce();
    expect(await screen.findByText("/")).toBeInTheDocument();
  });

  it("restores the logout action after a failed command", async () => {
    const user = userEvent.setup();
    const logout = vi.fn().mockRejectedValue(new Error("network"));
    render(
      <MemoryRouter>
        <AuthContext
          value={createAuthContextValue({
            status: "authenticated",
            profile: {
              id: "user-1",
              email: "user@example.test",
              role: "User",
              nickname: "学习者",
              avatarUrl: null,
              bio: null,
              createdAt: "2026-08-01T00:00:00Z",
            },
            profileStatus: "ready",
            logout,
          })}
        >
          <AuthControls />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.click(screen.getByRole("button", { name: /学习者/ }));
    const logoutButton = screen.getByRole("menuitem", { name: "退出登录" });
    await user.click(logoutButton);
    await waitFor(() => expect(logoutButton).not.toBeDisabled());
    expect(
      screen.getByRole("menuitem", { name: "退出登录" }),
    ).toBeInTheDocument();
  });
});
