import { describe, expect, it } from "vitest";
import {
  validateEmail,
  validateLoginPassword,
  validateNewPassword,
  validatePasswordConfirmation,
  validateVerificationCode,
} from "@/features/auth/authValidation";

describe("authValidation", () => {
  it("validates email presence, format, and length", () => {
    expect(validateEmail(" ")).toBe("请输入邮箱。");
    expect(validateEmail("invalid-email")).toBe("请输入有效的邮箱地址。");
    expect(validateEmail(`${"a".repeat(89)}@example.test`)).toBe(
      "邮箱不能超过 100 个字符。",
    );
    expect(validateEmail("user@example.test")).toBeNull();
  });

  it("matches login password constraints without inventing a minimum", () => {
    expect(validateLoginPassword("")).toBe("请输入密码。");
    expect(validateLoginPassword("x")).toBeNull();
    expect(validateLoginPassword("p".repeat(50))).toBeNull();
    expect(validateLoginPassword("p".repeat(51))).toBe(
      "密码不能超过 50 个字符。",
    );
  });

  it("validates new password and confirmation boundaries", () => {
    expect(validateNewPassword("")).toBe("请输入新密码。");
    expect(validateNewPassword("p".repeat(7))).toBe(
      "密码至少需要 8 个字符。",
    );
    expect(validateNewPassword("p".repeat(8))).toBeNull();
    expect(validateNewPassword("p".repeat(50))).toBeNull();
    expect(validateNewPassword("p".repeat(51))).toBe(
      "密码不能超过 50 个字符。",
    );
    expect(validatePasswordConfirmation("password", "")).toBe(
      "请再次输入密码。",
    );
    expect(validatePasswordConfirmation("password", "different")).toBe(
      "两次输入的密码不一致。",
    );
    expect(validatePasswordConfirmation("password", "password")).toBeNull();
  });

  it("requires exactly six numeric verification-code characters", () => {
    expect(validateVerificationCode("")).toBe("请输入验证码。");
    expect(validateVerificationCode("12345")).toBe(
      "验证码必须是 6 位数字。",
    );
    expect(validateVerificationCode("12345a")).toBe(
      "验证码必须是 6 位数字。",
    );
    expect(validateVerificationCode("123456")).toBeNull();
  });
});
