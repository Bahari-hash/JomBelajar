import { MoreHorizontal } from "lucide-react";
import { Link } from "react-router-dom";
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
import {
  getPaperActions,
  getPaperStatusLabel,
  isPaperEditable,
} from "@/constants/paperStatus.js";
import { formatDateTime } from "@/lib/dateTime.js";

const ACTION_LABELS = {
  publish: "发布",
  unpublish: "下架",
  archive: "归档",
  delete: "永久删除",
};

/** Compact administrator table for Paper lifecycle and audit summaries. */
export function PaperTable({ papers, onAction }) {
  return (
    <div className="max-w-full min-w-0 overflow-x-auto rounded-lg border">
      <Table className="min-w-300 table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead className="w-56 pl-4">标题</TableHead>
            <TableHead className="w-20">语言</TableHead>
            <TableHead className="w-24">状态</TableHead>
            <TableHead className="w-28">题目</TableHead>
            <TableHead className="w-28">分值</TableHead>
            <TableHead className="w-28">测验次数</TableHead>
            <TableHead className="w-52">审计</TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {papers.map((paper) => {
            const actions = getPaperActions(paper);
            const editable = isPaperEditable(paper);
            return (
              <TableRow key={paper.id}>
                <TableCell className="pl-4">
                  <Link
                    className="block truncate font-medium hover:underline"
                    title={paper.title}
                    to={`/papers/${paper.id}`}
                  >
                    {paper.title || "未命名试卷"}
                  </Link>
                  <span className="text-xs text-muted-foreground">
                    {paper.status === "Archived"
                      ? "归档内容只读"
                      : editable
                        ? "可编辑"
                        : "只读"}
                  </span>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {paper.languageTag}
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      paper.status === "Archived" ? "secondary" : "outline"
                    }
                  >
                    {getPaperStatusLabel(paper.status)}
                  </Badge>
                </TableCell>
                <TableCell>{paper.questionCount} 题</TableCell>
                <TableCell>
                  {paper.totalScore} / {paper.passingScore}
                </TableCell>
                <TableCell>
                  {paper.attemptCount > 0 ? (
                    <span className="text-muted-foreground">
                      {paper.attemptCount} 次 · 内容已锁定
                    </span>
                  ) : (
                    "0 次"
                  )}
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  <span
                    className="block truncate"
                    title={paper.createdBy.nickname ?? paper.createdBy.id}
                  >
                    创建：{paper.createdBy.nickname ?? "未设置昵称"}
                  </span>
                  <span
                    className="block truncate"
                    title={paper.lastEditor.nickname ?? paper.lastEditor.id}
                  >
                    修改：{paper.lastEditor.nickname ?? "未设置昵称"}
                  </span>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDateTime(paper.updatedAt)}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`管理试卷 ${paper.title || "未命名试卷"}`}
                      >
                        <MoreHorizontal aria-hidden="true" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuLabel>试卷操作</DropdownMenuLabel>
                      <DropdownMenuItem asChild>
                        <Link to={`/papers/${paper.id}`}>
                          {editable ? "查看与编辑" : "查看详情"}
                        </Link>
                      </DropdownMenuItem>
                      {actions.length ? <DropdownMenuSeparator /> : null}
                      {actions.map((action) => (
                        <DropdownMenuItem
                          key={action}
                          variant={
                            action === "archive" || action === "delete"
                              ? "destructive"
                              : undefined
                          }
                          onSelect={() => onAction(action, paper)}
                        >
                          {ACTION_LABELS[action]}
                        </DropdownMenuItem>
                      ))}
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
