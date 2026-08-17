import { useEffect, useRef, useState } from "react";
import { FileAudio, Upload, X } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import {
  formatAudioFileSize,
  useAudioUploadRunner,
  validateAudioFile,
} from "@/features/audio/useAudioUploadRunner.js";
import { getErrorMessage } from "@/services/problemDetails.js";

const ACTIVE_STAGES = new Set([
  "initializing",
  "uploading",
  "finalizing",
  "confirming",
  "processing",
]);
const INTERRUPTIBLE_STAGES = new Set([
  "initializing",
  "uploading",
  "processing",
]);

function stageText(stage, progress) {
  const labels = {
    idle: null,
    initializing: "正在创建 Uploading 音频资源",
    uploading: `正在上传 ${progress}%`,
    finalizing: "正在完成分片上传",
    confirming: "正在确认上传",
    processing: "上传完成，正在等待音频处理",
    queued: "源文件上传完成，后台处理中",
    completed: "音频已处理完成",
    cancelled: "上传已取消，已创建的音频资源会保留",
    failed: "上传或处理失败",
  };
  return labels[stage];
}

/** Uploads a new or replacement audio source through the shared audio lifecycle. */
export function AudioUploadControl({
  resource = null,
  waitForProcessing = true,
  onStarted,
  onCompleted,
}) {
  const inputRef = useRef(null);
  const controllerRef = useRef(null);
  const mountedRef = useRef(true);
  const stageRef = useRef("idle");
  const [file, setFile] = useState(null);
  const [stage, setStage] = useState("idle");
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState(null);
  const {
    capability,
    capabilityError,
    capabilityLoading,
    refetchCapability,
    uploadAudio,
  } = useAudioUploadRunner();
  const uploading = ACTIVE_STAGES.has(stage);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (INTERRUPTIBLE_STAGES.has(stageRef.current))
        controllerRef.current?.abort();
    };
  }, []);

  const update = (callback) => {
    if (mountedRef.current) callback();
  };

  const transitionTo = (nextStage) => {
    stageRef.current = nextStage;
    update(() => setStage(nextStage));
  };

  const finishUpload = (audio) => {
    transitionTo(
      audio.status === "Failed"
        ? "failed"
        : audio.status === "Ready"
          ? "completed"
          : "queued",
    );
    update(() => {
      setProgress(100);
      setFile(null);
      if (inputRef.current) inputRef.current.value = "";
      if (audio.status === "Failed")
        setError("音频处理失败，可在列表中重新上传或重新处理。");
      onCompleted?.(audio);
    });
  };

  const handleFile = (nextFile) => {
    setError(null);
    transitionTo("idle");
    setProgress(0);
    if (!nextFile) {
      setFile(null);
      return;
    }
    if (!capability) {
      setError("上传限制尚未加载，请稍后重试。");
      return;
    }
    const validationError = validateAudioFile(nextFile, capability);
    if (validationError) {
      setFile(null);
      setError(validationError);
      return;
    }
    setFile(nextFile);
  };

  const handleUpload = async () => {
    if (!file || !capability || uploading) return;
    const validationError = validateAudioFile(file, capability);
    if (validationError) {
      setError(validationError);
      return;
    }
    const controller = new AbortController();
    controllerRef.current = controller;
    setError(null);
    setProgress(0);
    try {
      const audio = await uploadAudio({
        file,
        resource,
        waitForProcessing,
        signal: controller.signal,
        onStarted: (id) => onStarted?.(id, file.name),
        onProgress: (value) => update(() => setProgress(value)),
        onStage: transitionTo,
        shouldContinue: () => mountedRef.current,
      });
      if (audio) finishUpload(audio);
    } catch (requestError) {
      const cancelled =
        requestError?.kind === "aborted" || requestError?.name === "AbortError";
      transitionTo(cancelled ? "cancelled" : "failed");
      update(() => {
        setError(
          cancelled
            ? "上传已取消。已创建的音频资源会保留在列表中。"
            : getErrorMessage(
                requestError,
                requestError?.message ?? "音频上传失败，请重试。",
              ),
        );
      });
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  };

  const accept = capability
    ? Array.from(
        new Set(
          capability.allowedTypes.flatMap((item) => [
            item.extension,
            ...item.contentTypes,
          ]),
        ),
      ).join(",")
    : undefined;

  return (
    <section className="space-y-3 border-y py-4">
      <div>
        <h2 className="text-sm font-semibold">
          {resource ? `重新上传 ${resource.name}` : "上传音频"}
        </h2>
        <p className="mt-1 text-xs text-muted-foreground">
          {capability
            ? `支持 ${capability.allowedTypes.map(({ extension }) => extension).join("、")}，最大 ${formatAudioFileSize(capability.maxSizeBytes)}。`
            : "正在读取服务端上传限制。"}
        </p>
      </div>
      {capabilityError ? (
        <Alert variant="destructive">
          <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
            <span>音频上传限制加载失败。</span>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={refetchCapability}
            >
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <input
          ref={inputRef}
          id={resource ? `audio-retry-${resource.id}` : "audio-upload-file"}
          className="sr-only"
          type="file"
          accept={accept}
          disabled={capabilityLoading || !capability || uploading}
          onClick={(event) => {
            event.currentTarget.value = "";
          }}
          onChange={(event) => handleFile(event.target.files?.[0] ?? null)}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={capabilityLoading || !capability || uploading}
          onClick={() => inputRef.current?.click()}
        >
          <FileAudio aria-hidden="true" />
          {file ? "重新选择" : "选择音频"}
        </Button>
        <Button
          type="button"
          size="sm"
          disabled={!file || uploading}
          onClick={handleUpload}
        >
          <Upload aria-hidden="true" />
          开始上传
        </Button>
        {INTERRUPTIBLE_STAGES.has(stage) ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => controllerRef.current?.abort()}
          >
            <X aria-hidden="true" />
            取消
          </Button>
        ) : null}
        <span
          className="min-w-0 truncate text-xs text-muted-foreground"
          title={file?.name}
        >
          {stageText(stage, progress) ??
            (file
              ? `${file.name} · ${formatAudioFileSize(file.size)}`
              : "尚未选择文件")}
        </span>
      </div>
      {uploading ? (
        <Progress value={progress} aria-label={`音频上传进度 ${progress}%`} />
      ) : null}
      {error ? (
        <p className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </section>
  );
}
