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
  getArticleActions,
  getArticleStatusLabel,
} from "@/constants/articleStatus.js";
import { formatDateTime } from "@/lib/dateTime.js";

const ACTION_LABELS = { publish: "发布", unpublish: "下架", archive: "归档" };

/** Presents article summaries with stable columns and state-aware commands. */
export function ArticleTable({ articles, onAction }) {
  return (
    <div className="rounded-lg border">
      <Table className="min-w-248 table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead className="w-[34%] pl-4">文章</TableHead>
            <TableHead className="w-24">状态</TableHead>
            <TableHead className="w-[20%]">分类</TableHead>
            <TableHead className="w-32">作者</TableHead>
            <TableHead className="hidden w-40 lg:table-cell">
              发布时间
            </TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {articles.map((article) => {
            const actions = getArticleActions(article.status);
            return (
              <TableRow key={article.id}>
                <TableCell className="pl-4 whitespace-normal">
                  <div className="min-w-0">
                    <Link
                      className="block truncate font-medium hover:underline"
                      to={`/articles/${article.id}/preview`}
                      title={article.title}
                    >
                      {article.title}
                    </Link>
                    <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">
                      {article.summary ?? "无摘要"}
                    </p>
                  </div>
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      article.status === "Published" ? "default" : "outline"
                    }
                  >
                    {getArticleStatusLabel(article.status)}
                  </Badge>
                </TableCell>
                <TableCell className="whitespace-normal">
                  <div className="flex max-h-12 flex-wrap gap-1 overflow-hidden">
                    {article.categories.length ? (
                      article.categories.map((category) => (
                        <Badge key={category.id} variant="secondary">
                          {category.name}
                        </Badge>
                      ))
                    ) : (
                      <span className="text-muted-foreground">未分类</span>
                    )}
                  </div>
                </TableCell>
                <TableCell
                  className="truncate"
                  title={article.author.nickname ?? "未设置昵称"}
                >
                  {article.author.nickname ?? "未设置昵称"}
                </TableCell>
                <TableCell className="hidden text-muted-foreground lg:table-cell">
                  {formatDateTime(article.publishedAt)}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDateTime(article.updatedAt)}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`管理文章 ${article.title}`}
                      >
                        <MoreHorizontal aria-hidden="true" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end" className="w-40">
                      <DropdownMenuLabel>文章操作</DropdownMenuLabel>
                      <DropdownMenuItem asChild>
                        <Link to={`/articles/${article.id}/preview`}>
                          完整预览
                        </Link>
                      </DropdownMenuItem>
                      {actions.includes("edit") ? (
                        <DropdownMenuItem asChild>
                          <Link to={`/articles/${article.id}/edit`}>编辑</Link>
                        </DropdownMenuItem>
                      ) : null}
                      {actions.some((action) => ACTION_LABELS[action]) ? (
                        <DropdownMenuSeparator />
                      ) : null}
                      {actions
                        .filter((action) => ACTION_LABELS[action])
                        .map((action) => (
                          <DropdownMenuItem
                            key={action}
                            variant={
                              action === "archive" ? "destructive" : undefined
                            }
                            onSelect={() => onAction(action, article)}
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
