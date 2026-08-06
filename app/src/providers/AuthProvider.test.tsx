import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { describe, expect, it, vi } from "vitest";
import { authApi } from "@/features/auth/authApi";
import { accountSecurityApi } from "@/features/auth/accountSecurityApi";
import { authStorageKey } from "@/features/auth/authStorage";
import { useAuth } from "@/hooks/useAuth";
import AuthProvider from "@/providers/AuthProvider";
import { AxiosHeaders } from "axios";
import { useAuthState } from "@/store/hooks";

function SessionStatus() {
  const { status, profile } = useAuth();
  return <output>{`${status}:${profile?.email ?? "none"}`}</output>;
}

function ReduxSessionStatus() {
  const state = useAuthState();
  return <output data-testid="redux-session">{JSON.stringify(state)}</output>;
}

function AccountSecurityStatus() {
  const { status, login, changeEmail, deleteAccount } = useAuth();
  return (
    <div>
      <output>{status}</output>
      <button
        type="button"
        onClick={() => void login("user@example.test", "password")}
      >
        登录命令
      </button>
      <button
        type="button"
        onClick={() => void changeEmail("new@example.test", "123456")}
      >
        换绑命令
      </button>
      <button type="button" onClick={() => void deleteAccount("123456")}>
        删除命令
      </button>
    </div>
  );
}

describe("AuthProvider", () => {
  it("starts anonymous without a stored refresh token", async () => {
    render(
      <StrictMode>
        <AuthProvider>
          <SessionStatus />
          <ReduxSessionStatus />
        </AuthProvider>
      </StrictMode>,
    );
    expect(await screen.findByText("anonymous:none")).toBeInTheDocument();
  });

  it("restores a rotated session and fetches the complete profile", async () => {
    sessionStorage.setItem(authStorageKey, '{"refreshToken":"refresh-old"}');
    const refresh = vi.spyOn(authApi, "refresh").mockResolvedValue({
      data: {
        token: "access-new",
        refreshToken: "refresh-new",
        expiresIn: 60,
        user: { id: "user-1", email: "user@example.test", role: "User" },
      },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    const getCurrentProfile = vi
      .spyOn(authApi, "getCurrentProfile")
      .mockResolvedValue({
        data: {
          id: "user-1",
          email: "user@example.test",
          role: "User",
          nickname: null,
          avatarUrl: null,
          bio: null,
          createdAt: "2026-08-01T00:00:00Z",
        },
        status: 200,
        statusText: "OK",
        headers: {},
        config: { headers: new AxiosHeaders() },
      });

    render(
      <StrictMode>
        <AuthProvider>
          <SessionStatus />
          <ReduxSessionStatus />
        </AuthProvider>
      </StrictMode>,
    );

    expect(
      await screen.findByText("authenticated:user@example.test"),
    ).toBeInTheDocument();
    const reduxState = screen.getByTestId("redux-session").textContent ?? "";
    expect(reduxState).toContain('"status":"authenticated"');
    expect(reduxState).toContain("user@example.test");
    expect(reduxState).not.toContain("access-new");
    expect(reduxState).not.toContain("refresh-new");
    expect(refresh).toHaveBeenCalledWith("refresh-old");
    expect(refresh).toHaveBeenCalledOnce();
    expect(getCurrentProfile).toHaveBeenCalledOnce();
    expect(sessionStorage.getItem(authStorageKey)).toBe(
      '{"refreshToken":"refresh-new"}',
    );
  });

  it("clears an invalid stored session and never exposes the refresh failure", async () => {
    sessionStorage.setItem(authStorageKey, '{"refreshToken":"expired"}');
    vi.spyOn(authApi, "refresh").mockRejectedValue(new Error("refresh secret"));

    render(
      <AuthProvider>
        <SessionStatus />
      </AuthProvider>,
    );

    expect(await screen.findByText("anonymous:none")).toBeInTheDocument();
    expect(screen.queryByText("refresh secret")).not.toBeInTheDocument();
    expect(sessionStorage.getItem(authStorageKey)).toBeNull();
  });

  it("clears Redux and persisted credentials after changing email", async () => {
    const user = userEvent.setup();
    vi.spyOn(authApi, "login").mockResolvedValue({
      data: {
        token: "access",
        refreshToken: "refresh",
        expiresIn: 60,
        user: { id: "user-1", email: "user@example.test", role: "User" },
      },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    vi.spyOn(authApi, "getCurrentProfile").mockResolvedValue({
      data: {
        id: "user-1",
        email: "user@example.test",
        role: "User",
        nickname: null,
        avatarUrl: null,
        bio: null,
        createdAt: "2026-08-01T00:00:00Z",
      },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    vi.spyOn(accountSecurityApi, "changeEmail").mockResolvedValue({
      data: { id: "user-1", email: "new@example.test" },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    render(
      <AuthProvider>
        <AccountSecurityStatus />
      </AuthProvider>,
    );

    await user.click(await screen.findByRole("button", { name: "登录命令" }));
    expect(await screen.findByText("authenticated")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "换绑命令" }));

    expect(await screen.findByText("anonymous")).toBeInTheDocument();
    expect(sessionStorage.getItem(authStorageKey)).toBeNull();
  });

  it("clears Redux and persisted credentials after deleting the account", async () => {
    const user = userEvent.setup();
    vi.spyOn(authApi, "login").mockResolvedValue({
      data: {
        token: "access",
        refreshToken: "refresh",
        expiresIn: 60,
        user: { id: "user-1", email: "user@example.test", role: "User" },
      },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    vi.spyOn(authApi, "getCurrentProfile").mockResolvedValue({
      data: {
        id: "user-1",
        email: "user@example.test",
        role: "User",
        nickname: null,
        avatarUrl: null,
        bio: null,
        createdAt: "2026-08-01T00:00:00Z",
      },
      status: 200,
      statusText: "OK",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    vi.spyOn(accountSecurityApi, "deleteAccount").mockResolvedValue({
      data: undefined,
      status: 204,
      statusText: "No Content",
      headers: {},
      config: { headers: new AxiosHeaders() },
    });
    render(
      <AuthProvider>
        <AccountSecurityStatus />
      </AuthProvider>,
    );

    await user.click(await screen.findByRole("button", { name: "登录命令" }));
    expect(await screen.findByText("authenticated")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "删除命令" }));

    expect(accountSecurityApi.deleteAccount).toHaveBeenCalledWith("123456");
    expect(await screen.findByText("anonymous")).toBeInTheDocument();
    expect(sessionStorage.getItem(authStorageKey)).toBeNull();
  });
});
