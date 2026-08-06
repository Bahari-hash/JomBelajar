import { useEffect, useRef, useState } from "react";
import { ArrowLeft, Play, RefreshCw, Save } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import {
  getProcessingStatusLabel,
  getPublicationStatusLabel,
  getVideoActions,
  getVideoFailureMessage,
} from "@/constants/videoStatus.js";
import { VideoActionDialog } from "@/features/videos/VideoActionDialog.jsx";
import { VideoCategorySelector } from "@/features/videos/VideoCategorySelector.jsx";
import { VideoCoverControl } from "@/features/videos/VideoCoverControl.jsx";
import { VideoPlayer } from "@/features/videos/VideoPlayer.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { formatDateTime } from "@/lib/dateTime.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAllVideoCategoryOptionsQuery } from "@/services/videoCategoriesApi.js";
import {
  useGetAdminVideoQuery,
  useGetVideoPlaybackMutation,
  useUpdateVideoMutation,
} from "@/services/videosApi.js";

function formFromVideo(video) {
  return {
    title: video.title,
    description: video.description ?? "",
    originalLanguage: video.originalLanguage,
    categoryIds: video.categories.map(({ id }) => id),
    cover: video.cover,
    coverAction: "Keep",
    concurrencyStamp: video.concurrencyStamp,
  };
}

function VideoDetails() {
  const { videoId } = useParams();
  const {
    data: video,
    error: loadError,
    isLoading,
    isFetching,
    refetch,
  } = useGetAdminVideoQuery(videoId);
  useAdminPage(video?.title ?? "视频详情", "视频详情");
  const { data: categories = [], error: categoryError } =
    useGetAllVideoCategoryOptionsQuery();
  const [form, setForm] = useState(null);
  const baselineRef = useRef(null);
  const [formError, setFormError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [action, setAction] = useState(null);
  const [confirmClearCover, setConfirmClearCover] = useState(false);
  const [playback, setPlayback] = useState(null);
  const [updateVideo, updateState] = useUpdateVideoMutation();
  const [loadPlayback, playbackState] = useGetVideoPlaybackMutation();
  const activePlayback =
    playback &&
    video &&
    playback.videoId === video.id &&
    playback.concurrencyStamp === video.concurrencyStamp
      ? playback.data
      : null;
  const dirty = Boolean(
    form &&
    baselineRef.current &&
    JSON.stringify(form) !== JSON.stringify(baselineRef.current),
  );

  useEffect(() => {
    if (
      video &&
      !dirty &&
      baselineRef.current?.concurrencyStamp !== video.concurrencyStamp
    ) {
      const next = formFromVideo(video);
      setForm(next);
      baselineRef.current = next;
    }
  }, [dirty, video]);

  useEffect(() => {
    if (!video || !["Queued", "Processing"].includes(video.processingStatus))
      return undefined;
    let timer;
    const schedule = () => {
      window.clearInterval(timer);
      if (!document.hidden) timer = window.setInterval(() => refetch(), 3000);
    };
    const handleVisibility = () => {
      schedule();
      if (!document.hidden) refetch();
    };
    schedule();
    document.addEventListener("visibilitychange", handleVisibility);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener("visibilitychange", handleVisibility);
    };
  }, [refetch, video]);

  const applyVideo = (saved) => {
    const next = formFromVideo(saved);
    setForm(next);
    baselineRef.current = next;
  };
  const handleSave = async (event) => {
    event.preventDefault();
    if (event.currentTarget.querySelector('[data-uploading="true"]')) {
      setFormError({ detail: "请等待封面上传完成后再保存。", fieldErrors: {} });
      return;
    }
    if (!form || updateState.isLoading) return;
    const localErrors = {};
    if (!form.title.trim()) localErrors.title = ["请输入视频标题。"];
    else if (form.title.length > 200)
      localErrors.title = ["标题不能超过 200 个字符。"];
    if (form.description.length > 2000)
      localErrors.description = ["描述不能超过 2000 个字符。"];
    if (
      !/^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$/.test(
        form.originalLanguage.trim(),
      )
    )
      localErrors.originalLanguage = ["请输入有效的语言标签。"];
    if (form.categoryIds.length > 10)
      localErrors.categoryIds = ["视频最多选择 10 个分类。"];
    if (Object.keys(localErrors).length) {
      setFormError({ detail: "请修正表单字段。", fieldErrors: localErrors });
      return;
    }
    setFormError(null);
    try {
      const saved = await updateVideo({
        videoId,
        title: form.title,
        description: form.description.trim() || null,
        originalLanguage: form.originalLanguage.trim(),
        categoryIds: form.categoryIds,
        coverAction: form.coverAction,
        coverMediaResourceId:
          form.coverAction === "Set" ? form.cover?.id : null,
        concurrencyStamp: form.concurrencyStamp,
      }).unwrap();
      applyVideo(saved);
      setNotice("视频信息已保存。");
    } catch (error) {
      setFormError(error);
      if (
        ["VideoStatusConflict", "VideoArchiveConflict"].includes(
          error.errorCode,
        )
      )
        refetch();
    }
  };
  const handlePlayback = async () => {
    if (!video) return;
    const concurrencyStamp = video.concurrencyStamp;
    try {
      const data = await loadPlayback(videoId).unwrap();
      setPlayback({ videoId, concurrencyStamp, data });
    } catch (error) {
      setFormError(error);
    }
  };

  if (isLoading)
    return (
      <div className="space-y-4" role="status" aria-label="正在加载视频详情">
        <Skeleton className="h-9 w-72" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  if (loadError)
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {loadError.status === 404
            ? "视频不存在"
            : loadError.status === 403
              ? "无权查看视频"
              : "视频详情加载失败"}
        </AlertTitle>
        <AlertDescription className="mt-2 flex items-center justify-between gap-3">
          <span>{getErrorMessage(loadError)}</span>
          <Button asChild variant="outline" size="sm">
            <Link to="/videos">
              <ArrowLeft aria-hidden="true" />
              返回视频列表
            </Link>
          </Button>
        </AlertDescription>
      </Alert>
    );
  if (!video || !form) return null;
  const readOnly = ["Published", "Archived"].includes(video.publicationStatus);
  const actions = getVideoActions(video);
  const fieldError = (field) => formError?.fieldErrors?.[field]?.[0];
  return (
    <form className="min-w-0 max-w-full space-y-5" onSubmit={handleSave}>
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0 max-w-full">
          <p className="text-sm font-medium text-muted-foreground">视频管理</p>
          <h1 className="mt-1 max-w-3xl truncate text-2xl font-semibold">
            {video.title}
          </h1>
        </div>
        <div className="flex w-full flex-wrap gap-2 sm:w-auto sm:justify-end">
          <Button asChild type="button" variant="outline">
            <Link to="/videos">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={refetch}
            disabled={isFetching}
          >
            <RefreshCw
              aria-hidden="true"
              className={isFetching ? "animate-spin" : undefined}
            />
            刷新
          </Button>
          {actions.map((item) => (
            <Button
              key={item}
              type="button"
              variant={item === "archive" ? "destructive" : "outline"}
              onClick={() => setAction(item)}
            >
              {
                {
                  publish: "发布",
                  unpublish: "下架",
                  retry: "重试处理",
                  archive: "归档",
                }[item]
              }
            </Button>
          ))}
          <Button
            type="submit"
            disabled={readOnly || updateState.isLoading || !dirty}
          >
            <Save aria-hidden="true" />
            {updateState.isLoading ? "正在保存" : dirty ? "保存修改" : "已保存"}
          </Button>
        </div>
      </header>
      {readOnly ? (
        <Alert>
          <AlertTitle>
            {video.publicationStatus === "Published"
              ? "视频已发布"
              : "视频已归档"}
          </AlertTitle>
          <AlertDescription>
            {video.publicationStatus === "Published"
              ? "请先下架视频，再修改元数据或分类。"
              : "归档视频是终态，只能查看处理和审计信息。"}
          </AlertDescription>
        </Alert>
      ) : null}
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      {formError ? (
        <Alert variant="destructive" aria-live="assertive">
          <AlertDescription>
            {formError.errorCode === "VideoConcurrencyConflict"
              ? "视频已被其他管理员修改。你的输入仍然保留；重新加载会丢弃本地修改。"
              : getErrorMessage(formError)}
          </AlertDescription>
          {formError.errorCode === "VideoConcurrencyConflict" ? (
            <div className="mt-2 flex gap-2">
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={async () => {
                  const result = await refetch();
                  if (result.data) applyVideo(result.data);
                  setFormError(null);
                }}
              >
                重新加载最新数据
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => {
                  setFormError(null);
                }}
              >
                保留本地输入
              </Button>
            </div>
          ) : null}
        </Alert>
      ) : null}
      <section className="flex flex-wrap gap-2">
        <Badge
          variant={
            video.processingStatus === "Failed" ? "destructive" : "outline"
          }
        >
          {getProcessingStatusLabel(video.processingStatus)}
        </Badge>
        <Badge variant="secondary">
          {getPublicationStatusLabel(video.publicationStatus)}
        </Badge>
        {isFetching ? (
          <span className="text-sm text-muted-foreground">
            正在更新处理状态
          </span>
        ) : null}
      </section>
      {video.failureCode ? (
        <Alert variant="destructive">
          <AlertTitle>视频处理失败</AlertTitle>
          <AlertDescription>
            {getVideoFailureMessage(video.failureCode)}
          </AlertDescription>
        </Alert>
      ) : null}
      <section className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="detail-title">
            标题 <span aria-hidden="true">*</span>
          </Label>
          <Input
            id="detail-title"
            value={form.title}
            maxLength={200}
            disabled={readOnly}
            aria-invalid={Boolean(fieldError("title"))}
            onChange={(event) =>
              setForm({ ...form, title: event.target.value })
            }
          />
          <p className="text-xs text-muted-foreground">
            {fieldError("title") ?? `${form.title.length}/200`}
          </p>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="detail-language">
            原始语言 <span aria-hidden="true">*</span>
          </Label>
          <Input
            id="detail-language"
            value={form.originalLanguage}
            maxLength={35}
            disabled={readOnly}
            aria-invalid={Boolean(fieldError("originalLanguage"))}
            onChange={(event) =>
              setForm({ ...form, originalLanguage: event.target.value })
            }
          />
          <p className="text-xs text-muted-foreground">
            {fieldError("originalLanguage") ?? "BCP 47 语言标签"}
          </p>
        </div>
      </section>
      <div className="space-y-1.5">
        <Label htmlFor="detail-description">描述</Label>
        <Textarea
          id="detail-description"
          rows={5}
          value={form.description}
          maxLength={2000}
          disabled={readOnly}
          aria-invalid={Boolean(fieldError("description"))}
          onChange={(event) =>
            setForm({ ...form, description: event.target.value })
          }
        />
        <p className="text-xs text-muted-foreground">
          {fieldError("description") ?? `${form.description.length}/2000`}
        </p>
      </div>
      <VideoCategorySelector
        categories={[
          ...categories,
          ...video.categories.filter(
            (item) => !categories.some(({ id }) => id === item.id),
          ),
        ]}
        selectedIds={form.categoryIds}
        disabled={readOnly || Boolean(categoryError)}
        error={
          fieldError("categoryIds") ?? (categoryError ? "分类加载失败。" : null)
        }
        onChange={(categoryIds) => setForm({ ...form, categoryIds })}
      />
      <VideoCoverControl
        value={form.cover}
        disabled={readOnly || updateState.isLoading}
        onUploaded={(cover) => {
          setForm({ ...form, cover, coverAction: "Set" });
          setFormError(null);
        }}
        onClear={() => setConfirmClearCover(true)}
      />
      <section className="space-y-3 border-y py-4">
        <h2 className="text-sm font-semibold">媒体与处理信息</h2>
        <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Info
            label="时长"
            value={
              video.durationSeconds === null
                ? "尚未探测"
                : `${video.durationSeconds.toFixed(1)} 秒`
            }
          />
          <Info
            label="显示尺寸"
            value={
              video.displayWidth
                ? `${video.displayWidth} × ${video.displayHeight}`
                : "尚未探测"
            }
          />
          <Info label="封装格式" value={video.containerFormat ?? "尚未探测"} />
          <Info
            label="视频 / 音频编码"
            value={
              [video.videoCodec, video.audioCodec]
                .filter(Boolean)
                .join(" / ") || "尚未探测"
            }
          />
          <Info
            label="创建者"
            value={video.createdBy.nickname ?? video.createdBy.id}
          />
          <Info
            label="最后修改者"
            value={video.lastEditor.nickname ?? video.lastEditor.id}
          />
          <Info label="创建时间" value={formatDateTime(video.createdAt)} />
          <Info label="更新时间" value={formatDateTime(video.updatedAt)} />
        </dl>
        {video.latestJob ? (
          <p className="text-sm text-muted-foreground">
            最新任务：{video.latestJob.status}，尝试{" "}
            {video.latestJob.attemptCount} 次
            {video.latestJob.completedAt
              ? `，完成于 ${formatDateTime(video.latestJob.completedAt)}`
              : ""}
          </p>
        ) : null}
        {video.renditions.length ? (
          <div className="flex flex-wrap gap-2">
            {video.renditions.map((item) => (
              <Badge
                key={`${item.targetHeight}-${item.codecs}`}
                variant="outline"
              >
                {item.width}×{item.height} · {item.codecs}
              </Badge>
            ))}
          </div>
        ) : null}
      </section>
      {video.processingStatus === "Ready" ? (
        <section className="space-y-3">
          <div className="flex items-center justify-between gap-3">
            <h2 className="text-sm font-semibold">播放预览</h2>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={playbackState.isLoading}
              onClick={handlePlayback}
            >
              <Play aria-hidden="true" />
              {playbackState.isLoading
                ? "正在获取播放地址"
                : activePlayback
                  ? "刷新播放地址"
                  : "加载播放预览"}
            </Button>
          </div>
          {activePlayback ? (
            <VideoPlayer playback={activePlayback} onRefresh={handlePlayback} />
          ) : (
            <p className="text-sm text-muted-foreground">
              播放地址仅在需要预览时请求，不会写入缓存或本地存储。
            </p>
          )}
        </section>
      ) : null}
      {action ? (
        <VideoActionDialog
          action={action}
          video={video}
          onClose={() => setAction(null)}
          onDone={(message, saved) => {
            setNotice(message);
            applyVideo(saved);
          }}
          onConflict={refetch}
        />
      ) : null}
      <AlertDialog open={confirmClearCover} onOpenChange={setConfirmClearCover}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              清除“{video.title}”的自定义封面？
            </AlertDialogTitle>
            <AlertDialogDescription>
              保存修改后将解除当前封面关联，并立即回退到视频自动抽帧封面。已上传的媒体资源不会被删除。
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>取消</AlertDialogCancel>
            <AlertDialogAction
              type="button"
              variant="destructive"
              onClick={() => {
                setForm({ ...form, cover: null, coverAction: "Clear" });
                setConfirmClearCover(false);
              }}
            >
              确认清除
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  );
}

function Info({ label, value }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-1 wrap-break-word">{value}</dd>
    </div>
  );
}

export default VideoDetails;
