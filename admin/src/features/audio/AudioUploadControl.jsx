import { useEffect, useRef, useState } from "react";
import { FileAudio, Upload, X } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import { putObject } from "@/services/objectStorageTransport.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useAbortAudioMultipartMutation,
  useCompleteAudioMultipartMutation,
  useConfirmAudioUploadMutation,
  useGetAudioUploadCapabilityQuery,
  useInitializeMultipartAudioUploadMutation,
  useInitializeSimpleAudioUploadMutation,
  useLazyGetAdminAudioResourceQuery,
  useLazyGetAudioMultipartStatusQuery,
  usePresignAudioMultipartPartsMutation,
  useRetryAudioUploadMutation,
} from "@/services/audioApi.js";

const MAX_CONCURRENT_PARTS = 3;
const POLL_INTERVAL_MS = 1500;
const MAX_MULTIPART_POLLS = 120;
const MAX_PROCESSING_POLLS = 240;
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

function formatFileSize(size) {
  if (size < 1024 * 1024) return `${Math.ceil(size / 1024)} KB`;
  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

function delay(ms, signal) {
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(resolve, ms);
    signal.addEventListener(
      "abort",
      () => {
        window.clearTimeout(timer);
        reject(new DOMException("Aborted", "AbortError"));
      },
      { once: true },
    );
  });
}

async function runPool(tasks, concurrency) {
  let index = 0;
  async function worker() {
    while (index < tasks.length) {
      const task = tasks[index];
      index += 1;
      await task();
    }
  }
  await Promise.all(
    Array.from({ length: Math.min(concurrency, tasks.length) }, worker),
  );
}

function validateFile(file, capability) {
  if (file.size <= 0) return "不能上传空文件。";
  if (file.size > capability.maxSizeBytes)
    return `文件不能超过 ${formatFileSize(capability.maxSizeBytes)}。`;
  const dot = file.name.lastIndexOf(".");
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  const allowed = capability.allowedTypes.some(
    (item) =>
      item.extension.toLowerCase() === extension &&
      item.contentTypes.includes(file.type),
  );
  return allowed ? null : "文件扩展名或媒体类型不受支持。";
}

function stageText(stage, progress) {
  const labels = {
    idle: null,
    initializing: "正在创建 Uploading 音频资源",
    uploading: `正在上传 ${progress}%`,
    finalizing: "正在完成分片上传",
    confirming: "正在确认上传",
    processing: "上传完成，正在等待音频处理",
    completed: "音频已处理完成",
    cancelled: "上传已取消，已创建的音频资源会保留",
    failed: "上传或处理失败",
  };
  return labels[stage];
}

/** Uploads a new or replacement audio source through the shared audio lifecycle. */
export function AudioUploadControl({
  resource = null,
  onStarted,
  onCompleted,
}) {
  const inputRef = useRef(null);
  const controllerRef = useRef(null);
  const requestRef = useRef(null);
  const multipartSessionRef = useRef(null);
  const mountedRef = useRef(true);
  const stageRef = useRef("idle");
  const [file, setFile] = useState(null);
  const [stage, setStage] = useState("idle");
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState(null);
  const {
    data: capability,
    error: capabilityError,
    isLoading: capabilityLoading,
    refetch: refetchCapability,
  } = useGetAudioUploadCapabilityQuery();
  const [initializeSimple] = useInitializeSimpleAudioUploadMutation();
  const [initializeMultipart] = useInitializeMultipartAudioUploadMutation();
  const [retryUpload] = useRetryAudioUploadMutation();
  const [abortMultipart] = useAbortAudioMultipartMutation();
  const [presignParts] = usePresignAudioMultipartPartsMutation();
  const [completeMultipart] = useCompleteAudioMultipartMutation();
  const [getMultipartStatus] = useLazyGetAudioMultipartStatusQuery();
  const [confirmUpload] = useConfirmAudioUploadMutation();
  const [getAudioResource] = useLazyGetAdminAudioResourceQuery();
  const uploading = ACTIVE_STAGES.has(stage);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (INTERRUPTIBLE_STAGES.has(stageRef.current)) {
        controllerRef.current?.abort();
        requestRef.current?.abort?.();
      }
    };
  }, []);

  const update = (callback) => {
    if (mountedRef.current) callback();
  };

  const transitionTo = (nextStage) => {
    stageRef.current = nextStage;
    update(() => setStage(nextStage));
  };

  const waitForMultipart = async (sessionId, initial, signal) => {
    let status = initial;
    for (let count = 0; count < MAX_MULTIPART_POLLS; count += 1) {
      if (status.status === "Completed") return status;
      if (["Failed", "Expired", "Aborted"].includes(status.status))
        throw new Error(`分片上传已进入终态：${status.status}。`);
      await delay(POLL_INTERVAL_MS, signal);
      requestRef.current = getMultipartStatus(sessionId, false);
      status = await requestRef.current.unwrap();
    }
    throw new Error("服务端仍在完成分片上传，请稍后刷新列表查看状态。");
  };

  const waitForProcessing = async (audioResourceId, signal) => {
    for (let count = 0; count < MAX_PROCESSING_POLLS; count += 1) {
      requestRef.current = getAudioResource(audioResourceId, false);
      const audio = await requestRef.current.unwrap();
      if (["Ready", "Failed"].includes(audio.status)) return audio;
      await delay(POLL_INTERVAL_MS, signal);
    }
    throw new Error("音频仍在处理中，请稍后刷新列表查看状态。");
  };

  const uploadMultipart = async (file, initialized, capability, signal) => {
    const { multipartSessionId: sessionId, partSize, partCount } = initialized;
    if (!sessionId || !partSize || !partCount)
      throw new Error("服务端未返回完整的分片上传信息。");
    multipartSessionRef.current = sessionId;
    let uploadedBytes = 0;
    const uploadedParts = [];
    for (
      let offset = 0;
      offset < partCount;
      offset += capability.partPresignBatchLimit
    ) {
      const partNumbers = Array.from(
        {
          length: Math.min(
            capability.partPresignBatchLimit,
            partCount - offset,
          ),
        },
        (_, index) => offset + index + 1,
      );
      requestRef.current = presignParts({ sessionId, partNumbers });
      const presigns = await requestRef.current.unwrap();
      const returnedNumbers = presigns.map(({ partNumber }) => partNumber);
      if (
        returnedNumbers.length !== partNumbers.length ||
        new Set(returnedNumbers).size !== returnedNumbers.length ||
        returnedNumbers.some((partNumber) => !partNumbers.includes(partNumber))
      )
        throw new Error("服务端返回的分片预签名集合与请求不一致。");
      await runPool(
        presigns.map((part) => async () => {
          const start = (part.partNumber - 1) * partSize;
          const blob = file.slice(
            start,
            Math.min(start + partSize, file.size),
            file.type,
          );
          if (blob.size !== part.contentLength)
            throw new Error(`第 ${part.partNumber} 片长度与服务端契约不一致。`);
          const eTag = await putObject({
            url: part.presignedUrl,
            body: blob,
            contentType: file.type,
            signal,
          });
          if (!eTag)
            throw new Error("对象存储未返回 ETag，请检查对象存储 CORS 配置。");
          uploadedParts.push({ partNumber: part.partNumber, eTag });
          uploadedBytes += blob.size;
          update(() =>
            setProgress(
              Math.min(99, Math.round((uploadedBytes / file.size) * 100)),
            ),
          );
        }),
        MAX_CONCURRENT_PARTS,
      );
    }
    const parts = uploadedParts.sort(
      (left, right) => left.partNumber - right.partNumber,
    );
    transitionTo("finalizing");
    requestRef.current = completeMultipart({ sessionId, parts });
    const completing = await requestRef.current.unwrap();
    await waitForMultipart(sessionId, completing, signal);
    multipartSessionRef.current = null;
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
    const validationError = validateFile(nextFile, capability);
    if (validationError) {
      setFile(null);
      setError(validationError);
      return;
    }
    setFile(nextFile);
  };

  const handleUpload = async () => {
    if (!file || !capability || uploading) return;
    const validationError = validateFile(file, capability);
    if (validationError) {
      setError(validationError);
      return;
    }
    const controller = new AbortController();
    controllerRef.current = controller;
    setError(null);
    setProgress(0);
    transitionTo("initializing");
    try {
      requestRef.current = resource
        ? retryUpload({ audioResourceId: resource.id, file })
        : file.size < capability.multipartThresholdBytes
          ? initializeSimple(file)
          : initializeMultipart(file);
      const initialized = await requestRef.current.unwrap();
      onStarted?.(initialized.audioResourceId);
      transitionTo("uploading");
      if (initialized.presignedUrl) {
        await putObject({
          url: initialized.presignedUrl,
          body: file,
          contentType: file.type,
          signal: controller.signal,
          onProgress: (value) => update(() => setProgress(value)),
        });
      } else {
        await uploadMultipart(file, initialized, capability, controller.signal);
      }
      transitionTo("confirming");
      requestRef.current = confirmUpload(initialized.audioResourceId);
      await requestRef.current.unwrap();
      transitionTo("processing");
      if (!mountedRef.current) return;
      const audio = await waitForProcessing(
        initialized.audioResourceId,
        controller.signal,
      );
      transitionTo(audio.status === "Ready" ? "completed" : "failed");
      update(() => {
        setProgress(100);
        setFile(null);
        if (inputRef.current) inputRef.current.value = "";
        if (audio.status === "Failed")
          setError("音频处理失败，可在列表中重新上传或重新处理。");
        onCompleted?.(audio);
      });
    } catch (requestError) {
      const multipartSessionId = multipartSessionRef.current;
      multipartSessionRef.current = null;
      if (multipartSessionId) {
        try {
          await abortMultipart(multipartSessionId).unwrap();
        } catch {
          // The original upload error remains the actionable message.
        }
      }
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
      controllerRef.current = null;
      requestRef.current = null;
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
            ? `支持 ${capability.allowedTypes.map(({ extension }) => extension).join("、")}，最大 ${formatFileSize(capability.maxSizeBytes)}。`
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
            onClick={() => {
              controllerRef.current?.abort();
              requestRef.current?.abort?.();
            }}
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
              ? `${file.name} · ${formatFileSize(file.size)}`
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
