import { render, screen } from "@testing-library/react";
import { StrictMode } from "react";
import { describe, expect, it, vi } from "vitest";
import { authApi } from "@/features/auth/authApi";
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
});
