import { KeyRound, MailCheck, Trash2 } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
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
import { useResendCountdown } from "@/hooks/useResendCountdown";

interface AccountSecurityPanelProps {
  currentEmail: string;
}

interface NoticeProps {
  message: string | null;
  error: boolean;
}

function Notice({ message, error }: NoticeProps) {
  return message ? (
    <div
      className={`alert mt-4 text-sm ${error ? "alert-error" : "alert-info"}`}
      role={error ? "alert" : "status"}
    >
      {message}
    </div>
  ) : null;
}

function ChangeEmailForm({ currentEmail }: AccountSecurityPanelProps) {
  const { requestChangeEmailToken, changeEmail } = useAuth();
  const navigate = useNavigate();
  const countdown = useResendCountdown();
  const [newEmail, setNewEmail] = useState("");
  const [verificationCode, setVerificationCode] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [sending, setSending] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const validateNewEmail = () => {
    const error = validateEmail(newEmail);
    if (error) {
      return error;
    }
    if (newEmail.trim().toLowerCase() === currentEmail.toLowerCase()) {
      return "新邮箱不能与当前邮箱相同。";
    }
    return null;
  };

  const handleSendCode = async () => {
    const emailError = validateNewEmail();
    if (emailError) {
      setFieldErrors({ newEmail: emailError });
      return;
    }

    setSending(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await requestChangeEmailToken(newEmail.trim());
      countdown.start();
      setIsError(false);
      setMessage("验证码已发送到新邮箱。");
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "验证码发送失败，请稍后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setIsError(true);
      setMessage(requestError.message);
    } finally {
      setSending(false);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const errors: FieldErrors = {};
    const emailError = validateNewEmail();
    const codeError = validateVerificationCode(verificationCode);
    if (emailError) errors.newEmail = emailError;
    if (codeError) errors.verificationCode = codeError;
    setFieldErrors(errors);
    setMessage(null);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setSubmitting(true);
    try {
      await changeEmail(newEmail.trim(), verificationCode);
      setNewEmail("");
      setVerificationCode("");
      navigate("/login", {
        replace: true,
        state: { emailChanged: true },
      });
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "邮箱修改失败，请检查后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setIsError(true);
      setMessage(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form
      className="rounded-lg border border-base-300 bg-base-100 p-5 sm:p-6"
      noValidate
      onSubmit={handleSubmit}
    >
      <div className="flex items-center gap-3">
        <MailCheck aria-hidden="true" className="size-5" />
        <h3 className="text-lg font-semibold">修改邮箱</h3>
      </div>
      <p className="mt-2 text-sm text-base-content/65">
        当前邮箱：{currentEmail}
      </p>
      <Notice error={isError} message={message} />
      <div className="mt-5 flex flex-col gap-4">
        <label className="form-control" htmlFor="security-new-email">
          <span className="label pb-1 font-medium">新邮箱</span>
          <input
            id="security-new-email"
            aria-invalid={Boolean(getFieldError(fieldErrors, "newEmail"))}
            autoComplete="email"
            className="input input-bordered w-full"
            maxLength={100}
            type="email"
            value={newEmail}
            onChange={(event) => {
              setNewEmail(event.target.value);
              setFieldErrors((current) =>
                clearFieldError(current, "newEmail"),
              );
              setMessage(null);
            }}
          />
          {getFieldError(fieldErrors, "newEmail") ? (
            <span className="label pt-1 text-error">
              {getFieldError(fieldErrors, "newEmail")}
            </span>
          ) : null}
        </label>
        <div className="form-control">
          <label
            className="label pb-1 font-medium"
            htmlFor="security-email-code"
          >
            邮箱验证码
          </label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <input
              id="security-email-code"
              aria-invalid={Boolean(
                getFieldError(fieldErrors, "verificationCode"),
              )}
              autoComplete="one-time-code"
              className="input input-bordered min-w-0 w-full sm:flex-1"
              inputMode="numeric"
              maxLength={6}
              value={verificationCode}
              onChange={(event) => {
                setVerificationCode(
                  event.target.value.replace(/\D/g, "").slice(0, 6),
                );
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
      </div>
      <button
        className="btn btn-primary mt-5 w-full sm:w-auto"
        disabled={submitting || sending}
        type="submit"
      >
        {submitting ? (
          <span className="loading loading-spinner loading-sm" />
        ) : null}
        {submitting ? "修改中" : "确认修改邮箱"}
      </button>
    </form>
  );
}

function ResetPasswordForm({ currentEmail }: AccountSecurityPanelProps) {
  const { requestResetPasswordToken, resetPassword } = useAuth();
  const navigate = useNavigate();
  const countdown = useResendCountdown();
  const [verificationCode, setVerificationCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [sending, setSending] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handleSendCode = async () => {
    setSending(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await requestResetPasswordToken();
      countdown.start();
      setIsError(false);
      setMessage(`验证码已发送到 ${currentEmail}。`);
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
    const errors: FieldErrors = {};
    const passwordError = validateNewPassword(newPassword);
    const confirmationError = validatePasswordConfirmation(
      newPassword,
      passwordConfirmation,
    );
    const codeError = validateVerificationCode(verificationCode);
    if (passwordError) errors.newPassword = passwordError;
    if (confirmationError) errors.passwordConfirmation = confirmationError;
    if (codeError) errors.verificationCode = codeError;
    setFieldErrors(errors);
    setMessage(null);
    if (Object.keys(errors).length > 0) {
      return;
    }

    setSubmitting(true);
    try {
      await resetPassword(newPassword, verificationCode);
      setNewPassword("");
      setPasswordConfirmation("");
      setVerificationCode("");
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
    <form
      className="rounded-lg border border-base-300 bg-base-100 p-5 sm:p-6"
      noValidate
      onSubmit={handleSubmit}
    >
      <div className="flex items-center gap-3">
        <KeyRound aria-hidden="true" className="size-5" />
        <h3 className="text-lg font-semibold">重置密码</h3>
      </div>
      <p className="mt-2 text-sm text-base-content/65">
        成功后所有设备需重新登录。
      </p>
      <Notice error={isError} message={message} />
      <div className="mt-5 flex flex-col gap-4">
        <div className="form-control">
          <label
            className="label pb-1 font-medium"
            htmlFor="security-password-code"
          >
            邮箱验证码
          </label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <input
              id="security-password-code"
              aria-invalid={Boolean(
                getFieldError(fieldErrors, "verificationCode"),
              )}
              autoComplete="one-time-code"
              className="input input-bordered min-w-0 w-full sm:flex-1"
              inputMode="numeric"
              maxLength={6}
              value={verificationCode}
              onChange={(event) => {
                setVerificationCode(
                  event.target.value.replace(/\D/g, "").slice(0, 6),
                );
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
          id="security-new-password"
          label="新密码 (最小 8 位)"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "newPassword")}
          value={newPassword}
          onChange={(value) => {
            setNewPassword(value);
            setFieldErrors((current) =>
              clearFieldError(current, "newPassword"),
            );
          }}
        />
        <PasswordField
          id="security-password-confirmation"
          label="确认新密码"
          autoComplete="new-password"
          error={getFieldError(fieldErrors, "passwordConfirmation")}
          value={passwordConfirmation}
          onChange={(value) => {
            setPasswordConfirmation(value);
            setFieldErrors((current) =>
              clearFieldError(current, "passwordConfirmation"),
            );
          }}
        />
      </div>
      <button
        className="btn btn-primary mt-5 w-full sm:w-auto"
        disabled={submitting || sending}
        type="submit"
      >
        {submitting ? (
          <span className="loading loading-spinner loading-sm" />
        ) : null}
        {submitting ? "重置中" : "确认重置密码"}
      </button>
    </form>
  );
}

function DeleteAccountSection({ currentEmail }: AccountSecurityPanelProps) {
  const { requestDeleteAccountToken, deleteAccount } = useAuth();
  const navigate = useNavigate();
  const countdown = useResendCountdown();
  const [expanded, setExpanded] = useState(false);
  const [verificationCode, setVerificationCode] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const [sending, setSending] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const handleSendCode = async () => {
    setSending(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await requestDeleteAccountToken();
      countdown.start();
      setIsError(false);
      setMessage(`验证码已发送到 ${currentEmail}。`);
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

  const handleCancel = () => {
    setExpanded(false);
    setVerificationCode("");
    setFieldErrors({});
    setMessage(null);
    setIsError(false);
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const codeError = validateVerificationCode(verificationCode);
    if (codeError) {
      setFieldErrors({ verificationCode: codeError });
      setMessage(null);
      return;
    }

    setSubmitting(true);
    setFieldErrors({});
    setMessage(null);
    try {
      await deleteAccount(verificationCode);
      setVerificationCode("");
      navigate("/login", {
        replace: true,
        state: { accountDeleted: true },
      });
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "账号删除失败，请检查后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setIsError(true);
      setMessage(requestError.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section
      aria-labelledby="delete-account-title"
      className="rounded-lg border border-error/40 bg-error/5 p-5 sm:p-6"
    >
      <div className="flex flex-col items-start justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3 text-error">
            <Trash2 aria-hidden="true" className="size-5" />
            <h3 id="delete-account-title" className="text-lg font-semibold">
              删除账号
            </h3>
          </div>
          <p className="mt-2 text-sm leading-6 text-base-content/70">
            删除后账号资料将被匿名化，所有设备会立即退出，且无法自行恢复。
          </p>
        </div>
        {!expanded ? (
          <button
            className="btn btn-error w-full shrink-0 sm:w-auto"
            type="button"
            onClick={() => setExpanded(true)}
          >
            <Trash2 aria-hidden="true" className="size-4" />
            删除账号
          </button>
        ) : null}
      </div>

      {expanded ? (
        <form
          aria-label="删除账号确认"
          className="mt-5 border-t border-error/30 pt-5"
          noValidate
          onSubmit={handleSubmit}
        >
          <p className="text-sm leading-6 text-base-content/80">
            验证码将发送到 <span className="font-medium">{currentEmail}</span>。
            输入验证码并再次确认后，账号将被永久删除。
          </p>
          <Notice error={isError} message={message} />
          <div className="form-control mt-4">
            <label
              className="label pb-1 font-medium"
              htmlFor="security-delete-account-code"
            >
              邮箱验证码
            </label>
            <div className="flex flex-col gap-2 sm:flex-row">
              <input
                id="security-delete-account-code"
                aria-invalid={Boolean(
                  getFieldError(fieldErrors, "verificationCode"),
                )}
                autoComplete="one-time-code"
                className="input input-bordered min-w-0 w-full sm:w-1/3"
                disabled={submitting}
                inputMode="numeric"
                maxLength={6}
                value={verificationCode}
                onChange={(event) => {
                  setVerificationCode(
                    event.target.value.replace(/\D/g, "").slice(0, 6),
                  );
                  setFieldErrors((current) =>
                    clearFieldError(current, "verificationCode"),
                  );
                  setMessage(null);
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
          <div className="mt-5 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
            <button
              className="btn btn-ghost w-full sm:w-auto"
              disabled={sending || submitting}
              type="button"
              onClick={handleCancel}
            >
              取消
            </button>
            <button
              className="btn btn-error w-full sm:w-auto"
              disabled={sending || submitting}
              type="submit"
            >
              {submitting ? (
                <span className="loading loading-spinner loading-sm" />
              ) : (
                <Trash2 aria-hidden="true" className="size-4" />
              )}
              {submitting ? "删除中" : "永久删除账号"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

/** Renders current-user email, password, and account deletion workflows. */
export default function AccountSecurityPanel({
  currentEmail,
}: AccountSecurityPanelProps) {
  return (
    <section aria-labelledby="account-security-title" className="space-y-4">
      <header>
        <h2 id="account-security-title" className="text-2xl font-bold">
          账户安全
        </h2>
      </header>
      <div className="grid items-start gap-5 xl:grid-cols-2">
        <ChangeEmailForm currentEmail={currentEmail} />
        <ResetPasswordForm currentEmail={currentEmail} />
      </div>
      <DeleteAccountSection currentEmail={currentEmail} />
    </section>
  );
}
