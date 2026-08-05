import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
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
  validateNewPassword,
  validatePasswordConfirmation,
  validateVerificationCode,
} from "@/features/auth/authValidation";
import { useAuth } from "@/hooks/useAuth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
import { useResendCountdown } from "@/hooks/useResendCountdown";

interface ForgotPasswordFields {
  email: string;
  verificationCode: string;
  newPassword: string;
  passwordConfirmation: string;
}

function validate(fields: ForgotPasswordFields) {
  const errors: FieldErrors = {};
  const passwordError = validateNewPassword(fields.newPassword);
  const confirmationError = validatePasswordConfirmation(
    fields.newPassword,
    fields.passwordConfirmation,
  );
  const emailError = validateEmail(fields.email);
  const codeError = validateVerificationCode(fields.verificationCode);
  if (emailError) errors.email = emailError;
  if (codeError) errors.verificationCode = codeError;
  if (passwordError) errors.newPassword = passwordError;
  if (confirmationError) errors.passwordConfirmation = confirmationError;
  return errors;
}

export default function ForgotPasswordPage() {
  useDocumentTitle("忘记密码");
  const { requestForgotPasswordToken, forgotPassword } = useAuth();
  const navigate = useNavigate();
  const countdown = useResendCountdown();
  const [fields, setFields] = useState<ForgotPasswordFields>({
    email: "",
    verificationCode: "",
    newPassword: "",
    passwordConfirmation: "",
  });
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [sending, setSending] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handleSendCode = async () => {
    const emailError = validateEmail(fields.email);
    if (emailError) {
      setFieldErrors({ email: emailError });
      return;
    }

    setSending(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await requestForgotPasswordToken(fields.email.trim());
      countdown.start();
      setIsError(false);
      setMessage("若该邮箱已注册，验证码将发送至该邮箱。");
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "验证码发送失败，请稍后重试。",
      );
      setIsError(true);
      setMessage(requestError.message);
    } finally {
      setSending(false);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const errors = validate(fields);
    setFieldErrors(errors);
    setMessage(null);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setSubmitting(true);
    try {
      await forgotPassword(
        fields.email.trim(),
        fields.newPassword,
        fields.verificationCode,
      );
      setFields({
        email: "",
        verificationCode: "",
        newPassword: "",
        passwordConfirmation: "",
      });
      navigate("/login", {
        replace: true,
        state: { passwordReset: true },
      });
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "密码重置失败，请检查后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setIsError(true);
      setMessage(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthPageShell
      title="重置密码"
      description="使用注册邮箱接收验证码。"
      footer={
        <Link className="link link-primary" to="/login">
          返回登录
        </Link>
      }
    >
      {message ? (
        <div
          className={`alert mb-5 text-sm ${isError ? "alert-error" : "alert-info"}`}
          role={isError ? "alert" : "status"}
        >
          {message}
        </div>
      ) : null}
      <form className="flex flex-col gap-4" noValidate onSubmit={handleSubmit}>
        <label className="form-control w-full" htmlFor="forgot-password-email">
          <span className="label pb-1 font-medium">邮箱</span>
          <input
            id="forgot-password-email"
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
            <span className="label pt-1 text-error">
              {getFieldError(fieldErrors, "email")}
            </span>
          ) : null}
        </label>
        <div className="form-control w-full">
          <label
            className="label pb-1 font-medium"
            htmlFor="forgot-password-code"
          >
            邮箱验证码
          </label>
          <div className="flex gap-2">
            <input
              id="forgot-password-code"
              aria-invalid={Boolean(
                getFieldError(fieldErrors, "verificationCode"),
              )}
              autoComplete="one-time-code"
              className="input input-bordered min-w-0 flex-1"
              inputMode="numeric"
              maxLength={6}
              value={fields.verificationCode}
              onChange={(event) => {
                setFields({
                  ...fields,
                  verificationCode: event.target.value
                    .replace(/\D/g, "")
                    .slice(0, 6),
                });
                setFieldErrors((current) =>
                  clearFieldError(current, "verificationCode"),
                );
              }}
            />
            <button
              className="btn btn-outline shrink-0"
              disabled={sending || countdown.seconds > 0 || submitting}
              type="button"
              onClick={() => void handleSendCode()}
            >
              {sending ? (
                <span className="loading loading-spinner loading-sm" />
              ) : null}
              {countdown.seconds > 0
                ? `${countdown.seconds}s 后重发`
                : "发送验证码"}
            </button>
          </div>
          {getFieldError(fieldErrors, "verificationCode") ? (
            <span className="label pt-1 text-error">
              {getFieldError(fieldErrors, "verificationCode")}
            </span>
          ) : null}
        </div>
        <PasswordField
          id="forgot-password-new-password"
          label="新密码 (最小 8 位)"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "newPassword")}
          value={fields.newPassword}
          onChange={(newPassword) => {
            setFields({ ...fields, newPassword });
            setFieldErrors((current) =>
              clearFieldError(current, "newPassword"),
            );
          }}
        />
        <PasswordField
          id="forgot-password-confirmation"
          label="确认新密码"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "passwordConfirmation")}
          value={fields.passwordConfirmation}
          onChange={(passwordConfirmation) => {
            setFields({ ...fields, passwordConfirmation });
            setFieldErrors((current) =>
              clearFieldError(current, "passwordConfirmation"),
            );
          }}
        />
        <button
          className="btn btn-primary mt-4 w-full"
          disabled={submitting || sending}
          type="submit"
        >
          {submitting ? (
            <span className="loading loading-spinner loading-sm" />
          ) : null}
          {submitting ? "重置中" : "重置密码"}
        </button>
      </form>
    </AuthPageShell>
  );
}
