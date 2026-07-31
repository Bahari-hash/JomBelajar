import { useState } from "react";
import { Check, Copy, RotateCcw } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { getRoleLabel } from "@/services/roles.js";
import { useGetAdminUserQuery } from "@/services/usersApi.js";

function DetailRow({ label, children }) {
  return (
    <div className="grid gap-1 border-b py-3 sm:grid-cols-[8rem_minmax(0,1fr)] sm:gap-3">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="min-w-0 wrap-break-word text-sm">{children ?? "未设置"}</dd>
    </div>
  );
}

/** Loads and displays only the server's administrator detail DTO. */
export function UserDetailSheet({ userId, open, onOpenChange }) {
  const [copied, setCopied] = useState(false);
  const {
    data: user,
    error,
    isLoading,
    refetch,
  } = useGetAdminUserQuery(userId, {
    skip: !open || !userId,
  });

  const handleCopyId = async () => {
    if (!user?.id) return;
    try {
      await navigator.clipboard.writeText(user.id);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      setCopied(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[min(32rem,92vw)] overflow-y-auto sm:max-w-lg">
        <SheetHeader className="border-b">
          <SheetTitle>用户详情</SheetTitle>
          <SheetDescription>账户、状态和会话摘要</SheetDescription>
        </SheetHeader>

        <div className="px-4 pb-6">
          {isLoading ? (
            <div
              className="space-y-3 py-4"
              role="status"
              aria-label="正在加载用户详情"
            >
              {Array.from({ length: 8 }, (_, index) => (
                <Skeleton key={index} className="h-10 w-full" />
              ))}
            </div>
          ) : error ? (
            <Alert variant="destructive" className="mt-4">
              <AlertDescription className="flex items-center justify-between gap-3">
                <span>{getErrorMessage(error, "用户详情加载失败。")}</span>
                <Button size="sm" variant="outline" onClick={refetch}>
                  <RotateCcw aria-hidden="true" />
                  重试
                </Button>
              </AlertDescription>
            </Alert>
          ) : user ? (
            <dl>
              <DetailRow label="用户 ID">
                <span className="flex min-w-0 items-center gap-2">
                  <code className="min-w-0 truncate text-xs" title={user.id}>
                    {user.id}
                  </code>
                  <Button
                    size="icon-sm"
                    variant="ghost"
                    onClick={handleCopyId}
                    aria-label="复制用户 ID"
                  >
                    {copied ? (
                      <Check aria-hidden="true" />
                    ) : (
                      <Copy aria-hidden="true" />
                    )}
                  </Button>
                </span>
              </DetailRow>
              <DetailRow label="用户名">{user.username}</DetailRow>
              <DetailRow label="邮箱">{user.email}</DetailRow>
              <DetailRow label="昵称">{user.nickname}</DetailRow>
              <DetailRow label="角色">
                <Badge variant="secondary">{getRoleLabel(user.role)}</Badge>
              </DetailRow>
              <DetailRow label="账户状态">
                {user.isDeleted ? "已删除" : user.isBanned ? "已封禁" : "正常"}
              </DetailRow>
              {user.isBanned ? (
                <>
                  <DetailRow label="封禁时间">
                    {formatDateTime(user.bannedAt)}
                  </DetailRow>
                  <DetailRow label="封禁原因">{user.bannedReason}</DetailRow>
                </>
              ) : null}
              {user.isDeleted ? (
                <DetailRow label="删除时间">
                  {formatDateTime(user.deletedAt)}
                </DetailRow>
              ) : null}
              <DetailRow label="个人简介">
                <span className="whitespace-pre-wrap">
                  {user.bio ?? "未设置"}
                </span>
              </DetailRow>
              <DetailRow label="活动会话">{user.activeSessionCount}</DetailRow>
              <DetailRow label="最近登录">
                {formatDateTime(user.lastLoginAt)}
              </DetailRow>
              <DetailRow label="创建时间">
                {formatDateTime(user.createdAt)}
              </DetailRow>
              <DetailRow label="更新时间">
                {formatDateTime(user.updatedAt)}
              </DetailRow>
            </dl>
          ) : null}
        </div>
      </SheetContent>
    </Sheet>
  );
}
