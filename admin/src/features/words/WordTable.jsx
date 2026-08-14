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
  getPartOfSpeechLabel,
  getWordActions,
  getWordStatusLabel,
} from "@/constants/wordStatus.js";
import { formatDateTime } from "@/lib/dateTime.js";

const ACTION_LABELS = {
  publish: "发布",
  unpublish: "下架",
  archive: "归档",
  delete: "永久删除",
};

/** Compact administrator table for global word content and lifecycle commands. */
export function WordTable({ words, onAction }) {
  return (
    <div className="max-w-full min-w-0 overflow-x-auto rounded-lg border">
      <Table className="min-w-280 table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead className="w-44 pl-4">词头</TableHead>
            <TableHead className="w-48">词性与释义</TableHead>
            <TableHead className="w-24">状态</TableHead>
            <TableHead className="w-32">内容数量</TableHead>
            <TableHead className="w-48">审计</TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {words.map((word) => {
            const actions = getWordActions(word);
            const editable =
              word.status === "Draft" || word.status === "Unpublished";
            return (
              <TableRow key={word.id}>
                <TableCell className="pl-4">
                  <Link
                    className="block truncate font-medium hover:underline"
                    title={word.headword}
                    to={`/words/${word.id}`}
                  >
                    {word.headword}
                  </Link>
                </TableCell>
                <TableCell>
                  <span className="block truncate text-sm">
                    {word.primaryPartOfSpeech
                      ? getPartOfSpeechLabel(word.primaryPartOfSpeech)
                      : "未设置词性"}
                  </span>
                  <span
                    className="block truncate text-xs text-muted-foreground"
                    title={word.primaryDefinition ?? ""}
                  >
                    {word.primaryDefinition ?? "暂无释义"}
                  </span>
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      word.status === "Archived" ? "secondary" : "outline"
                    }
                  >
                    {getWordStatusLabel(word.status)}
                  </Badge>
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  {word.senseCount} 释义 · {word.exampleCount} 例句
                  <br />
                  {word.pronunciationCount} 发音
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  <span
                    className="block truncate"
                    title={word.createdBy.nickname ?? word.createdBy.id}
                  >
                    创建：{word.createdBy.nickname ?? "未设置昵称"}
                  </span>
                  <span
                    className="block truncate"
                    title={word.lastEditor.nickname ?? word.lastEditor.id}
                  >
                    修改：{word.lastEditor.nickname ?? "未设置昵称"}
                  </span>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDateTime(word.updatedAt)}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`管理单词 ${word.headword}`}
                      >
                        <MoreHorizontal aria-hidden="true" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuLabel>单词操作</DropdownMenuLabel>
                      <DropdownMenuItem asChild>
                        <Link to={`/words/${word.id}`}>
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
                          onSelect={() => onAction(action, word)}
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
