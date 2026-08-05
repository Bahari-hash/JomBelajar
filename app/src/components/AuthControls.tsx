import { LogIn, LogOut, UserRound } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth";
import UserAvatar from "@/components/UserAvatar";
import { cn } from "@/lib/utils";

interface AuthControlsProps {
  mobile?: boolean;
  onNavigate?: () => void;
}

/** Renders anonymous actions or the authenticated profile/logout menu. */
export default function AuthControls({
  mobile = false,
  onNavigate,
}: AuthControlsProps) {
  const { status, profile, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const firstItemRef = useRef<HTMLAnchorElement>(null);
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    setOpen(false);
  }, [location.key]);

  useEffect(() => {
    if (!open) {
      return;
    }
    firstItemRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
    };
    const handlePointerDown = (event: PointerEvent) => {
      if (
        event.target instanceof Node &&
        !menuRef.current?.contains(event.target)
      ) {
        setOpen(false);
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    document.addEventListener("pointerdown", handlePointerDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [open]);

  if (status === "loading") {
    return (
      <div
        aria-label="账户状态加载中"
        className="skeleton h-9 w-24"
        role="status"
      />
    );
  }

  if (status === "anonymous") {
    return (
      <div
        className={cn(
          "flex items-center gap-2",
          mobile && "w-full flex-col items-stretch",
        )}
      >
        <Link className="btn btn-ghost btn-sm" to="/login" onClick={onNavigate}>
          <LogIn aria-hidden="true" className="size-4" />
          登录
        </Link>
        <Link
          className="btn btn-primary btn-sm"
          to="/register"
          onClick={onNavigate}
        >
          注册
        </Link>
      </div>
    );
  }

  const displayName = profile?.nickname || profile?.email || "我的账户";

  const handleLogout = async () => {
    setLoggingOut(true);
    try {
      await logout();
      setOpen(false);
      onNavigate?.();
      navigate("/", { replace: true, state: { notice: "已安全退出登录。" } });
    } catch {
      // The provider clears local credentials even when the server logout fails.
    } finally {
      setLoggingOut(false);
    }
  };

  return (
    <div ref={menuRef} className={cn("relative", mobile && "w-full")}>
      <button
        ref={triggerRef}
        aria-expanded={open}
        aria-haspopup="menu"
        className={cn(
          "btn btn-ghost btn-sm h-10 max-w-48 justify-start gap-2",
          mobile && "w-full max-w-none",
        )}
        type="button"
        onClick={() => setOpen((current) => !current)}
      >
        <UserAvatar
          className="size-7"
          name={displayName}
          url={profile?.avatarUrl}
        />
        <span className="min-w-0 truncate">{displayName}</span>
      </button>
      {open ? (
        <div
          aria-label="用户菜单"
          className={cn(
            "absolute z-50 rounded-lg border border-base-300 bg-base-100/95 p-2 shadow-lg backdrop-blur",
            mobile
              ? "bottom-full left-0 right-0 mb-2"
              : "right-0 top-full mt-2 w-64",
          )}
          role="menu"
        >
          <Link
            ref={firstItemRef}
            className="flex min-h-10 items-center gap-2 rounded-md px-3 text-sm hover:bg-base-200"
            role="menuitem"
            to="/profile"
            onClick={() => {
              setOpen(false);
              onNavigate?.();
            }}
          >
            <UserRound aria-hidden="true" className="size-4" />
            个人资料
          </Link>
          <button
            className="flex min-h-10 w-full items-center gap-2 rounded-md px-3 text-left text-sm text-error hover:bg-base-200 disabled:opacity-60"
            disabled={loggingOut}
            role="menuitem"
            type="button"
            onClick={() => void handleLogout()}
          >
            {loggingOut ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              <LogOut aria-hidden="true" className="size-4" />
            )}
            {loggingOut ? "退出中" : "退出登录"}
          </button>
        </div>
      ) : null}
    </div>
  );
}
