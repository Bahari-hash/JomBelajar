import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import {
  adminTokenResponse,
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";
import { AUTH_STATUS } from "@/store/authSlice.js";
import { createAppStore } from "@/store/index.js";

const UNAUTHENTICATED = {
  status: AUTH_STATUS.UNAUTHENTICATED,
  user: null,
  message: null,
};

describe("Login", () => {
  it("exposes the validated login fields and password visibility control", async () => {
    const user = userEvent.setup();
    renderAppAt("/login", { auth: UNAUTHENTICATED });

    const email = await screen.findByLabelText("邮箱");
    const password = screen.getByLabelText("密码");
    expect(email).toHaveAttribute("autocomplete", "username");
    expect(email).toHaveAttribute("maxlength", "100");
    expect(password).toHaveAttribute("autocomplete", "current-password");
    expect(password).toHaveAttribute("maxlength", "50");

    await user.click(screen.getByRole("button", { name: "显示密码" }));
    expect(password).toHaveAttribute("type", "text");
    expect(screen.getByRole("button", { name: "隐藏密码" })).toBeVisible();
  });

  it("validates required fields before making a request", async () => {
    const user = userEvent.setup();
    const requestMock = mockHttpClient(vi.fn());
    renderAppAt("/login", { auth: UNAUTHENTICATED });

    await user.click(await screen.findByRole("button", { name: "登录" }));

    expect(screen.getByText("请输入邮箱。")).toBeVisible();
    expect(screen.getByText("请输入密码。")).toBeVisible();
    expect(requestMock).not.toHaveBeenCalled();
  });

  it("establishes an admin session without placing credentials or tokens in Redux", async () => {
    const user = userEvent.setup();
    mockHttpClient(() => Promise.resolve(axiosResponse(adminTokenResponse())));
    const actions = [];
    const store = createAppStore({ auth: UNAUTHENTICATED });
    const originalDispatch = store.dispatch;
    store.dispatch = (action) => {
      actions.push(action);
      return originalDispatch(action);
    };
    renderAppAt("/login", { store });

    await user.type(await screen.findByLabelText("邮箱"), "admin@example.test");
    await user.type(screen.getByLabelText("密码"), "secret-password");
    await user.click(screen.getByRole("button", { name: "登录" }));

    expect(await screen.findByRole("heading", { level: 1, name: "工作台" })).toBeVisible();
    expect(store.getState().auth.user).toEqual({
      id: "11111111-1111-1111-1111-111111111111",
      email: "admin@example.test",
      role: "Admin",
    });
    const serializedActions = JSON.stringify(actions);
    expect(serializedActions).not.toContain("secret-password");
    expect(serializedActions).not.toContain("access-token");
    expect(serializedActions).not.toContain("refresh-token");
  });

  it("maps backend field errors to the matching control", async () => {
    const user = userEvent.setup();
    mockHttpClient(() =>
      Promise.reject(
        axiosHttpError(
          {
            status: 400,
            detail: "请求验证失败。",
            errors: { Email: ["邮箱格式无效。"] },
          },
          400,
        ),
      ),
    );
    renderAppAt("/login", { auth: UNAUTHENTICATED });

    await user.type(await screen.findByLabelText("邮箱"), "admin@example.test");
    await user.type(screen.getByLabelText("密码"), "secret-password");
    await user.click(screen.getByRole("button", { name: "登录" }));

    expect(await screen.findByText("邮箱格式无效。")).toBeVisible();
    expect(screen.getByLabelText("邮箱")).toHaveAttribute("aria-invalid", "true");
  });
});
