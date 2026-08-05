import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { ApiRequestError } from "@/features/auth/authErrors";
import ForgotPasswordPage from "@/pages/ForgotPasswordPage";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";

function LocationStatus() {
  const location = useLocation();
  return <output>{location.pathname}</output>;
}

describe("ForgotPasswordPage", () => {
  it("blocks an invalid email before requesting a recovery code", async () => {
    const user = userEvent.setup();
    const requestForgotPasswordToken = vi.fn();
    render(
      <MemoryRouter initialEntries={["/forgot-password"]}>
        <AuthContext
          value={createAuthContextValue({ requestForgotPasswordToken })}
        >
          <ForgotPasswordPage />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText("邮箱"), "invalid-email");
    await user.click(screen.getByRole("button", { name: "发送验证码" }));

    expect(screen.getByText("请输入有效的邮箱地址。")).toBeInTheDocument();
    expect(requestForgotPasswordToken).not.toHaveBeenCalled();
  });

  it("uses enumeration-safe copy and resets an anonymous account", async () => {
    const user = userEvent.setup();
    const requestForgotPasswordToken = vi.fn().mockResolvedValue(undefined);
    const forgotPassword = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter initialEntries={["/forgot-password"]}>
        <AuthContext
          value={createAuthContextValue({
            requestForgotPasswordToken,
            forgotPassword,
          })}
        >
          <ForgotPasswordPage />
          <LocationStatus />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText("邮箱"), "user@example.test");
    await user.click(screen.getByRole("button", { name: "发送验证码" }));
    expect(requestForgotPasswordToken).toHaveBeenCalledWith("user@example.test");
    expect(
      screen.getByText("若该邮箱已注册，验证码将发送至该邮箱。"),
    ).toBeInTheDocument();

    await user.type(screen.getByLabelText("邮箱验证码"), "123456");
    await user.type(screen.getByLabelText("新密码"), "new-password");
    await user.type(screen.getByLabelText("确认新密码"), "new-password");
    await user.click(screen.getByRole("button", { name: "重置密码" }));

    expect(forgotPassword).toHaveBeenCalledWith(
      "user@example.test",
      "new-password",
      "123456",
    );
    expect(await screen.findByText("/login")).toBeInTheDocument();
  });

  it("explains rate limits and restores the send action", async () => {
    const user = userEvent.setup();
    const requestForgotPasswordToken = vi.fn().mockRejectedValue(
      new ApiRequestError("请求过于频繁，请在 27 秒后重试。", {
        status: 429,
        retryAfterSeconds: 27,
      }),
    );
    render(
      <MemoryRouter initialEntries={["/forgot-password"]}>
        <AuthContext
          value={createAuthContextValue({ requestForgotPasswordToken })}
        >
          <ForgotPasswordPage />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText("邮箱"), "user@example.test");
    const sendButton = screen.getByRole("button", { name: "发送验证码" });
    await user.click(sendButton);

    expect(
      await screen.findByText("请求过于频繁，请在 27 秒后重试。"),
    ).toBeInTheDocument();
    expect(sendButton).toBeEnabled();
    expect(screen.getByLabelText("邮箱")).toHaveValue("user@example.test");
  });

  it("blocks a short password and submits once after the user corrects it", async () => {
    const user = userEvent.setup();
    const forgotPassword = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter initialEntries={["/forgot-password"]}>
        <AuthContext value={createAuthContextValue({ forgotPassword })}>
          <ForgotPasswordPage />
        </AuthContext>
      </MemoryRouter>,
    );

    await user.type(screen.getByLabelText("邮箱"), "user@example.test");
    await user.type(screen.getByLabelText("邮箱验证码"), "123456");
    await user.type(screen.getByLabelText("新密码"), "short");
    await user.type(screen.getByLabelText("确认新密码"), "short");
    await user.click(screen.getByRole("button", { name: "重置密码" }));

    expect(screen.getByText("密码至少需要 8 个字符。"))
      .toBeInTheDocument();
    expect(forgotPassword).not.toHaveBeenCalled();

    const newPassword = screen.getByLabelText(/^新密码/);
    const passwordConfirmation = screen.getByLabelText(/^确认新密码/);
    await user.clear(newPassword);
    await user.type(newPassword, "correct-password");
    await user.clear(passwordConfirmation);
    await user.type(passwordConfirmation, "correct-password");
    await user.click(screen.getByRole("button", { name: "重置密码" }));

    expect(forgotPassword).toHaveBeenCalledOnce();
    expect(forgotPassword).toHaveBeenCalledWith(
      "user@example.test",
      "correct-password",
      "123456",
    );
  });
});
