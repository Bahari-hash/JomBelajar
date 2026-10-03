import {
  Eye,
  Pencil,
  Play,
  RefreshCw,
  RotateCcw,
  Trash2,
  Upload,
} from "lucide-react";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table.jsx";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { AUDIO_STATUS_LABELS } from "@/services/audioContracts.js";

function formatDuration(value) {
  if (value === null) return "-";
  const minutes = Math.floor(value / 60);
  const seconds = Math.round(value % 60)
    .toString()
    .padStart(2, "0");
  return `${minutes}:${seconds}`;
}

function ActionButton({
  label,
  title = label,
  children,
  disabled = false,
  onClick,
}) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Button
          type="button"
          size="icon-sm"
          variant="ghost"
          aria-label={label}
          title={title}
          disabled={disabled}
          onClick={onClick}
        >
          {children}
        </Button>
      </TooltipTrigger>
      <TooltipContent>{title}</TooltipContent>
    </Tooltip>
  );
}

/** Compact audio library table with status-aware administrator commands. */
export function AudioTable({
  selected = {}, onSelect, onSelectPage,
  audioResources,
  pendingAction,
  onDetails,
  onPlay,
  onRename,
  onReprocess,
  onRetryUpload,
  onDelete,
}) {
  return (
    <div className="max-w-full min-w-0 overflow-x-auto rounded-lg border">
      <Table className="min-w-216 table-fixed">
        <TableHeader>
          <TableRow>
            {onSelect ? <TableHead className="w-12"><input type="checkbox" aria-label="全选本页音频"
              checked={audioResources.length > 0 && audioResources.every(x => selected[x.id])}
              ref={node => { if (node) node.indeterminate = audioResources.some(x => selected[x.id]) && !audioResources.every(x => selected[x.id]); }}
              onChange={e => onSelectPage(e.target.checked)} /></TableHead> : null}
            <TableHead className="w-[31%] pl-4">名称</TableHead>
            <TableHead className="w-28">状态</TableHead>
            <TableHead className="w-24">时长</TableHead>
            <TableHead>失败信息</TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-64 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {audioResources.map((audio) => {
            const pending = pendingAction?.audioResourceId === audio.id;
            return (
              <TableRow key={audio.id}>
                {onSelect ? <TableCell><input type="checkbox" aria-label={`选择音频 ${audio.name}`} checked={Boolean(selected[audio.id])} onChange={e => onSelect(audio, e.target.checked)} /></TableCell> : null}
                <TableCell
                  className="truncate pl-4 font-medium"
                  title={audio.name}
                >
                  {audio.name}
                </TableCell>
                <TableCell>
                  <Badge
                    variant={
                      audio.status === "Failed" ? "destructive" : "outline"
                    }
                  >
                    {AUDIO_STATUS_LABELS[audio.status]}
                  </Badge>
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDuration(audio.durationSeconds)}
                </TableCell>
                <TableCell
                  className="truncate text-muted-foreground"
                  title={audio.lastFailureCode ?? ""}
                >
                  {audio.lastFailureCode ?? "-"}
                </TableCell>
                <TableCell className="text-muted-foreground">
                  {formatDateTime(audio.updatedAt)}
                </TableCell>
                <TableCell className="pr-4">
                  <div className="flex h-8 items-center justify-end gap-1">
                    <ActionButton
                      label={`查看详情 ${audio.name}`}
                      onClick={() => onDetails(audio)}
                    >
                      <Eye aria-hidden="true" />
                    </ActionButton>
                    <ActionButton
                      label={`试听 ${audio.name}`}
                      title={
                        audio.status === "Ready"
                          ? `试听 ${audio.name}`
                          : "音频暂不可用"
                      }
                      disabled={audio.status !== "Ready" || pending}
                      onClick={() => onPlay(audio)}
                    >
                      <Play aria-hidden="true" />
                    </ActionButton>
                    <ActionButton
                      label={`重命名 ${audio.name}`}
                      disabled={pending}
                      onClick={() => onRename(audio)}
                    >
                      <Pencil aria-hidden="true" />
                    </ActionButton>
                    {audio.status === "Failed" ? (
                      <>
                        <ActionButton
                          label={`重新处理 ${audio.name}`}
                          disabled={pending}
                          onClick={() => onReprocess(audio)}
                        >
                          {pendingAction?.action === "reprocess" && pending ? (
                            <RefreshCw
                              aria-hidden="true"
                              className="animate-spin"
                            />
                          ) : (
                            <RotateCcw aria-hidden="true" />
                          )}
                        </ActionButton>
                        <ActionButton
                          label={`重新上传 ${audio.name}`}
                          disabled={pending}
                          onClick={() => onRetryUpload(audio)}
                        >
                          <Upload aria-hidden="true" />
                        </ActionButton>
                      </>
                    ) : null}
                    <ActionButton
                      label={`删除 ${audio.name}`}
                      disabled={pending}
                      onClick={() => onDelete(audio)}
                    >
                      <Trash2 aria-hidden="true" />
                    </ActionButton>
                  </div>
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
