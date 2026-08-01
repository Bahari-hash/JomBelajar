import { useEffect, useRef, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Play,
  RotateCcw,
  Search,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
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
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useGetAudioPlaybackMutation,
  useGetWordAudioOptionsQuery,
} from "@/services/wordsApi.js";

/** Selects a compatible published audio clip without retaining playback URLs. */
export function WordAudioPicker({
  kind,
  language,
  selectedId,
  onSelect,
  onClose,
}) {
  const [draftKeyword, setDraftKeyword] = useState("");
  const [filters, setFilters] = useState({
    page: 1,
    pageSize: 20,
    keyword: "",
    language: language ?? "",
    kind,
  });
  const [playback, setPlayback] = useState(null);
  const audioRef = useRef(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetWordAudioOptionsQuery(filters);
  const [getPlayback, playbackState] = useGetAudioPlaybackMutation();

  useEffect(
    () => () => {
      audioRef.current?.pause();
    },
    [],
  );

  const handlePlay = async (audio) => {
    audioRef.current?.pause();
    setPlayback(null);
    try {
      const result = await getPlayback(audio.id).unwrap();
      setPlayback({ ...result, id: audio.id, title: audio.title });
    } catch {
      // RTK mutation state renders a safe recoverable error.
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>选择音频</DialogTitle>
          <DialogDescription>
            {kind === "WordPronunciation"
              ? "仅显示已处理并发布的单词发音音频。"
              : "仅显示已处理并发布的例句音频。"}
          </DialogDescription>
        </DialogHeader>
        <form
          className="grid gap-3 sm:grid-cols-[1fr_10rem_auto]"
          onSubmit={(event) => {
            event.preventDefault();
            setFilters((current) => ({
              ...current,
              page: 1,
              keyword: draftKeyword.trim(),
            }));
          }}
        >
          <div className="space-y-1.5">
            <Label htmlFor="audio-keyword">关键词</Label>
            <Input
              id="audio-keyword"
              value={draftKeyword}
              maxLength={200}
              onChange={(event) => setDraftKeyword(event.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="audio-language">语言</Label>
            <Input
              id="audio-language"
              value={filters.language}
              maxLength={35}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  language: event.target.value,
                }))
              }
            />
          </div>
          <div className="self-end">
            <Button type="submit">
              <Search aria-hidden="true" />
              搜索
            </Button>
          </div>
        </form>
        {isLoading ? (
          <div className="space-y-2" role="status" aria-label="正在加载音频">
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
          </div>
        ) : error ? (
          <Alert variant="destructive">
            <AlertDescription className="flex items-center justify-between gap-3">
              <span>
                {getErrorMessage(error, "音频列表加载失败，请重试。")}
              </span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={refetch}
              >
                <RotateCcw aria-hidden="true" />
                重试
              </Button>
            </AlertDescription>
          </Alert>
        ) : data?.items.length ? (
          <div className="divide-y rounded-lg border">
            {data.items.map((audio) => (
              <div
                key={audio.id}
                className="flex flex-wrap items-center gap-3 p-3"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium">{audio.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {audio.languageTag} ·{" "}
                    {audio.durationSeconds === null
                      ? "时长未知"
                      : `${audio.durationSeconds.toFixed(1)} 秒`}{" "}
                    · {formatDateTime(audio.updatedAt)}
                  </p>
                </div>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  aria-label={`试听 ${audio.title}`}
                  disabled={playbackState.isLoading}
                  onClick={() => handlePlay(audio)}
                >
                  <Play aria-hidden="true" />
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant={selectedId === audio.id ? "secondary" : "outline"}
                  onClick={() => {
                    onSelect(audio);
                    onClose();
                  }}
                >
                  {selectedId === audio.id ? "已选择" : "选择"}
                </Button>
              </div>
            ))}
          </div>
        ) : (
          <p className="border-y py-8 text-center text-sm text-muted-foreground">
            没有符合条件的可用音频。
          </p>
        )}
        {playback ? (
          <div className="space-y-2" role="status">
            <p className="text-sm font-medium">正在试听：{playback.title}</p>
            <audio
              ref={audioRef}
              controls
              autoPlay
              className="w-full"
              src={playback.url}
            >
              当前浏览器不支持音频播放。
            </audio>
          </div>
        ) : playbackState.error ? (
          <Alert variant="destructive">
            <AlertDescription>
              {getErrorMessage(
                playbackState.error,
                "音频播放地址获取失败，请重试或选择其他音频。",
              )}
            </AlertDescription>
          </Alert>
        ) : null}
        {data ? (
          <div className="flex items-center justify-between gap-3">
            <span className="text-sm text-muted-foreground">
              第 {data.page} / {Math.max(data.totalPages, 1)} 页
              {isFetching ? " · 正在更新" : ""}
            </span>
            <div className="flex gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={data.page <= 1 || isFetching}
                onClick={() =>
                  setFilters((current) => ({
                    ...current,
                    page: current.page - 1,
                  }))
                }
              >
                <ChevronLeft aria-hidden="true" />
                上一页
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={data.page >= data.totalPages || isFetching}
                onClick={() =>
                  setFilters((current) => ({
                    ...current,
                    page: current.page + 1,
                  }))
                }
              >
                下一页
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </div>
        ) : null}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            关闭
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
