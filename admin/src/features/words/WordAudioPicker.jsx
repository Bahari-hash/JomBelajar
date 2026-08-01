import { useEffect, useRef, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Play,
  RotateCcw,
  Search,
} from "lucide-react";
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
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { WordAudioUploadControl } from "@/features/words/WordAudioUploadControl.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useGetAudioPlaybackMutation,
  useGetWordAudioOptionsQuery,
  usePublishWordAudioMutation,
  useRetryWordAudioMutation,
} from "@/services/wordsApi.js";

const PROCESSING_LABELS = {
  Queued: "等待处理",
  Processing: "处理中",
  Ready: "处理完成",
  Failed: "处理失败",
};

const PUBLICATION_LABELS = {
  Draft: "未发布",
  Published: "已发布",
  Unpublished: "已下架",
};

function isSelectable(audio) {
  return (
    audio.processingStatus === "Ready" &&
    audio.publicationStatus === "Published"
  );
}

/** Uploads, processes and selects word audio within the word workflow. */
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
    language: language?.trim() ?? "",
    kind,
  });
  const [playback, setPlayback] = useState(null);
  const [notice, setNotice] = useState(null);
  const [actionError, setActionError] = useState(null);
  const [activeAudioId, setActiveAudioId] = useState(null);
  const [pollingInterval, setPollingInterval] = useState(3000);
  const audioRef = useRef(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetWordAudioOptionsQuery(filters, {
      pollingInterval,
      skipPollingIfUnfocused: true,
    });
  const [getPlayback, playbackState] = useGetAudioPlaybackMutation();
  const [publishAudio, publishState] = usePublishWordAudioMutation();
  const [retryAudio, retryState] = useRetryWordAudioMutation();
  const actionPending = publishState.isLoading || retryState.isLoading;

  useEffect(
    () => () => {
      audioRef.current?.pause();
    },
    [],
  );

  useEffect(() => {
    if (!data) return;
    const hasActiveProcessing = data.items.some((audio) =>
      ["Queued", "Processing"].includes(audio.processingStatus),
    );
    setPollingInterval(hasActiveProcessing ? 3000 : 0);
  }, [data]);

  const handlePlay = async (audio) => {
    audioRef.current?.pause();
    setPlayback(null);
    setActionError(null);
    try {
      const result = await getPlayback(audio.id).unwrap();
      setPlayback({ ...result, id: audio.id, title: audio.title });
    } catch {
      // RTK mutation state renders a safe recoverable error.
    }
  };

  const handlePublishAndSelect = async (audio) => {
    if (actionPending) return;
    setActionError(null);
    setActiveAudioId(audio.id);
    try {
      const published = await publishAudio(audio.id).unwrap();
      onSelect(published);
      onClose();
    } catch (requestError) {
      setActionError(requestError);
      refetch();
    } finally {
      setActiveAudioId(null);
    }
  };

  const handleRetry = async (audio) => {
    if (actionPending) return;
    setActionError(null);
    setActiveAudioId(audio.id);
    try {
      await retryAudio(audio.id).unwrap();
      setNotice(`“${audio.title}”已重新提交处理。`);
      refetch();
    } catch (requestError) {
      setActionError(requestError);
    } finally {
      setActiveAudioId(null);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-4xl">
        <DialogHeader>
          <DialogTitle>上传或选择音频</DialogTitle>
          <DialogDescription>
            {kind === "WordPronunciation" ? "单词发音" : "例句音频"}
            {filters.language ? ` · ${filters.language}` : ""}
            。新音频处理完成并发布后才能用于单词。
          </DialogDescription>
        </DialogHeader>
        {!filters.language ? (
          <Alert variant="destructive">
            <AlertDescription>
              请先关闭窗口并填写当前单词或例句的语言标签。
            </AlertDescription>
          </Alert>
        ) : null}
        <WordAudioUploadControl
          kind={kind}
          language={filters.language}
          onCreated={(audio) => {
            setNotice(`“${audio.title}”已上传，正在等待后台处理。`);
            setFilters((current) => ({ ...current, page: 1, keyword: "" }));
            setDraftKeyword("");
            setPollingInterval(3000);
          }}
        />
        <form
          className="grid gap-3 sm:grid-cols-[1fr_auto]"
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
            <Label htmlFor="audio-keyword">搜索音频</Label>
            <Input
              id="audio-keyword"
              value={draftKeyword}
              maxLength={200}
              placeholder="音频标题"
              onChange={(event) => setDraftKeyword(event.target.value)}
            />
          </div>
          <div className="self-end">
            <Button type="submit">
              <Search aria-hidden="true" />
              搜索
            </Button>
          </div>
        </form>
        {notice ? (
          <Alert role="status">
            <AlertDescription>{notice}</AlertDescription>
          </Alert>
        ) : null}
        {actionError ? (
          <Alert variant="destructive">
            <AlertDescription>
              {getErrorMessage(actionError, "音频状态操作失败，请刷新后重试。")}
            </AlertDescription>
          </Alert>
        ) : null}
        {isLoading ? (
          <div className="space-y-2" role="status" aria-label="正在加载音频">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
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
            {data.items.map((audio) => {
              const selectable = isSelectable(audio);
              const active = activeAudioId === audio.id;
              return (
                <div
                  key={audio.id}
                  className="flex flex-wrap items-center gap-3 p-3"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{audio.title}</p>
                    <p className="text-xs text-muted-foreground">
                      {audio.languageTag} · {formatDateTime(audio.updatedAt)}
                      {audio.durationSeconds === null
                        ? ""
                        : ` · ${audio.durationSeconds.toFixed(1)} 秒`}
                    </p>
                    <div className="mt-1.5 flex flex-wrap gap-1.5">
                      <Badge
                        variant={
                          audio.processingStatus === "Failed"
                            ? "destructive"
                            : "outline"
                        }
                      >
                        {PROCESSING_LABELS[audio.processingStatus]}
                      </Badge>
                      <Badge variant="secondary">
                        {PUBLICATION_LABELS[audio.publicationStatus]}
                      </Badge>
                    </div>
                  </div>
                  {selectable ? (
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
                  ) : null}
                  {selectable ? (
                    <Button
                      type="button"
                      size="sm"
                      variant={
                        selectedId === audio.id ? "secondary" : "outline"
                      }
                      disabled={!filters.language}
                      onClick={() => {
                        onSelect(audio);
                        onClose();
                      }}
                    >
                      {selectedId === audio.id ? "已选择" : "选择"}
                    </Button>
                  ) : audio.processingStatus === "Ready" ? (
                    <Button
                      type="button"
                      size="sm"
                      disabled={actionPending || !filters.language}
                      onClick={() => handlePublishAndSelect(audio)}
                    >
                      {active && publishState.isLoading
                        ? "正在发布"
                        : "发布并选择"}
                    </Button>
                  ) : audio.processingStatus === "Failed" ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={actionPending}
                      onClick={() => handleRetry(audio)}
                    >
                      {active && retryState.isLoading ? "正在重试" : "重试处理"}
                    </Button>
                  ) : (
                    <span className="text-xs text-muted-foreground">
                      后台处理中
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        ) : (
          <p className="border-y py-8 text-center text-sm text-muted-foreground">
            没有符合条件的音频，可以在上方上传新音频。
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
              {isFetching ? " · 正在同步处理状态" : ""}
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
