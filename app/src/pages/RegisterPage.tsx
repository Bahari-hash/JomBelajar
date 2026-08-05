import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import AuthPageShell from "@/components/AuthPageShell";
import PasswordField from "@/components/PasswordField";
import {
  getFieldError,
  toApiRequestError,
  type FieldErrors,
} from "@/features/auth/authErrors";
import { useAuth } from "@/hooks/useAuth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

interface RegisterFields {
  email: string;
  verificationCode: string;
  password: string;
  passwordConfirmation: string;
}

function validate(fields: RegisterFields): FieldErrors {
  const errors: FieldErrors = {};
  if (!fields.email.trim()) {
    errors.email = "请输入邮箱。";
  } else if (
    fields.email.trim().length > 100 ||
    !/^\S+@\S+\.\S+$/.test(fields.email.trim())
  ) {
    errors.email = "请输入有效的邮箱地址。";
  }
  if (!/^\d{6}$/.test(fields.verificationCode)) {
    errors.verificationCode = "验证码必须是 6 位数字。";
  }
  if (!fields.password) {
    errors.password = "请输入密码。";
  } else if (fields.password.length < 8) {
    errors.password = "密码至少需要 8 个字符。";
  } else if (fields.password.length > 50) {
    errors.password = "密码不能超过 50 个字符。";
  }
  if (fields.password !== fields.passwordConfirmation) {
    errors.passwordConfirmation = "两次输入的密码不一致。";
  }
  return errors;
}

export default function RegisterPage() {
  useDocumentTitle("注册");
  const { register, requestRegisterToken } = useAuth();
  const navigate = useNavigate();
  const [fields, setFields] = useState<RegisterFields>({
    email: "",
    verificationCode: "",
    password: "",
    passwordConfirmation: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [countdown, setCountdown] = useState(0);
  const [tokenPending, setTokenPending] = useState(false);
  const [registerPending, setRegisterPending] = useState(false);

  useEffect(() => {
    if (countdown <= 0) {
      return;
    }
    const timer = window.setInterval(
      () => setCountdown((current) => Math.max(0, current - 1)),
      1000,
    );
    return () => window.clearInterval(timer);
  }, [countdown]);

  const handleSendToken = async () => {
    const emailError = validate({
      ...fields,
      verificationCode: "000000",
      password: "validpass",
      passwordConfirmation: "validpass",
    }).email;
    if (emailError) {
      setFieldErrors({ email: emailError });
      return;
    }

    setTokenPending(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await requestRegisterToken(fields.email.trim());
      setCountdown(60);
      setMessage("验证码已发送，请检查邮箱。");
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "验证码发送失败，请稍后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setMessage(requestError.message);
    } finally {
      setTokenPending(false);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const localErrors = validate(fields);
    setFieldErrors(localErrors);
    setMessage(null);
    if (Object.keys(localErrors).length > 0) {
      return;
    }

    setRegisterPending(true);
    try {
      await register(
        fields.email.trim(),
        fields.password,
        fields.verificationCode,
      );
      navigate("/login", { replace: true, state: { registered: true } });
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "注册失败，请检查信息后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setMessage(requestError.message);
    } finally {
      setRegisterPending(false);
    }
  };

  return (
    <AuthPageShell
      title="创建 TinyLang 账户"
      description="注册后即可保存你的学习记录。"
      footer={
        <span>
          已有账户？
          <Link className="link link-primary ml-1" to="/login">
            返回登录
          </Link>
        </span>
      }
    >
      {message ? (
        <div className="alert alert-info mb-5 text-sm" role="status">
          {message}
        </div>
      ) : null}
      <form className="flex flex-col gap-2" noValidate onSubmit={handleSubmit}>
        <label className="form-control w-full" htmlFor="register-email">
          <span className="label pb-1">
            <span className="label-text font-medium">邮箱</span>
          </span>
          <input
            id="register-email"
            aria-invalid={Boolean(getFieldError(fieldErrors, "email"))}
            autoComplete="email"
            className="input input-bordered w-full"
            type="email"
            value={fields.email}
            onChange={(event) =>
              setFields({ ...fields, email: event.target.value })
            }
          />
          {getFieldError(fieldErrors, "email") ? (
            <span className="label pt-1 text-error">
              {getFieldError(fieldErrors, "email")}
            </span>
          ) : null}
        </label>
        <div className="form-control w-full">
          <label className="label pb-1" htmlFor="register-verification-code">
            <span className="label-text font-medium">注册验证码</span>
          </label>
          <div className="flex gap-2">
            <input
              id="register-verification-code"
              aria-invalid={Boolean(
                getFieldError(fieldErrors, "verificationCode"),
              )}
              autoComplete="one-time-code"
              className="input input-bordered min-w-0 flex-1"
              inputMode="numeric"
              maxLength={6}
              pattern="[0-9]{6}"
              value={fields.verificationCode}
              onChange={(event) =>
                setFields({
                  ...fields,
                  verificationCode: event.target.value
                    .replace(/\D/g, "")
                    .slice(0, 6),
                })
              }
            />
            <button
              className="btn btn-outline shrink-0"
              disabled={tokenPending || countdown > 0}
              type="button"
              onClick={() => void handleSendToken()}
            >
              {tokenPending ? (
                <span className="loading loading-spinner loading-sm" />
              ) : null}
              {countdown > 0 ? `${countdown}s 后重发` : "发送验证码"}
            </button>
          </div>
          {getFieldError(fieldErrors, "verificationCode") ? (
            <span className="label pt-1 text-error">
              {getFieldError(fieldErrors, "verificationCode")}
            </span>
          ) : null}
        </div>
        <PasswordField
          id="register-password"
          label="密码"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "password")}
          value={fields.password}
          onChange={(password) => setFields({ ...fields, password })}
        />
        <PasswordField
          id="register-password-confirmation"
          label="确认密码"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "passwordConfirmation")}
          value={fields.passwordConfirmation}
          onChange={(passwordConfirmation) =>
            setFields({ ...fields, passwordConfirmation })
          }
        />
        <button
          className="btn btn-primary w-full mt-4"
          disabled={registerPending}
          type="submit"
        >
          {registerPending ? (
            <span className="loading loading-spinner loading-sm" />
          ) : null}
          {registerPending ? "注册中" : "注册"}
        </button>
      </form>
    </AuthPageShell>
  );
}
