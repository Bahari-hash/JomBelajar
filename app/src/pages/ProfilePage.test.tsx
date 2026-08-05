import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import ProfilePage from "@/pages/ProfilePage";
import * as profileApi from "@/features/profile/profileApi";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";

const profile = {
  id: "user-1",
  email: "user@example.test",
  role: "User" as const,
  nickname: "学习者",
  avatarUrl: null,
  bio: "正在学习外语。",
  createdAt: "2026-08-01T00:00:00Z",
};

function renderProfile(value: ReturnType<typeof createAuthContextValue>) {
  return render(
    <MemoryRouter>
      <AuthContext value={value}>
        <ProfilePage />
      </AuthContext>
    </MemoryRouter>,
  );
}

describe("ProfilePage", () => {
  it("shows read-only account information and saves editable profile fields", async () => {
    const user = userEvent.setup();
    const updateProfile = vi
      .fn()
      .mockResolvedValue({ ...profile, nickname: "新昵称" });
    renderProfile(
      createAuthContextValue({
        status: "authenticated",
        profile,
        profileStatus: "ready",
        updateProfile,
      }),
    );

    expect(screen.getByText("user@example.test")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "学习者" })).toBeInTheDocument();
    expect(
      screen.queryByRole("textbox", { name: "邮箱" }),
    ).not.toBeInTheDocument();
    const nickname = screen.getByDisplayValue("学习者");
    await user.clear(nickname);
    await user.type(nickname, "新昵称");
    await user.click(screen.getByRole("button", { name: "保存资料" }));

    expect(updateProfile).toHaveBeenCalledWith({
      nickname: "新昵称",
      avatarUrl: null,
      bio: "正在学习外语。",
    });
    expect(await screen.findByText("个人资料已保存。")).toBeInTheDocument();
  });

  it("uploads a selected avatar before saving its confirmed URL", async () => {
    const user = userEvent.setup();
    const uploadAvatar = vi
      .spyOn(profileApi, "uploadAvatar")
      .mockResolvedValue("https://cdn.example.test/avatar.png");
    const updateProfile = vi.fn().mockResolvedValue({
      ...profile,
      avatarUrl: "https://cdn.example.test/avatar.png",
    });
    renderProfile(
      createAuthContextValue({
        status: "authenticated",
        profile,
        profileStatus: "ready",
        updateProfile,
      }),
    );

    const file = new File(["avatar"], "avatar.png", { type: "image/png" });
    await user.upload(screen.getByLabelText("上传头像"), file);
    expect(uploadAvatar).toHaveBeenCalledWith(file, expect.any(Function));
    expect(screen.getByRole("img", { name: "学习者的头像" })).toHaveAttribute(
      "src",
      "https://cdn.example.test/avatar.png",
    );

    await user.click(screen.getByRole("button", { name: "保存资料" }));
    expect(updateProfile).toHaveBeenCalledWith({
      nickname: "学习者",
      avatarUrl: "https://cdn.example.test/avatar.png",
      bio: "正在学习外语。",
    });
    uploadAvatar.mockRestore();
  });

  it("cancels a draft and exposes a recoverable profile error", async () => {
    const user = userEvent.setup();
    const refreshProfile = vi.fn().mockResolvedValue(profile);
    const { rerender } = renderProfile(
      createAuthContextValue({
        status: "authenticated",
        profile,
        profileStatus: "ready",
        refreshProfile,
      }),
    );

    const bio = screen.getByDisplayValue("正在学习外语。");
    await user.clear(bio);
    await user.type(bio, "临时内容");
    await user.click(screen.getByRole("button", { name: "取消修改" }));
    expect(bio).toHaveValue("正在学习外语。");

    rerender(
      <MemoryRouter>
        <AuthContext
          value={createAuthContextValue({
            status: "authenticated",
            profile: null,
            profileStatus: "error",
            profileError: "资料加载失败，请重试。",
            refreshProfile,
          })}
        >
          <ProfilePage />
        </AuthContext>
      </MemoryRouter>,
    );
    expect(screen.getByText("资料加载失败，请重试。")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "重新加载" }));
    expect(refreshProfile).toHaveBeenCalled();
  });
});
