import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, RotateCcw, Search } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group.jsx";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import {
  AUDIO_RESOURCE_STATUSES,
  AUDIO_STATUS_LABELS,
} from "@/services/audioContracts.js";
import { useGetAdminAudioResourcesQuery } from "@/services/audioApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

const PAGE_SIZE = 20;

/** Selects one shared audio resource without exposing library management actions. */
export function AudioResourcePickerDialog({
  open,
  value,
  onSelect,
  onOpenChange,
}) {
  const [draftKeyword, setDraftKeyword] = useState("");
  const [keyword, setKeyword] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(value);
  const query = useGetAdminAudioResourcesQuery(
    { page, pageSize: PAGE_SIZE, keyword, status },
    { skip: !open },
  );

  useEffect(() => {
    if (open) setSelected(value);
  }, [open, value]);

  const close = () => onOpenChange(false);
  const submitSearch = (event) => {
    event.preventDefault();
    setKeyword(draftKeyword.trim());
    setPage(1);
  };
  const selectResource = (audioResourceId) => {
    const audio = query.data?.items.find((item) => item.id === audioResourceId);
    if (audio) setSelected(audio);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>选择音频资源</DialogTitle>
          <DialogDescription>
            文章可以关联处于任意处理状态的音频资源。
          </DialogDescription>
        </DialogHeader>

        <form
          className="grid gap-3 border-y py-3 items-start sm:grid-cols-[minmax(0,1fr)_9rem_auto]"
          role="search"
          onSubmit={submitSearch}
        >
          <div className="space-y-1.5">
            <Label htmlFor="audio-picker-keyword">名称</Label>
            <Input
              id="audio-picker-keyword"
              type="search"
              aria-label="搜索音频名称"
              value={draftKeyword}
              placeholder="搜索完整或部分文件名"
              onChange={(event) => setDraftKeyword(event.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="audio-picker-status">状态</Label>
            <Select
              value={status || "all"}
              onValueChange={(nextStatus) => {
                setStatus(nextStatus === "all" ? "" : nextStatus);
                setPage(1);
              }}
            >
              <SelectTrigger
                id="audio-picker-status"
                className="w-full"
                aria-label="筛选音频状态"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部状态</SelectItem>
                {AUDIO_RESOURCE_STATUSES.map((audioStatus) => (
                  <SelectItem key={audioStatus} value={audioStatus}>
                    {AUDIO_STATUS_LABELS[audioStatus]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label className="invisible hidden sm:block">操作</Label>
            <div className="flex gap-2">
              <Button type="submit">
                <Search aria-hidden="true" />
                搜索
              </Button>
              <Button
                type="button"
                size="icon"
                variant="outline"
                aria-label="重置筛选"
                onClick={() => {
                  setDraftKeyword("");
                  setKeyword("");
                  setStatus("");
                  setPage(1);
                }}
              >
                <RotateCcw aria-hidden="true" />
              </Button>
            </div>
          </div>
        </form>

        {query.isLoading ? (
          <div
            className="space-y-2"
            role="status"
            aria-label="正在加载音频资源"
          >
            {Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-12 w-full" />
            ))}
          </div>
        ) : query.isError ? (
          <Alert variant="destructive">
            <AlertDescription className="flex items-center justify-between gap-3">
              <span>{getErrorMessage(query.error)}</span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => query.refetch()}
              >
                重试
              </Button>
            </AlertDescription>
          </Alert>
        ) : query.data?.items.length ? (
          <RadioGroup
            value={selected?.id ?? ""}
            onValueChange={selectResource}
            aria-label="音频资源"
            className="max-h-80 gap-0 overflow-y-auto rounded-lg border"
          >
            {query.data.items.map((audio) => {
              const id = `audio-picker-${audio.id}`;
              return (
                <Label
                  key={audio.id}
                  htmlFor={id}
                  className="flex min-h-12 cursor-pointer items-center gap-3 border-b px-3 py-2 last:border-b-0"
                >
                  <RadioGroupItem id={id} value={audio.id} />
                  <span className="min-w-0 flex-1 break-all font-normal">
                    {audio.name}
                  </span>
                  <Badge
                    className="shrink-0"
                    variant={
                      audio.status === "Failed" ? "destructive" : "outline"
                    }
                  >
                    {AUDIO_STATUS_LABELS[audio.status]}
                  </Badge>
                </Label>
              );
            })}
          </RadioGroup>
        ) : (
          <p className="py-10 text-center text-sm text-muted-foreground">
            没有找到符合条件的音频资源。
          </p>
        )}

        {query.data ? (
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="音频资源分页"
          >
            <span className="text-sm text-muted-foreground">
              第 {query.data.page} / {Math.max(query.data.totalPages, 1)} 页
            </span>
            <div className="flex gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={page <= 1 || query.isFetching}
                onClick={() => setPage((current) => current - 1)}
              >
                <ChevronLeft aria-hidden="true" />
                上一页
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={page >= query.data.totalPages || query.isFetching}
                onClick={() => setPage((current) => current + 1)}
              >
                下一页
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </nav>
        ) : null}

        <DialogFooter>
          <Button type="button" variant="outline" onClick={close}>
            取消
          </Button>
          <Button
            type="button"
            disabled={!selected}
            onClick={() => {
              onSelect(selected);
              close();
            }}
          >
            确认选择
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
