import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { ApiRequestError } from "@/features/auth/authErrors";
import { authApi } from "@/features/auth/authApi";
import ProfilePage from "@/pages/ProfilePage";
import * as profileApi from "@/features/profile/profileApi";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";
import { createAppStore } from "@/store/store";

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
    <Provider store={createAppStore()}>
      <MemoryRouter>
        <AuthContext value={value}>
          <ProfilePage />
        </AuthContext>
      </MemoryRouter>
    </Provider>,
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
      avatarMediaResourceId: null,
      bio: "正在学习外语。",
    });
    expect(await screen.findByText("个人资料已保存。")).toBeInTheDocument();
  });

  it("uploads a selected avatar before saving its confirmed URL", async () => {
    const user = userEvent.setup();
    const uploadAvatar = vi
      .spyOn(profileApi, "uploadAvatar")
      .mockResolvedValue({
        id: "resource-1",
        uploaderId: "user-1",
        objectName: "avatars/avatar.png",
        originalName: "avatar.png",
        module: "Avatar",
        status: "Active",
        size: 123,
        extension: ".png",
        contentType: "image/png",
        url: "https://cdn.example.test/avatar.png",
        createdAt: "2026-08-01T00:00:00Z",
      });
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
      avatarMediaResourceId: "resource-1",
      bio: "正在学习外语。",
    });
    uploadAvatar.mockRestore();
  });

  it("shows local avatar validation without requesting a presign", async () => {
    const user = userEvent.setup();
    const presignAvatar = vi.spyOn(authApi, "presignAvatar");
    renderProfile(
      createAuthContextValue({
        status: "authenticated",
        profile,
        profileStatus: "ready",
      }),
    );

    const mismatchedFile = new File(["avatar"], "avatar.jpg", {
      type: "image/png",
    });
    await user.upload(screen.getByLabelText("上传头像"), mismatchedFile);

    expect(
      (await screen.findAllByText("头像文件扩展名与文件类型不匹配。")).length,
    ).toBeGreaterThan(0);
    expect(presignAvatar).not.toHaveBeenCalled();
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
      <Provider store={createAppStore()}>
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
        </MemoryRouter>
      </Provider>,
    );
    expect(screen.getByText("资料加载失败，请重试。")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "重新加载" }));
    expect(refreshProfile).toHaveBeenCalled();
  });

  it("renders server field validation beside the matching profile control", async () => {
    const user = userEvent.setup();
    const updateProfile = vi.fn().mockRejectedValue(
      new ApiRequestError("请检查表单中的填写内容。", {
        code: "RequestValidationFailed",
        fieldErrors: { Nickname: "昵称不能超过 60 个字符。" },
      }),
    );
    renderProfile(
      createAuthContextValue({
        status: "authenticated",
        profile,
        profileStatus: "ready",
        updateProfile,
      }),
    );

    const nickname = screen.getByDisplayValue("学习者");
    await user.clear(nickname);
    await user.type(nickname, "新昵称");
    await user.click(screen.getByRole("button", { name: "保存资料" }));

    expect(
      await screen.findByText("昵称不能超过 60 个字符。"),
    ).toBeInTheDocument();
    expect(updateProfile).toHaveBeenCalled();
  });
});
