import { useRef, useState } from "react";
import { useDispatch } from "react-redux";
import { Eye, EyeOff, LoaderCircle, LogIn } from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import { ThemeMenu } from "@/components/ThemeMenu.jsx";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip.jsx";
import { getSafeRedirect } from "@/lib/safeRedirect.js";
import { authSession } from "@/services/authSession.js";
import { markApiSessionActive } from "@/services/baseApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { sessionAuthenticated, sessionForbidden } from "@/store/authSlice.js";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function validateLogin(email, password) {
  const errors = {};
  const normalizedEmail = email.trim();
  if (!normalizedEmail) errors.email = "请输入邮箱。";
  else if (normalizedEmail.length > 100)
    errors.email = "邮箱不能超过 100 个字符。";
  else if (!EMAIL_PATTERN.test(normalizedEmail))
    errors.email = "请输入有效的邮箱地址。";
  if (!password) errors.password = "请输入密码。";
  else if (password.length > 50) errors.password = "密码不能超过 50 个字符。";
  return errors;
}

function Login() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const errorRef = useRef(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState(location.state?.message ?? null);
  const [submitting, setSubmitting] = useState(false);

  const focusError = () =>
    requestAnimationFrame(() => errorRef.current?.focus());

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (submitting) return;

    const validationErrors = validateLogin(email, password);
    setFieldErrors(validationErrors);
    setFormError(null);
    if (Object.keys(validationErrors).length > 0) {
      focusError();
      return;
    }

    setSubmitting(true);
    try {
      const session = await authSession.login({
        email: email.trim(),
        password,
      });
      markApiSessionActive();
      dispatch(sessionAuthenticated(session.user));
      navigate(getSafeRedirect(location.state?.from), { replace: true });
    } catch (error) {
      if (error?.status === 403) {
        dispatch(sessionForbidden("仅管理员可以访问管理后台。"));
        navigate("/forbidden", { replace: true });
        return;
      }

      setFieldErrors(error?.fieldErrors ?? {});
      setFormError(
        error?.status === 401
          ? "邮箱或密码错误。"
          : getErrorMessage(error, "登录失败，请稍后重试。"),
      );
      focusError();
    } finally {
      setSubmitting(false);
    }
  };

  const handleEmailChange = (event) => {
    setEmail(event.target.value);
    setFieldErrors((current) => {
      const nextErrors = { ...current };
      delete nextErrors.email;
      return nextErrors;
    });
  };

  const handlePasswordChange = (event) => {
    setPassword(event.target.value);
    setFieldErrors((current) => {
      const nextErrors = { ...current };
      delete nextErrors.password;
      return nextErrors;
    });
  };

  return (
    <main className="grid min-h-dvh place-items-center bg-background px-4 py-10 sm:px-6">
      <div className="w-full max-w-sm">
        <div className="mb-5 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <img
              src={`${import.meta.env.BASE_URL}logo.png`}
              alt=""
              className="size-9 rounded-lg object-contain"
            />
            <div>
              <p className="text-sm font-semibold">JomBelajar</p>
              <p className="text-xs text-muted-foreground">管理后台</p>
            </div>
          </div>
          <ThemeMenu />
        </div>

        <section className="rounded-lg border bg-card p-5 text-card-foreground shadow-sm sm:p-6">
          <header className="mb-6">
            <h1 className="text-xl font-semibold">管理员登录</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              使用管理员账户继续。
            </p>
          </header>

          <form noValidate onSubmit={handleSubmit} className="space-y-4">
            {(formError || Object.keys(fieldErrors).length > 0) && (
              <Alert ref={errorRef} tabIndex={-1} variant="destructive">
                <AlertDescription>
                  {formError ?? "请检查标出的登录信息。"}
                </AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              <Label htmlFor="email">邮箱</Label>
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="username"
                maxLength={100}
                value={email}
                onChange={handleEmailChange}
                aria-invalid={Boolean(fieldErrors.email)}
                aria-describedby={fieldErrors.email ? "email-error" : undefined}
                disabled={submitting}
              />
              {fieldErrors.email?.[0] || fieldErrors.email ? (
                <p id="email-error" className="text-sm text-destructive">
                  {Array.isArray(fieldErrors.email)
                    ? fieldErrors.email[0]
                    : fieldErrors.email}
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">密码</Label>
              <div className="relative">
                <Input
                  id="password"
                  name="password"
                  type={passwordVisible ? "text" : "password"}
                  autoComplete="current-password"
                  maxLength={50}
                  value={password}
                  onChange={handlePasswordChange}
                  aria-invalid={Boolean(fieldErrors.password)}
                  aria-describedby={
                    fieldErrors.password ? "password-error" : undefined
                  }
                  disabled={submitting}
                  className="pr-10"
                />
                <Tooltip>
                  <TooltipTrigger asChild>
                    <button
                      type="button"
                      className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      aria-label={passwordVisible ? "隐藏密码" : "显示密码"}
                      onClick={() => setPasswordVisible((visible) => !visible)}
                      disabled={submitting}
                    >
                      {passwordVisible ? (
                        <EyeOff aria-hidden="true" className="size-4" />
                      ) : (
                        <Eye aria-hidden="true" className="size-4" />
                      )}
                    </button>
                  </TooltipTrigger>
                  <TooltipContent>
                    {passwordVisible ? "隐藏密码" : "显示密码"}
                  </TooltipContent>
                </Tooltip>
              </div>
              {fieldErrors.password?.[0] || fieldErrors.password ? (
                <p id="password-error" className="text-sm text-destructive">
                  {Array.isArray(fieldErrors.password)
                    ? fieldErrors.password[0]
                    : fieldErrors.password}
                </p>
              ) : null}
            </div>

            <Button type="submit" className="w-full" disabled={submitting}>
              {submitting ? (
                <LoaderCircle aria-hidden="true" className="animate-spin" />
              ) : (
                <LogIn aria-hidden="true" />
              )}
              {submitting ? "正在登录" : "登录"}
            </Button>
          </form>
        </section>
      </div>
    </main>
  );
}

export default Login;
