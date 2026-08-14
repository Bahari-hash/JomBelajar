import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Label } from "@/components/ui/label.jsx";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";
import { ROLE_OPTIONS } from "@/services/roles.js";
import {
  useBanUserMutation,
  useRevokeUserSessionsMutation,
  useUnbanUserMutation,
  useUpdateUserRoleMutation,
} from "@/services/usersApi.js";

function getDialogCopy(action, user, isSelf) {
  const identity = user.email;
  switch (action) {
    case "ban":
      return {
        title: "封禁用户",
        description: `封禁 ${identity} 后，其现有会话将立即失效。`,
        submit: "确认封禁",
      };
    case "unban":
      return {
        title: "解除封禁",
        description: `解除 ${identity} 的封禁不会让其自动重新登录。`,
        submit: "解除封禁",
      };
    case "role":
      return {
        title: "修改用户角色",
        description: `修改 ${identity} 的角色会使其现有会话立即失效。`,
        submit: "保存角色",
      };
    case "revoke":
      return {
        title: isSelf ? "撤销自己的全部会话" : "撤销全部会话",
        description: isSelf
          ? "该操作会立即结束包括当前会话在内的全部登录，并返回登录页。"
          : `撤销 ${identity} 的全部会话，但不会封禁账户或修改角色。`,
        submit: isSelf ? "撤销并退出" : "确认撤销",
      };
    default:
      return { title: "用户操作", description: "", submit: "确认" };
  }
}

/** Executes one user mutation at a time while preserving confirmation context on failure. */
export function UserActionDialog({
  action,
  user,
  currentUserId,
  onClose,
  onSelfRevoked,
}) {
  const [reason, setReason] = useState("");
  const [role, setRole] = useState(user.role);
  const [error, setError] = useState(null);
  const [banUser, banState] = useBanUserMutation();
  const [unbanUser, unbanState] = useUnbanUserMutation();
  const [updateRole, roleState] = useUpdateUserRoleMutation();
  const [revokeSessions, revokeState] = useRevokeUserSessionsMutation();
  const isSelf = user.id === currentUserId;
  const copy = getDialogCopy(action, user, isSelf);
  const pending =
    banState.isLoading ||
    unbanState.isLoading ||
    roleState.isLoading ||
    revokeState.isLoading;
  const reasonError = error?.fieldErrors?.reason?.[0];

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (pending) return;
    const normalizedReason = reason.trim();
    if (action === "ban" && !normalizedReason) {
      setError({
        detail: "请输入封禁原因。",
        fieldErrors: { reason: ["请输入封禁原因。"] },
      });
      return;
    }
    if (action === "ban" && normalizedReason.length > 500) {
      setError({
        detail: "封禁原因不能超过 500 个字符。",
        fieldErrors: { reason: ["封禁原因不能超过 500 个字符。"] },
      });
      return;
    }

    setError(null);
    try {
      if (action === "ban") {
        await banUser({ userId: user.id, reason: normalizedReason }).unwrap();
      } else if (action === "unban") {
        await unbanUser({ userId: user.id }).unwrap();
      } else if (action === "role") {
        await updateRole({ userId: user.id, role }).unwrap();
      } else if (action === "revoke") {
        await revokeSessions({ userId: user.id }).unwrap();
        if (isSelf) {
          onSelfRevoked();
          return;
        }
      }
      onClose();
    } catch (requestError) {
      setError(requestError);
    }
  };

  return (
    <AlertDialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <AlertDialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit}>
          <AlertDialogHeader>
            <AlertDialogTitle>{copy.title}</AlertDialogTitle>
            <AlertDialogDescription>{copy.description}</AlertDialogDescription>
          </AlertDialogHeader>

          <div className="mt-4 space-y-4">
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {getErrorMessage(error, "操作失败，请重试。")}
                </AlertDescription>
              </Alert>
            ) : null}

            {action === "ban" ? (
              <div className="space-y-2">
                <Label htmlFor="ban-reason">封禁原因</Label>
                <Textarea
                  id="ban-reason"
                  value={reason}
                  maxLength={500}
                  rows={4}
                  onChange={(event) => {
                    setReason(event.target.value);
                    setError(null);
                  }}
                  aria-invalid={Boolean(reasonError)}
                  aria-describedby={
                    reasonError ? "ban-reason-error" : "ban-reason-help"
                  }
                  disabled={pending}
                />
                <div className="flex justify-between gap-3 text-xs text-muted-foreground">
                  <span
                    id={reasonError ? "ban-reason-error" : "ban-reason-help"}
                  >
                    {reasonError ?? "该原因将保存在用户管理记录中。"}
                  </span>
                  <span className="shrink-0 tabular-nums">
                    {reason.length}/500
                  </span>
                </div>
              </div>
            ) : null}

            {action === "role" ? (
              <div className="space-y-2">
                <Label id="role-update-label">新角色</Label>
                <Select value={role} onValueChange={setRole} disabled={pending}>
                  <SelectTrigger
                    className="w-full"
                    aria-labelledby="role-update-label"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ROLE_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            ) : null}
          </div>

          <AlertDialogFooter className="mt-4">
            <AlertDialogCancel type="button" disabled={pending}>
              取消
            </AlertDialogCancel>
            <Button
              type="submit"
              variant={action === "role" ? "default" : "destructive"}
              disabled={pending || (action === "role" && role === user.role)}
            >
              {pending ? "正在处理" : copy.submit}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  );
}
