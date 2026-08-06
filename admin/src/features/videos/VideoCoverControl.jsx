import { useRef, useState } from "react";
import { ImageOff, ImageUp, X } from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import { useVideoCoverUpload } from "@/features/videos/useCourseVideoUpload.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetVideoCoverUploadCapabilityQuery } from "@/services/videoUploadApi.js";

function validateFile(file, capability) {
  if (!file || file.size <= 0 || file.size > capability.maxSizeBytes)
    return "封面图片大小不符合服务端限制。";
  const dot = file.name.lastIndexOf(".");
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  const allowed = capability.allowedTypes.find(
    (item) => item.extension.toLowerCase() === extension,
  );
  if (!allowed || !allowed.contentTypes.includes(file.type))
    return "封面图片的扩展名或媒体类型不受支持。";
  return null;
}

/** Uploads and confirms a VideoCover resource through the Axios OSS transport. */
export function VideoCoverControl({ value, disabled, onUploaded, onClear }) {
  const inputRef = useRef(null);
  const [error, setError] = useState(null);
  const [failedPreviewUrl, setFailedPreviewUrl] = useState(null);
  const { data: capability, error: capabilityError } =
    useGetVideoCoverUploadCapabilityQuery();
  const uploader = useVideoCoverUpload();
  const uploading = [
    "checking",
    "initializing",
    "uploading",
    "finalizing",
    "confirming",
    "aborting",
  ].includes(uploader.stage);

  const handleFile = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || !capability || disabled || uploading) return;
    const validationError = validateFile(file, capability);
    if (validationError) {
      setError(validationError);
      return;
    }
    setError(null);
    try {
      const resource = await uploader.upload(file, capability);
      onUploaded(resource);
    } catch (requestError) {
      if (requestError?.name !== "AbortError" && requestError?.kind !== "aborted")
        setError(getErrorMessage(requestError, "封面上传失败，请重新选择图片。"));
    }
  };

  return (
    <section className="space-y-3" data-uploading={uploading}>
      <div>
        <Label htmlFor="video-cover">自定义封面</Label>
        <p className="text-xs text-muted-foreground">
          可选。未上传或清除后，将使用视频自动抽帧封面。
        </p>
      </div>
      {value ? (
        <div className="grid gap-3 rounded-lg border p-3 sm:grid-cols-[12rem_minmax(0,1fr)]">
          <div className="aspect-video overflow-hidden rounded-md border bg-muted">
            {failedPreviewUrl === value.url ? (
              <div className="grid h-full place-items-center text-muted-foreground">
                <ImageOff aria-hidden="true" />
              </div>
            ) : (
              <img
                src={value.url}
                alt="视频封面预览"
                className="h-full w-full object-cover"
                onError={() => {
                  setFailedPreviewUrl(value.url);
                  setError("封面预览加载失败，资源仍可保存；请检查对象存储访问配置。");
                }}
              />
            )}
          </div>
          <div className="min-w-0 self-center">
            <p className="truncate text-sm font-medium">{value.originalName}</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={disabled || uploading || !capability}
                onClick={() => inputRef.current?.click()}
              >
                <ImageUp aria-hidden="true" />
                替换封面
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={disabled || uploading}
                onClick={onClear}
              >
                <ImageOff aria-hidden="true" />
                清除封面
              </Button>
            </div>
          </div>
        </div>
      ) : (
        <Button
          type="button"
          variant="outline"
          disabled={disabled || uploading || !capability}
          onClick={() => inputRef.current?.click()}
        >
          <ImageUp aria-hidden="true" />
          上传封面
        </Button>
      )}
      <Input
        ref={inputRef}
        id="video-cover"
        type="file"
        className="sr-only"
        accept={capability?.allowedTypes.flatMap((item) => item.contentTypes).join(",")}
        disabled={disabled || uploading}
        onChange={handleFile}
      />
      {uploading ? (
        <div className="flex items-center gap-3" role="status">
          <Progress value={uploader.progress} className="max-w-sm flex-1" />
          <span className="text-xs tabular-nums text-muted-foreground">{uploader.progress}%</span>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="取消封面上传"
            onClick={() =>
              uploader
                .cancel({ abortSession: Boolean(uploader.resumeRecord) })
                .catch((requestError) => setError(getErrorMessage(requestError)))
            }
          >
            <X aria-hidden="true" />
          </Button>
        </div>
      ) : null}
      {uploader.resumeRecord && !uploading ? (
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-xs text-muted-foreground">
            检测到未完成的封面分片会话，请选择同一文件继续。
          </p>
          <Button
            type="button"
            size="sm"
            variant="ghost"
            onClick={() =>
              uploader
                .cancel({ abortSession: true })
                .catch((requestError) => setError(getErrorMessage(requestError)))
            }
          >
            <X aria-hidden="true" />
            取消旧会话
          </Button>
        </div>
      ) : null}
      {error || capabilityError ? (
        <p className="text-sm text-destructive" role="alert">
          {error ?? getErrorMessage(capabilityError, "无法读取封面上传限制。")}
        </p>
      ) : null}
    </section>
  );
}
