import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { ApiRequestError } from "@/features/auth/authErrors";
import LoginPage from "@/pages/LoginPage";
import RegisterPage from "@/pages/RegisterPage";
import { AuthContext } from "@/providers/authContext";
import { createAuthContextValue } from "@/test/authTestUtils";

function renderWithAuth(
  ui: React.ReactNode,
  value: ReturnType<typeof createAuthContextValue>,
) {
  return render(
    <MemoryRouter>
      <AuthContext value={value}>{ui}</AuthContext>
    </MemoryRouter>,
  );
}

describe("consumer auth forms", () => {
  it("shows a result message after account deletion", () => {
    render(
      <MemoryRouter
        initialEntries={[
          { pathname: "/login", state: { accountDeleted: true } },
        ]}
      >
        <AuthContext value={createAuthContextValue()}>
          <LoginPage />
        </AuthContext>
      </MemoryRouter>,
    );

    expect(
      screen.getByText("账号已删除，所有设备均已退出登录。"),
    ).toBeInTheDocument();
  });

  it("blocks invalid login fields before calling the API", async () => {
    const user = userEvent.setup();
    const login = vi.fn();
    renderWithAuth(<LoginPage />, createAuthContextValue({ login }));

    await user.type(screen.getByLabelText("邮箱"), "invalid-email");
    await user.click(screen.getByRole("button", { name: "登录" }));

    expect(screen.getByText("请输入有效的邮箱地址。")).toBeInTheDocument();
    expect(screen.getByText("请输入密码。")).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it("validates login fields and clears the password after invalid credentials", async () => {
    const user = userEvent.setup();
    const login = vi
      .fn()
      .mockRejectedValue(
        new ApiRequestError("邮箱或密码错误。", { code: "InvalidCredentials" }),
      );
    renderWithAuth(<LoginPage />, createAuthContextValue({ login }));

    await user.type(screen.getByLabelText("邮箱"), "user@example.test");
    await user.type(screen.getByLabelText(/^密码/), "wrong-password");
    await user.click(screen.getByRole("button", { name: "登录" }));

    expect(login).toHaveBeenCalledWith("user@example.test", "wrong-password");
    expect(await screen.findByText("邮箱或密码错误。")).toBeInTheDocument();
    expect(screen.getByLabelText(/^密码/)).toHaveValue("");
  });

  it("submits a successful login and supports password visibility", async () => {
    const user = userEvent.setup();
    const login = vi.fn().mockResolvedValue({ id: "1" });
    renderWithAuth(<LoginPage />, createAuthContextValue({ login }));

    const password = screen.getByLabelText(/^密码/);
    await user.type(screen.getByLabelText("邮箱"), "user@example.test");
    await user.type(password, "correct-password");
    expect(password).toHaveAttribute("type", "password");
    await user.click(screen.getByRole("button", { name: "显示或隐藏密码" }));
    expect(password).toHaveAttribute("type", "text");
    expect(screen.getByRole("link", { name: "忘记密码？" })).toHaveAttribute(
      "href",
      "/forgot-password",
    );
  });

  it("sends a real registration token request and starts the resend countdown", async () => {
    const user = userEvent.setup();
    const requestRegisterToken = vi.fn().mockResolvedValue(undefined);
    renderWithAuth(
      <RegisterPage />,
      createAuthContextValue({ requestRegisterToken }),
    );

    await user.type(screen.getByLabelText("邮箱"), "new@example.test");
    await user.click(screen.getByRole("button", { name: "发送验证码" }));

    expect(requestRegisterToken).toHaveBeenCalledWith("new@example.test");
    expect(
      await screen.findByText("验证码已发送，请检查邮箱。"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /后重发/ })).toBeDisabled();
  });

  it("blocks an invalid registration email before requesting a token", async () => {
    const user = userEvent.setup();
    const requestRegisterToken = vi.fn();
    renderWithAuth(
      <RegisterPage />,
      createAuthContextValue({ requestRegisterToken }),
    );

    await user.type(screen.getByLabelText("邮箱"), "invalid-email");
    await user.click(screen.getByRole("button", { name: "发送验证码" }));

    expect(screen.getByText("请输入有效的邮箱地址。")).toBeInTheDocument();
    expect(requestRegisterToken).not.toHaveBeenCalled();
  });

  it("requires matching passwords and does not create a session on registration", async () => {
    const user = userEvent.setup();
    const register = vi.fn();
    renderWithAuth(<RegisterPage />, createAuthContextValue({ register }));

    await user.type(screen.getByLabelText("邮箱"), "new@example.test");
    await user.type(screen.getByLabelText("注册验证码"), "123456");
    await user.type(screen.getByLabelText(/^密码/), "password1");
    await user.type(screen.getByLabelText("确认密码"), "password2");
    await user.click(screen.getByRole("button", { name: "注册" }));

    expect(
      await screen.findByText("两次输入的密码不一致。"),
    ).toBeInTheDocument();
    expect(register).not.toHaveBeenCalled();
  });

  it("shows registration password length errors before submitting", async () => {
    const user = userEvent.setup();
    const register = vi.fn();
    renderWithAuth(<RegisterPage />, createAuthContextValue({ register }));

    await user.type(screen.getByLabelText("邮箱"), "new@example.test");
    await user.type(screen.getByLabelText("注册验证码"), "123456");
    await user.type(screen.getByLabelText(/^密码/), "short");
    await user.type(screen.getByLabelText("确认密码"), "short");
    await user.click(screen.getByRole("button", { name: "注册" }));

    expect(screen.getByText("密码至少需要 8 个字符。"))
      .toBeInTheDocument();
    expect(register).not.toHaveBeenCalled();
  });
});
