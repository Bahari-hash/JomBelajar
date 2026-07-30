import { LoaderCircle } from "lucide-react";

/** Stable full-viewport fallback while the current tab restores its admin session. */
export function SessionLoading() {
  return (
    <main className="flex min-h-dvh items-center justify-center bg-background px-4">
      <div
        className="flex items-center gap-3 text-sm text-muted-foreground"
        role="status"
      >
        <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />
        正在恢复登录状态
      </div>
    </main>
  );
}
