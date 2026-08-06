import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import AccountSecurityPanel from "@/features/profile/AccountSecurityPanel";
import { ApiRequestError } from "@/features/auth/authErrors";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";

function LocationStatus() {
  const location = useLocation();
  return <output>{location.pathname}</output>;
}

describe("AccountSecurityPanel", () => {
  it("sends a new-email code and completes email change", async () => {
    const user = userEvent.setup();
    const requestChangeEmailToken = vi.fn().mockResolvedValue(undefined);
    const changeEmail = vi.fn().mockResolvedValue({
      id: "user-1",
      email: "new@example.test",
    });
    render(
      <MemoryRouter initialEntries={["/profile"]}>
        <AuthContext
          value={createAuthContextValue({
            status: "authenticated",
            requestChangeEmailToken,
            changeEmail,
          })}
        >
          <AccountSecurityPanel currentEmail="user@example.test" />
          <LocationStatus />
        </AuthContext>
      </MemoryRouter>,
    );

    const form = screen
      .getByRole("heading", { name: "修改邮箱" })
      .closest("form")!;
    await user.type(within(form).getByLabelText("新邮箱"), "new@example.test");
    await user.click(within(form).getByRole("button", { name: "发送验证码" }));
    expect(requestChangeEmailToken).toHaveBeenCalledWith("new@example.test");
    expect(within(form).getByRole("button", { name: /后重发/ })).toBeDisabled();

    await user.type(within(form).getByLabelText("邮箱验证码"), "123456");
    await user.click(
      within(form).getByRole("button", { name: "确认修改邮箱" }),
    );
    expect(changeEmail).toHaveBeenCalledWith("new@example.test", "123456");
    expect(await screen.findByText("/login")).toBeInTheDocument();
  });

  it("validates password confirmation before resetting", async () => {
    const user = userEvent.setup();
    const resetPassword = vi.fn();
    render(
      <MemoryRouter>
        <AuthContext value={createAuthContextValue({ resetPassword })}>
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    const form = screen
      .getByRole("heading", { name: "重置密码" })
      .closest("form")!;
    await user.type(within(form).getByLabelText("邮箱验证码"), "123456");
    await user.type(within(form).getByLabelText(/^新密码/), "new-password");
    await user.type(
      within(form).getByLabelText("确认新密码"),
      "different-password",
    );
    await user.click(
      within(form).getByRole("button", { name: "确认重置密码" }),
    );

    expect(screen.getByText("两次输入的密码不一致。")).toBeInTheDocument();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  it("shows a password length error on the new-password field", async () => {
    const user = userEvent.setup();
    const resetPassword = vi.fn();
    render(
      <MemoryRouter>
        <AuthContext value={createAuthContextValue({ resetPassword })}>
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    const form = screen
      .getByRole("heading", { name: "重置密码" })
      .closest("form")!;
    await user.type(within(form).getByLabelText("邮箱验证码"), "123456");
    await user.type(within(form).getByLabelText(/^新密码/), "short");
    await user.type(within(form).getByLabelText("确认新密码"), "short");
    await user.click(
      within(form).getByRole("button", { name: "确认重置密码" }),
    );

    expect(screen.getByText("密码至少需要 8 个字符。")).toBeInTheDocument();
    expect(resetPassword).not.toHaveBeenCalled();
  });

  it("renders server email validation beside the new-email control", async () => {
    const user = userEvent.setup();
    const requestChangeEmailToken = vi.fn().mockResolvedValue(undefined);
    const changeEmail = vi.fn().mockRejectedValue(
      new ApiRequestError("请检查表单中的填写内容。", {
        code: "RequestValidationFailed",
        fieldErrors: { NewEmail: "请输入有效的邮箱地址。" },
      }),
    );
    render(
      <MemoryRouter>
        <AuthContext
          value={createAuthContextValue({
            requestChangeEmailToken,
            changeEmail,
          })}
        >
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    const form = screen
      .getByRole("heading", { name: "修改邮箱" })
      .closest("form")!;
    await user.type(within(form).getByLabelText("新邮箱"), "new@example.test");
    await user.type(within(form).getByLabelText("邮箱验证码"), "123456");
    await user.click(
      within(form).getByRole("button", { name: "确认修改邮箱" }),
    );

    expect(
      await within(form).findByText("请输入有效的邮箱地址。"),
    ).toBeInTheDocument();
    expect(changeEmail).toHaveBeenCalledWith("new@example.test", "123456");
  });

  it("restores the reset-code action after a request timeout", async () => {
    const user = userEvent.setup();
    const requestResetPasswordToken = vi
      .fn()
      .mockRejectedValue(
        new ApiRequestError("请求超时，请检查网络连接后重试。"),
      );
    render(
      <MemoryRouter>
        <AuthContext
          value={createAuthContextValue({ requestResetPasswordToken })}
        >
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    const form = screen
      .getByRole("heading", { name: "重置密码" })
      .closest("form")!;
    const sendButton = within(form).getByRole("button", { name: "发送验证码" });
    await user.click(sendButton);

    expect(
      await within(form).findByText("请求超时，请检查网络连接后重试。"),
    ).toBeInTheDocument();
    expect(sendButton).toBeEnabled();
  });

  it("renders a red account deletion action and validates before deleting", async () => {
    const user = userEvent.setup();
    const deleteAccount = vi.fn();
    render(
      <MemoryRouter>
        <AuthContext value={createAuthContextValue({ deleteAccount })}>
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    const openButton = screen.getByRole("button", { name: "删除账号" });
    expect(openButton).toHaveClass("btn-error");
    await user.click(openButton);

    const form = screen.getByRole("form", { name: "删除账号确认" });
    await user.click(
      within(form).getByRole("button", { name: "永久删除账号" }),
    );

    expect(within(form).getByText("请输入验证码。")).toBeInTheDocument();
    expect(deleteAccount).not.toHaveBeenCalled();
  });

  it("sends a deletion code and deletes the account after confirmation", async () => {
    const user = userEvent.setup();
    const requestDeleteAccountToken = vi.fn().mockResolvedValue(undefined);
    const deleteAccount = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter initialEntries={["/profile"]}>
        <AuthContext
          value={createAuthContextValue({
            requestDeleteAccountToken,
            deleteAccount,
          })}
        >
          <AccountSecurityPanel currentEmail="user@example.test" />
          <LocationStatus />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.click(screen.getByRole("button", { name: "删除账号" }));
    const form = screen.getByRole("form", { name: "删除账号确认" });
    await user.click(within(form).getByRole("button", { name: "发送验证码" }));
    expect(requestDeleteAccountToken).toHaveBeenCalledOnce();
    expect(
      within(form).getByText("验证码已发送到 user@example.test。"),
    ).toBeInTheDocument();

    await user.type(within(form).getByLabelText("邮箱验证码"), "123456");
    await user.click(
      within(form).getByRole("button", { name: "永久删除账号" }),
    );

    expect(deleteAccount).toHaveBeenCalledWith("123456");
    expect(await screen.findByText("/login")).toBeInTheDocument();
  });

  it("keeps the deletion code available after the backend rejects it", async () => {
    const user = userEvent.setup();
    const deleteAccount = vi.fn().mockRejectedValue(
      new ApiRequestError("验证码错误或已失效，请重新获取。", {
        code: "VerificationCodeInvalid",
        status: 401,
      }),
    );
    render(
      <MemoryRouter>
        <AuthContext value={createAuthContextValue({ deleteAccount })}>
          <AccountSecurityPanel currentEmail="user@example.test" />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.click(screen.getByRole("button", { name: "删除账号" }));
    const form = screen.getByRole("form", { name: "删除账号确认" });
    const codeInput = within(form).getByLabelText("邮箱验证码");
    await user.type(codeInput, "123456");
    await user.click(
      within(form).getByRole("button", { name: "永久删除账号" }),
    );

    expect(
      await within(form).findByText("验证码错误或已失效，请重新获取。"),
    ).toBeInTheDocument();
    expect(codeInput).toHaveValue("123456");
    expect(deleteAccount).toHaveBeenCalledOnce();
  });
});
