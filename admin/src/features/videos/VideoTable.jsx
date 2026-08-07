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
  getProcessingStatusLabel,
  getPublicationStatusLabel,
  getVideoActions,
  getVideoFailureMessage,
} from "@/constants/videoStatus.js";
import { formatDateTime } from "@/lib/dateTime.js";

const ACTION_LABELS = {
  publish: "发布",
  unpublish: "下架",
  retry: "重试处理",
  archive: "归档",
};

/** Stable administrator table for global video state and commands. */
export function VideoTable({ videos, onAction }) {
  return (
    <div className="max-w-full min-w-0 overflow-x-auto rounded-lg border">
      <Table className="min-w-296 table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead className="w-[19%] pl-4">标题</TableHead>
            <TableHead className="w-28">处理状态</TableHead>
            <TableHead className="w-24">发布状态</TableHead>
            <TableHead className="w-[15%]">分类</TableHead>
            <TableHead className="w-[14%]">审计</TableHead>
            <TableHead>任务摘要</TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {videos.map((video) => {
            const actions = getVideoActions(video);
            return (
              <TableRow key={video.id}>
                <TableCell className="pl-4">
                  <Link
                    className="block truncate font-medium hover:underline"
                    title={video.title}
                    to={`/videos/${video.id}`}
                  >
                    {video.title}
                  </Link>
                  {/* <span className="text-xs text-muted-foreground">
                    {video.originalLanguage}
                  </span> */}
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      video.processingStatus === "Failed"
                        ? "destructive"
                        : "outline"
                    }
                  >
                    {getProcessingStatusLabel(video.processingStatus)}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Badge variant="secondary">
                    {getPublicationStatusLabel(video.publicationStatus)}
                  </Badge>
                </TableCell>
                <TableCell
                  className="truncate"
                  title={video.categories.map(({ name }) => name).join("、")}
                >
                  {video.categories.length
                    ? video.categories.map(({ name }) => name).join("、")
                    : "未分类"}
                </TableCell>
                <TableCell
                  className="truncate text-sm text-muted-foreground"
                  title={`创建：${video.createdBy.nickname ?? video.createdBy.id}；最后修改：${video.lastEditor.nickname ?? video.lastEditor.id}`}
                >
                  <span className="block truncate">
                    创建：{video.createdBy.nickname ?? "未设置昵称"}
                  </span>
                  <span className="block truncate">
                    修改：{video.lastEditor.nickname ?? "未设置昵称"}
                  </span>
                </TableCell>
                <TableCell
                  className="truncate text-sm text-muted-foreground"
                  title={
                    video.failureCode
                      ? getVideoFailureMessage(video.failureCode)
                      : ""
                  }
                >
                  {video.failureCode
                    ? getVideoFailureMessage(video.failureCode)
                    : video.latestJob
                      ? `任务 ${video.latestJob.status}，第 ${video.latestJob.attemptCount} 次尝试`
                      : "暂无任务"}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDateTime(video.updatedAt)}
                </TableCell>
                <TableCell className="pr-4 text-right">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`管理视频 ${video.title}`}
                      >
                        <MoreHorizontal aria-hidden="true" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuLabel>视频操作</DropdownMenuLabel>
                      <DropdownMenuItem asChild>
                        <Link to={`/videos/${video.id}`}>查看与编辑</Link>
                      </DropdownMenuItem>
                      {actions.length ? <DropdownMenuSeparator /> : null}
                      {actions.map((action) => (
                        <DropdownMenuItem
                          key={action}
                          variant={
                            action === "archive" ? "destructive" : undefined
                          }
                          onSelect={() => onAction(action, video)}
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
