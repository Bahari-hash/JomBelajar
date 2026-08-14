import { useState } from "react";
import { ArrowLeft, Upload, X } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import { VideoCategorySelector } from "@/features/videos/VideoCategorySelector.jsx";
import { VideoCoverControl } from "@/features/videos/VideoCoverControl.jsx";
import { VideoFileControl } from "@/features/videos/VideoFileControl.jsx";
import { useCourseVideoUpload } from "@/features/videos/useCourseVideoUpload.js";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAllVideoCategoryOptionsQuery } from "@/services/videoCategoriesApi.js";
import { useCreateVideoMutation } from "@/services/videosApi.js";
import { useGetCourseVideoUploadCapabilityQuery } from "@/services/videoUploadApi.js";

function validateFile(file, capability) {
  if (!file) return "请选择视频源文件。";
  if (file.size <= 0 || file.size > capability.maxSizeBytes)
    return "文件大小不符合服务端上传限制。";
  const dot = file.name.lastIndexOf(".");
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  const allowed = capability.allowedTypes.find(
    (item) => item.extension.toLowerCase() === extension,
  );
  if (!allowed || !allowed.contentTypes.includes(file.type))
    return "文件扩展名或媒体类型不受支持。";
  return null;
}

function stageLabel(stage) {
  return (
    {
      idle: "等待上传",
      checking: "检查恢复会话",
      initializing: "初始化上传",
      uploading: "上传源文件",
      finalizing: "服务端归档文件",
      confirming: "确认媒体资源",
      completed: "上传完成",
      aborting: "取消服务端会话",
      cancelled: "已取消",
      failed: "上传失败",
    }[stage] ?? "处理中"
  );
}

function VideoCreate() {
  useAdminPage("上传视频");
  const navigate = useNavigate();
  const [form, setForm] = useState({
    title: "",
    description: "",
    categoryIds: [],
  });
  const [file, setFile] = useState(null);
  const [uploadedResource, setUploadedResource] = useState(null);
  const [coverResource, setCoverResource] = useState(null);
  const [error, setError] = useState(null);
  const {
    data: capability,
    error: capabilityError,
    isLoading: capabilityLoading,
    refetch: refetchCapability,
  } = useGetCourseVideoUploadCapabilityQuery();
  const { data: categories = [], error: categoryError } =
    useGetAllVideoCategoryOptionsQuery();
  const [createVideo, createState] = useCreateVideoMutation();
  const uploader = useCourseVideoUpload();
  const busy =
    createState.isLoading ||
    [
      "checking",
      "initializing",
      "uploading",
      "finalizing",
      "confirming",
      "aborting",
    ].includes(uploader.stage);
  const fieldError = (field) => error?.fieldErrors?.[field]?.[0];
  const handleSubmit = async (event) => {
    event.preventDefault();
    if (event.currentTarget.querySelector('[data-uploading="true"]')) {
      setError({ detail: "请等待封面上传完成后再创建视频。", fieldErrors: {} });
      return;
    }
    if (busy || !capability) return;
    const localErrors = {};
    if (!form.title.trim()) localErrors.title = ["请输入视频标题。"];
    if (form.title.length > 200)
      localErrors.title = ["标题不能超过 200 个字符。"];
    if (form.description.length > 2000)
      localErrors.description = ["描述不能超过 2000 个字符。"];
    if (form.categoryIds.length > 10)
      localErrors.categoryIds = ["视频最多选择 10 个分类。"];
    const fileError = validateFile(file, capability);
    if (fileError) localErrors.file = [fileError];
    if (Object.keys(localErrors).length) {
      setError({ detail: "请修正表单字段。", fieldErrors: localErrors });
      return;
    }
    setError(null);
    try {
      const resource =
        uploadedResource ?? (await uploader.upload(file, capability));
      setUploadedResource(resource);
      const saved = await createVideo({
        sourceMediaResourceId: resource.id,
        coverMediaResourceId: coverResource?.id ?? null,
        title: form.title,
        description: form.description.trim() || null,
        categoryIds: form.categoryIds,
      }).unwrap();
      navigate(`/videos/${saved.id}`, { replace: true });
    } catch (requestError) {
      setError(
        requestError?.detail
          ? requestError
          : { detail: requestError?.message ?? "视频上传失败，请重试。" },
      );
    }
  };
  if (capabilityLoading)
    return (
      <div
        className="space-y-4"
        role="status"
        aria-label="正在读取视频上传限制"
      >
        <Skeleton className="h-9 w-56" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  if (capabilityError)
    return (
      <Alert variant="destructive">
        <AlertTitle>无法读取上传限制</AlertTitle>
        <AlertDescription className="mt-2 flex items-center justify-between gap-3">
          <span>{getErrorMessage(capabilityError)}</span>
          <Button variant="outline" size="sm" onClick={refetchCapability}>
            重试
          </Button>
        </AlertDescription>
      </Alert>
    );
  return (
    <form className="min-w-0 max-w-full space-y-5" onSubmit={handleSubmit}>
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">视频管理</p>
          <h1 className="mt-1 text-2xl font-semibold">上传视频</h1>
        </div>
        <div className="flex w-full flex-wrap sm:w-auto sm:justify-end">
          <Button asChild type="button" variant="outline">
            <Link to="/videos">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
        </div>
      </header>
      {uploader.resumeRecord ? (
        <Alert>
          <AlertTitle>发现可恢复的上传会话</AlertTitle>
          <AlertDescription>
            重新选择同一名称、大小、类型和最后修改时间的文件后提交即可恢复；也可以明确取消旧会话后重新开始。
          </AlertDescription>
        </Alert>
      ) : null}
      {error ? (
        <Alert variant="destructive" aria-live="assertive">
          <AlertDescription>
            {getErrorMessage(error, "请修正表单或上传状态后重试。")}
          </AlertDescription>
        </Alert>
      ) : null}
      <section className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="video-title">
            标题 <span aria-hidden="true">*</span>
          </Label>
          <Input
            id="video-title"
            value={form.title}
            maxLength={200}
            aria-invalid={Boolean(fieldError("title"))}
            disabled={busy}
            onChange={(event) =>
              setForm({ ...form, title: event.target.value })
            }
          />
          <p className="text-xs text-muted-foreground">
            {fieldError("title") ?? `${form.title.length}/200`}
          </p>
        </div>
      </section>
      <div className="space-y-1.5">
        <Label htmlFor="video-description">描述</Label>
        <Textarea
          id="video-description"
          rows={5}
          value={form.description}
          maxLength={2000}
          disabled={busy}
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
        categories={categories}
        selectedIds={form.categoryIds}
        disabled={busy || Boolean(categoryError)}
        error={
          fieldError("categoryIds") ??
          (categoryError ? "分类加载失败，请刷新后重试。" : null)
        }
        onChange={(categoryIds) => setForm({ ...form, categoryIds })}
      />
      <VideoCoverControl
        value={coverResource}
        disabled={busy}
        onUploaded={(resource) => {
          setCoverResource(resource);
          setError(null);
        }}
        onClear={() => setCoverResource(null)}
      />
      <section className="space-y-3 border-y py-4">
        <div>
          <Label htmlFor="video-file">
            视频源文件 <span aria-hidden="true">*</span>
          </Label>
          <p className="text-xs text-muted-foreground">
            允许{" "}
            {capability.allowedTypes.map((item) => item.extension).join("、")}
            ，最大 {Math.round(capability.maxSizeBytes / 1024 / 1024)}{" "}
            MB；系统会自动选择简单或分片上传。
          </p>
        </div>
        <VideoFileControl
          id="video-file"
          disabled={busy || !capability}
          accept={capability.allowedTypes
            .flatMap((item) => item.contentTypes)
            .join(",")}
          file={file}
          error={fieldError("file")}
          status={
            uploadedResource ? "源文件已上传并确认；可重试创建视频。" : null
          }
          onFileChange={(nextFile) => {
            setFile(nextFile);
            setUploadedResource(null);
            setError(null);
          }}
        />
        {uploader.stage !== "idle" ? (
          <div className="space-y-2" role="status" aria-live="polite">
            <div className="flex justify-between text-sm">
              <span>{stageLabel(uploader.stage)}</span>
              <span className="tabular-nums">{uploader.progress}%</span>
            </div>
            <Progress value={uploader.progress} />
          </div>
        ) : null}
      </section>
      <div className="flex flex-wrap justify-end gap-2">
        {busy || uploader.resumeRecord ? (
          <Button
            type="button"
            variant="outline"
            disabled={uploader.stage === "aborting"}
            onClick={async () => {
              try {
                await uploader.cancel({
                  abortSession: Boolean(uploader.resumeRecord),
                });
                setError(null);
              } catch (requestError) {
                setError(requestError);
              }
            }}
          >
            <X aria-hidden="true" />
            取消{uploader.resumeRecord ? "上传会话" : "上传"}
          </Button>
        ) : null}
        <Button type="submit" disabled={busy || Boolean(categoryError)}>
          <Upload aria-hidden="true" />
          {busy ? stageLabel(uploader.stage) : "上传并创建视频"}
        </Button>
      </div>
    </form>
  );
}

export default VideoCreate;
