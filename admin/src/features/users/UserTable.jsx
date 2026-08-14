import { MoreHorizontal } from "lucide-react";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu.jsx";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { getRoleLabel } from "@/services/roles.js";

function getUserStatus(user) {
  if (user.isDeleted) return { label: "已删除", variant: "secondary" };
  if (user.isBanned) return { label: "已封禁", variant: "destructive" };
  return { label: "正常", variant: "outline" };
}

/** Presents the administrator list DTO with stable columns and accurate row actions. */
export function UserTable({ users, currentUserId, onAction }) {
  return (
    <div className="rounded-lg border">
      <Table className="min-w-232">
        <TableHeader>
          <TableRow>
            <TableHead className="w-[26%] pl-4">用户</TableHead>
            <TableHead className="w-28">角色</TableHead>
            <TableHead className="w-24">状态</TableHead>
            <TableHead className="w-24 text-right">活动会话</TableHead>
            <TableHead className="hidden w-40 lg:table-cell">
              最近登录
            </TableHead>
            <TableHead className="hidden w-40 xl:table-cell">
              创建时间
            </TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {users.map((user) => {
            const status = getUserStatus(user);
            const isSelf = user.id === currentUserId;
            return (
              <TableRow key={user.id}>
                <TableCell className="max-w-72 pl-4 whitespace-normal">
                  <div className="min-w-0">
                    <p className="truncate font-medium" title={user.email}>
                      {user.email}
                      {isSelf ? (
                        <span className="ml-1 text-xs text-muted-foreground">
                          （当前）
                        </span>
                      ) : null}
                    </p>
                    {user.nickname ? (
                      <p
                        className="truncate text-xs text-muted-foreground"
                        title={user.nickname}
                      >
                        {user.nickname}
                      </p>
                    ) : null}
                  </div>
                </TableCell>
                <TableCell>
                  <Badge variant="secondary">{getRoleLabel(user.role)}</Badge>
                </TableCell>
                <TableCell>
                  <Badge variant={status.variant}>{status.label}</Badge>
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {user.activeSessionCount}
                </TableCell>
                <TableCell className="hidden text-muted-foreground lg:table-cell">
                  {formatDateTime(user.lastLoginAt)}
                </TableCell>
                <TableCell className="hidden text-muted-foreground xl:table-cell">
                  {formatDateTime(user.createdAt)}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`管理用户 ${user.email}`}
                      >
                        <MoreHorizontal aria-hidden="true" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" className="w-48">
                      <DropdownMenuLabel>用户操作</DropdownMenuLabel>
                      <DropdownMenuItem
                        onSelect={() => onAction("detail", user)}
                      >
                        查看详情
                      </DropdownMenuItem>
                      {!user.isDeleted ? <DropdownMenuSeparator /> : null}
                      {!user.isDeleted && !user.isBanned ? (
                        <DropdownMenuItem
                          disabled={isSelf}
                          title={isSelf ? "管理员不能封禁自己" : undefined}
                          onSelect={() => onAction("ban", user)}
                        >
                          {isSelf ? "不能封禁当前账户" : "封禁用户"}
                        </DropdownMenuItem>
                      ) : null}
                      {!user.isDeleted && user.isBanned ? (
                        <DropdownMenuItem
                          onSelect={() => onAction("unban", user)}
                        >
                          解除封禁
                        </DropdownMenuItem>
                      ) : null}
                      {!user.isDeleted ? (
                        <DropdownMenuItem
                          disabled={isSelf}
                          title={
                            isSelf ? "管理员不能修改自己的角色" : undefined
                          }
                          onSelect={() => onAction("role", user)}
                        >
                          {isSelf ? "不能修改当前角色" : "修改角色"}
                        </DropdownMenuItem>
                      ) : null}
                      {!user.isDeleted ? (
                        <DropdownMenuItem
                          variant="destructive"
                          onSelect={() => onAction("revoke", user)}
                        >
                          {isSelf ? "撤销自己的全部会话" : "撤销全部会话"}
                        </DropdownMenuItem>
                      ) : null}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
