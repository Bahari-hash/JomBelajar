import { Link, useLocation, useNavigate } from "react-router-dom";
import { useState } from "react";
import AuthPageShell from "@/components/AuthPageShell";
import PasswordField from "@/components/PasswordField";
import {
  clearFieldError,
  getFieldError,
  toApiRequestError,
  type FieldErrors,
} from "@/features/auth/authErrors";
import {
  validateEmail,
  validateLoginPassword,
} from "@/features/auth/authValidation";
import { getSafeReturnTo } from "@/features/auth/returnTo";
import { useAuth } from "@/hooks/useAuth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

interface LoginFields {
  email: string;
  password: string;
}

function validate(fields: LoginFields): FieldErrors {
  const errors: FieldErrors = {};
  const emailError = validateEmail(fields.email);
  const passwordError = validateLoginPassword(fields.password);
  if (emailError) errors.email = emailError;
  if (passwordError) errors.password = passwordError;
  return errors;
}

export default function LoginPage() {
  useDocumentTitle("登录");
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [fields, setFields] = useState<LoginFields>({
    email: "",
    password: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(
    location.state?.registered
      ? "注册成功，请使用新账户登录。"
      : location.state?.accountDeleted
        ? "账号已删除，所有设备均已退出登录。"
        : location.state?.emailChanged
          ? "邮箱修改成功，请使用新邮箱重新登录。"
          : location.state?.passwordReset
            ? "密码已重置，请使用新密码登录。"
            : null,
  );
  const [pending, setPending] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const localErrors = validate(fields);
    setFieldErrors(localErrors);
    setMessage(null);
    if (Object.keys(localErrors).length > 0) {
      return;
    }

    setPending(true);
    try {
      await login(fields.email.trim(), fields.password);
      const target = getSafeReturnTo(
        new URLSearchParams(location.search).get("returnTo"),
        "/",
      );
      navigate(target, { replace: true });
    } catch (error) {
      const requestError = toApiRequestError(error, "登录失败，请稍后重试。");
      setFieldErrors(requestError.fieldErrors);
      setMessage(requestError.message);
      setFields((current) => ({ ...current, password: "" }));
    } finally {
      setPending(false);
    }
  };

  return (
    <AuthPageShell
      title="登录 TinyLang"
      description="登录后继续你的外语学习。"
      footer={
        <span>
          还没有账户？
          <Link className="link link-primary ml-1" to="/register">
            注册账户
          </Link>
        </span>
      }
    >
      {message ? (
        <div className="alert alert-info mb-5 text-sm" role="status">
          {message}
        </div>
      ) : null}
      <form className="flex flex-col gap-4" noValidate onSubmit={handleSubmit}>
        <label className="form-control w-full" htmlFor="login-email">
          <span className="label pb-1">
            <span className="label-text font-medium">邮箱</span>
          </span>
          <input
            id="login-email"
            aria-describedby={
              getFieldError(fieldErrors, "email")
                ? "login-email-error"
                : undefined
            }
            aria-invalid={Boolean(getFieldError(fieldErrors, "email"))}
            autoComplete="email"
            className="input input-bordered w-full"
            maxLength={100}
            type="email"
            value={fields.email}
            onChange={(event) => {
              setFields({ ...fields, email: event.target.value });
              setFieldErrors((current) => clearFieldError(current, "email"));
            }}
          />
          {getFieldError(fieldErrors, "email") ? (
            <span className="label pt-1 text-error" id="login-email-error">
              {getFieldError(fieldErrors, "email")}
            </span>
          ) : null}
        </label>
        <PasswordField
          id="login-password"
          label="密码 (最小 8 位)"
          autoComplete="current-password"
          error={getFieldError(fieldErrors, "password")}
          value={fields.password}
          onChange={(password) => {
            setFields({ ...fields, password });
            setFieldErrors((current) => clearFieldError(current, "password"));
          }}
        />
        <div className="text-right">
          <Link className="link link-primary text-sm" to="/forgot-password">
            忘记密码？
          </Link>
        </div>
        <button
          className="btn btn-primary w-full mt-4"
          disabled={pending}
          type="submit"
        >
          {pending ? (
            <span className="loading loading-spinner loading-sm" />
          ) : null}
          {pending ? "登录中" : "登录"}
        </button>
      </form>
    </AuthPageShell>
  );
}
