import { useEffect, useMemo, useState } from "react";
import {
  AudioLines,
  ChevronLeft,
  ChevronRight,
  RotateCcw,
  Search,
} from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { AudioTable } from "@/features/audio/AudioTable.jsx";
import { AudioUploadControl } from "@/features/audio/AudioUploadControl.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { formatDateTime } from "@/lib/dateTime.js";
import {
  AUDIO_RESOURCE_STATUSES,
  AUDIO_STATUS_LABELS,
} from "@/services/audioContracts.js";
import {
  useDeleteAudioResourceMutation,
  useGetAdminAudioResourcesQuery,
  useGetAudioPlaybackMutation,
  useLazyGetAdminAudioResourceQuery,
  useRenameAudioResourceMutation,
  useReprocessAudioResourceMutation,
} from "@/services/audioApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

const PAGE_SIZE = 20;

function readFilters(searchParams) {
  const parsedPage = Number(searchParams.get("page"));
  const status = searchParams.get("status") ?? "";
  return {
    page: Number.isInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1,
    pageSize: PAGE_SIZE,
    keyword: searchParams.get("keyword")?.trim() ?? "",
    status: AUDIO_RESOURCE_STATUSES.includes(status) ? status : "",
  };
}

function writeFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page > 1) params.set("page", String(filters.page));
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.status) params.set("status", filters.status);
  return params;
}

function audioMutationMessage(error, fallback) {
  if (error?.errorCode === "AudioNameConflict")
    return "已有同名音频资源，请使用其他名称。";
  if (error?.errorCode === "AudioInUse")
    return "该音频正在被其他内容使用，无法删除。";
  if (error?.errorCode === "AudioStatusConflict")
    return "音频状态已发生变化，列表已刷新，请确认后重试。";
  return getErrorMessage(error, fallback);
}

function AudioDetailsDialog({ state, onClose }) {
  return (
    <Dialog open={Boolean(state)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>音频详情</DialogTitle>
          <DialogDescription>
            查看音频处理结果和技术元数据，不展示对象存储路径。
          </DialogDescription>
        </DialogHeader>
        {state?.loading ? (
          <div
            className="space-y-2"
            role="status"
            aria-label="正在加载音频详情"
          >
            {Array.from({ length: 5 }, (_, index) => (
              <Skeleton key={index} className="h-8 w-full" />
            ))}
          </div>
        ) : state?.error ? (
          <Alert variant="destructive">
            <AlertDescription>{getErrorMessage(state.error)}</AlertDescription>
          </Alert>
        ) : state?.audio ? (
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm">
            <Detail label="名称" value={state.audio.name} />
            <Detail
              label="状态"
              value={AUDIO_STATUS_LABELS[state.audio.status]}
            />
            <Detail
              label="时长"
              value={
                state.audio.durationSeconds === null
                  ? "-"
                  : `${state.audio.durationSeconds.toFixed(2)} 秒`
              }
            />
            <Detail
              label="采样率"
              value={
                state.audio.sampleRate ? `${state.audio.sampleRate} Hz` : "-"
              }
            />
            <Detail label="声道" value={state.audio.channels ?? "-"} />
            <Detail
              label="容器格式"
              value={state.audio.containerFormat ?? "-"}
            />
            <Detail label="源编码" value={state.audio.sourceCodec ?? "-"} />
            <Detail
              label="失败代码"
              value={state.audio.lastFailureCode ?? "-"}
            />
            <Detail
              label="创建时间"
              value={formatDateTime(state.audio.createdAt)}
            />
            <Detail
              label="更新时间"
              value={formatDateTime(state.audio.updatedAt)}
            />
          </dl>
        ) : null}
        <DialogFooter showCloseButton />
      </DialogContent>
    </Dialog>
  );
}

function Detail({ label, value }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-1 truncate" title={String(value)}>
        {value}
      </dd>
    </div>
  );
}

function AudioLibrary() {
  useAdminPage("音频资源");
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = useMemo(() => readFilters(searchParams), [searchParams]);
  const canonical = writeFilters(filters).toString();
  const [keyword, setKeyword] = useState(filters.keyword);
  const [notice, setNotice] = useState(null);
  const [activePlayback, setActivePlayback] = useState(null);
  const [renameTarget, setRenameTarget] = useState(null);
  const [renameName, setRenameName] = useState("");
  const [renameError, setRenameError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteError, setDeleteError] = useState(null);
  const [retryTarget, setRetryTarget] = useState(null);
  const [details, setDetails] = useState(null);
  const [pendingAction, setPendingAction] = useState(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetAdminAudioResourcesQuery(filters, { pollingInterval: 5000 });
  const [getDetails] = useLazyGetAdminAudioResourceQuery();
  const [getPlayback] = useGetAudioPlaybackMutation();
  const [renameAudio, renameState] = useRenameAudioResourceMutation();
  const [reprocessAudio] = useReprocessAudioResourceMutation();
  const [deleteAudio, deleteState] = useDeleteAudioResourceMutation();

  useEffect(() => {
    if (searchParams.toString() !== canonical)
      setSearchParams(canonical, { replace: true });
  }, [canonical, searchParams, setSearchParams]);

  useEffect(() => setKeyword(filters.keyword), [filters.keyword]);

  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1))
      setSearchParams(
        writeFilters({ ...filters, page: Math.max(data.totalPages, 1) }),
        { replace: true },
      );
  }, [data, filters, setSearchParams]);

  const showDetails = async (audio) => {
    setDetails({ loading: true, audio: null, error: null });
    try {
      const result = await getDetails(audio.id, false).unwrap();
      setDetails({ loading: false, audio: result, error: null });
    } catch (requestError) {
      setDetails({ loading: false, audio: null, error: requestError });
    }
  };

  const playAudio = async (audio) => {
    setPendingAction({ action: "play", audioResourceId: audio.id });
    try {
      const playback = await getPlayback(audio.id).unwrap();
      setActivePlayback({ name: audio.name, ...playback });
    } catch (requestError) {
      setNotice(getErrorMessage(requestError, "音频暂不可用。"));
    } finally {
      setPendingAction(null);
    }
  };

  const reprocess = async (audio) => {
    setPendingAction({ action: "reprocess", audioResourceId: audio.id });
    try {
      await reprocessAudio(audio.id).unwrap();
      setNotice("已提交重新处理。");
    } catch (requestError) {
      setNotice(audioMutationMessage(requestError, "重新处理失败，请重试。"));
      if (requestError?.errorCode === "AudioStatusConflict") refetch();
    } finally {
      setPendingAction(null);
    }
  };

  const submitRename = async (event) => {
    event.preventDefault();
    const name = renameName.trim();
    if (!name) {
      setRenameError("请输入音频名称。");
      return;
    }
    setRenameError(null);
    try {
      await renameAudio({ audioResourceId: renameTarget.id, name }).unwrap();
      setNotice("音频名称已更新。");
      setRenameTarget(null);
    } catch (requestError) {
      setRenameError(
        audioMutationMessage(requestError, "重命名失败，请重试。"),
      );
    }
  };

  const submitDelete = async (event) => {
    event.preventDefault();
    setDeleteError(null);
    try {
      await deleteAudio(deleteTarget.id).unwrap();
      if (activePlayback?.name === deleteTarget.name) setActivePlayback(null);
      setNotice("音频资源已删除。");
      setDeleteTarget(null);
    } catch (requestError) {
      setDeleteError(audioMutationMessage(requestError, "删除失败，请重试。"));
    }
  };

  const hasFilters = Boolean(filters.keyword || filters.status);

  return (
    <div className="min-w-0 max-w-full space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">资源管理</p>
          <h1 className="mt-1 text-2xl font-semibold">音频资源</h1>
        </div>
        <Button
          type="button"
          variant="outline"
          onClick={refetch}
          disabled={isFetching}
        >
          <RotateCcw
            aria-hidden="true"
            className={isFetching ? "animate-spin" : undefined}
          />
          {isFetching ? "正在刷新" : "刷新"}
        </Button>
      </header>

      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}

      <AudioUploadControl
        onStarted={() => refetch()}
        onCompleted={(audio) => {
          setNotice(
            audio.status === "Ready"
              ? "音频已处理完成。"
              : "音频处理失败，可从列表重试。",
          );
          refetch();
        }}
      />

      <form
        className="border-y py-4"
        role="search"
        onSubmit={(event) => {
          event.preventDefault();
          setSearchParams(
            writeFilters({ ...filters, page: 1, keyword: keyword.trim() }),
          );
        }}
      >
        <div className="grid gap-y-3 gap-x-4 md:grid-cols-[16rem_8rem_auto] items-start">
          <div className="space-y-1.5">
            <Label htmlFor="audio-keyword">名称</Label>
            <Input
              id="audio-keyword"
              type="search"
              aria-label="搜索音频名称"
              value={keyword}
              placeholder="搜索完整或部分文件名"
              onChange={(event) => setKeyword(event.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="audio-status">状态</Label>
            <Select
              value={filters.status || "all"}
              onValueChange={(value) =>
                setSearchParams(
                  writeFilters({
                    ...filters,
                    page: 1,
                    status: value === "all" ? "" : value,
                  }),
                )
              }
            >
              <SelectTrigger
                className="w-full"
                id="audio-status"
                aria-label="筛选音频状态"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">全部状态</SelectItem>
                {AUDIO_RESOURCE_STATUSES.map((status) => (
                  <SelectItem key={status} value={status}>
                    {AUDIO_STATUS_LABELS[status]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label className="invisible hidden md:block" aria-hidden="true">
              操作
            </Label>
            <div className="flex gap-2">
              <Button type="submit">
                <Search aria-hidden="true" />
                应用
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setKeyword("");
                  setSearchParams(new URLSearchParams());
                }}
              >
                <RotateCcw aria-hidden="true" />
              </Button>
            </div>
          </div>
        </div>
      </form>

      {activePlayback ? (
        <section className="flex flex-wrap items-center gap-3 border-b pb-4">
          <div className="min-w-0">
            <h2 className="truncate text-sm font-medium">
              {activePlayback.name}
            </h2>
            <p className="text-xs text-muted-foreground">
              播放地址由服务端临时授权。
            </p>
          </div>
          <audio
            className="h-10 max-w-full flex-1"
            controls
            src={activePlayback.url}
            aria-label={`正在试听 ${activePlayback.name}`}
          />
        </section>
      ) : null}

      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载音频列表">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-14 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>
            {error.status === 403 ? "无权查看音频" : "音频列表加载失败"}
          </AlertTitle>
          <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有音频管理权限。"
                : getErrorMessage(error)}
            </span>
            <Button type="button" variant="outline" size="sm" onClick={refetch}>
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : data?.items.length === 0 ? (
        <section className="flex min-h-56 flex-col items-center justify-center border-y text-center">
          <AudioLines
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2 className="mt-4 text-sm font-medium">
            {hasFilters ? "没有符合条件的音频" : "暂无音频资源"}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? "调整筛选条件后重试。"
              : "从上方选择音频文件开始上传。"}
          </p>
        </section>
      ) : data ? (
        <>
          <div
            className="flex min-h-6 items-center justify-between text-sm text-muted-foreground"
            aria-live="polite"
          >
            <span>共 {data.totalCount} 个音频资源</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <AudioTable
            audioResources={data.items}
            pendingAction={pendingAction}
            onDetails={showDetails}
            onPlay={playAudio}
            onRename={(audio) => {
              setRenameTarget(audio);
              setRenameName(audio.name);
              setRenameError(null);
            }}
            onReprocess={reprocess}
            onRetryUpload={setRetryTarget}
            onDelete={(audio) => {
              setDeleteTarget(audio);
              setDeleteError(null);
            }}
          />
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="音频列表分页"
          >
            <p className="text-sm text-muted-foreground">
              第 {data.page} / {Math.max(data.totalPages, 1)} 页
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={data.page <= 1 || isFetching}
                onClick={() =>
                  setSearchParams(
                    writeFilters({ ...filters, page: data.page - 1 }),
                  )
                }
              >
                <ChevronLeft aria-hidden="true" />
                上一页
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={data.page >= data.totalPages || isFetching}
                onClick={() =>
                  setSearchParams(
                    writeFilters({ ...filters, page: data.page + 1 }),
                  )
                }
              >
                下一页
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </nav>
        </>
      ) : null}

      <AudioDetailsDialog state={details} onClose={() => setDetails(null)} />

      <Dialog
        open={Boolean(renameTarget)}
        onOpenChange={(open) =>
          !open && !renameState.isLoading && setRenameTarget(null)
        }
      >
        <DialogContent>
          <form onSubmit={submitRename}>
            <DialogHeader>
              <DialogTitle>重命名音频</DialogTitle>
              <DialogDescription>修改资源库中的显示文件名。</DialogDescription>
            </DialogHeader>
            <div className="mt-4 space-y-2">
              <Label htmlFor="audio-rename">音频名称</Label>
              <Input
                id="audio-rename"
                value={renameName}
                maxLength={255}
                disabled={renameState.isLoading}
                onChange={(event) => setRenameName(event.target.value)}
              />
              {renameError ? (
                <p className="text-xs text-destructive" role="alert">
                  {renameError}
                </p>
              ) : null}
            </div>
            <DialogFooter className="mt-4">
              <Button
                type="button"
                variant="outline"
                disabled={renameState.isLoading}
                onClick={() => setRenameTarget(null)}
              >
                取消
              </Button>
              <Button type="submit" disabled={renameState.isLoading}>
                {renameState.isLoading ? "正在保存" : "保存名称"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={Boolean(deleteTarget)}
        onOpenChange={(open) =>
          !open && !deleteState.isLoading && setDeleteTarget(null)
        }
      >
        <AlertDialogContent>
          <form onSubmit={submitDelete}>
            <AlertDialogHeader>
              <AlertDialogTitle>删除音频资源？</AlertDialogTitle>
              <AlertDialogDescription>
                “{deleteTarget?.name}
                ”及其源文件和处理结果会被永久删除。被业务内容引用时服务端会拒绝删除。
              </AlertDialogDescription>
            </AlertDialogHeader>
            {deleteError ? (
              <Alert className="mt-3" variant="destructive">
                <AlertDescription>{deleteError}</AlertDescription>
              </Alert>
            ) : null}
            <AlertDialogFooter className="mt-4">
              <AlertDialogCancel type="button" disabled={deleteState.isLoading}>
                取消
              </AlertDialogCancel>
              <Button
                type="submit"
                variant="destructive"
                disabled={deleteState.isLoading}
              >
                {deleteState.isLoading ? "正在删除" : "确认删除"}
              </Button>
            </AlertDialogFooter>
          </form>
        </AlertDialogContent>
      </AlertDialog>

      <Dialog
        open={Boolean(retryTarget)}
        onOpenChange={(open) => !open && setRetryTarget(null)}
      >
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>重新上传音频</DialogTitle>
            <DialogDescription>
              新文件会替换失败资源的旧上传源，资源 ID 保持不变。
            </DialogDescription>
          </DialogHeader>
          {retryTarget ? (
            <AudioUploadControl
              resource={retryTarget}
              onStarted={() => refetch()}
              onCompleted={(audio) => {
                setNotice(
                  audio.status === "Ready"
                    ? "音频已重新上传并处理完成。"
                    : "重新上传完成，但音频处理失败。",
                );
                setRetryTarget(null);
                refetch();
              }}
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default AudioLibrary;
