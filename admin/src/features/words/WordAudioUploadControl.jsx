import { useEffect, useRef, useState } from "react";
import { FileAudio, Upload, X } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import { putObject } from "@/services/objectStorageTransport.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useConfirmWordAudioResourceMutation,
  useCreateWordAudioMutation,
  useGetAudioUploadCapabilityQuery,
  usePresignWordAudioMutation,
} from "@/services/wordsApi.js";

const ACTIVE_STAGES = new Set([
  "presigning",
  "uploading",
  "confirming",
  "creating",
]);

function extensionOf(fileName) {
  const dot = fileName.lastIndexOf(".");
  return dot >= 0 ? fileName.slice(dot).toLowerCase() : "";
}

function titleOf(fileName) {
  const extension = extensionOf(fileName);
  const title = extension ? fileName.slice(0, -extension.length) : fileName;
  return title || fileName;
}

function formatFileSize(size) {
  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

function validateFile(file, capability) {
  if (file.size <= 0) return "音频文件不能为空。";
  if (file.size > capability.maxSizeBytes)
    return `音频不能超过 ${formatFileSize(capability.maxSizeBytes)}。`;
  if (file.size >= capability.multipartThresholdBytes)
    return "该文件需要分片上传，当前音频上传契约不支持，请选择较小文件。";
  const extension = extensionOf(file.name);
  const allowed = capability.allowedTypes.find(
    (item) => item.extension.toLowerCase() === extension,
  );
  if (
    !allowed ||
    !allowed.contentTypes.some(
      (contentType) => contentType.toLowerCase() === file.type.toLowerCase(),
    )
  )
    return "文件扩展名或媒体类型不受支持。";
  return null;
}

function stageText(stage, progress) {
  if (stage === "presigning") return "正在准备上传";
  if (stage === "uploading") return `正在上传 ${progress}%`;
  if (stage === "confirming") return "正在确认媒体资源";
  if (stage === "creating") return "正在创建音频并提交处理";
  if (stage === "cancelled") return "上传已取消，可以重新提交。";
  return null;
}

/** Uploads an Audio media resource and creates the matching word AudioClip. */
export function WordAudioUploadControl({ kind, language, onCreated }) {
  const inputRef = useRef(null);
  const controllerRef = useRef(null);
  const requestRef = useRef(null);
  const confirmedResourceRef = useRef(null);
  const mountedRef = useRef(true);
  const [file, setFile] = useState(null);
  const [title, setTitle] = useState("");
  const [stage, setStage] = useState("idle");
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState(null);
  const {
    data: capability,
    error: capabilityError,
    isLoading: capabilityLoading,
    refetch: refetchCapability,
  } = useGetAudioUploadCapabilityQuery();
  const [presign] = usePresignWordAudioMutation();
  const [confirm] = useConfirmWordAudioResourceMutation();
  const [createAudio] = useCreateWordAudioMutation();
  const uploading = ACTIVE_STAGES.has(stage);

  useEffect(
    () => () => {
      mountedRef.current = false;
      controllerRef.current?.abort();
      requestRef.current?.abort?.();
    },
    [],
  );

  const updateIfMounted = (callback) => {
    if (mountedRef.current) callback();
  };

  const handleFile = (nextFile) => {
    confirmedResourceRef.current = null;
    setError(null);
    setStage("idle");
    if (!nextFile) {
      setFile(null);
      setTitle("");
      return;
    }
    if (!capability) {
      setError("上传限制尚未加载，请稍后重试。");
      return;
    }
    const validationError = validateFile(nextFile, capability);
    if (validationError) {
      setFile(null);
      setTitle("");
      setError(validationError);
      return;
    }
    setFile(nextFile);
    setTitle(titleOf(nextFile.name).slice(0, 200));
  };

  const handleUpload = async () => {
    if (uploading || !file || !capability) return;
    const normalizedLanguage = language?.trim() ?? "";
    const normalizedTitle = title.trim();
    if (!normalizedLanguage) {
      setError("请先填写当前发音或例句的语言标签。");
      return;
    }
    if (!normalizedTitle) {
      setError("请输入音频标题。");
      return;
    }
    const validationError = validateFile(file, capability);
    if (validationError) {
      setError(validationError);
      return;
    }

    const controller = new AbortController();
    controllerRef.current = controller;
    setError(null);
    setProgress(0);
    try {
      let resource = confirmedResourceRef.current;
      if (!resource) {
        setStage("presigning");
        requestRef.current = presign(file);
        const signed = await requestRef.current.unwrap();
        setStage("uploading");
        await putObject({
          url: signed.presignedUrl,
          body: file,
          contentType: file.type,
          signal: controller.signal,
          onProgress: (value) => updateIfMounted(() => setProgress(value)),
        });
        setStage("confirming");
        requestRef.current = confirm(signed.resourceId);
        resource = await requestRef.current.unwrap();
        confirmedResourceRef.current = resource;
      }
      setStage("creating");
      requestRef.current = createAudio({
        sourceMediaResourceId: resource.id,
        title: normalizedTitle,
        description: null,
        languageTag: normalizedLanguage,
        kind,
      });
      const audio = await requestRef.current.unwrap();
      updateIfMounted(() => {
        setFile(null);
        setTitle("");
        setStage("idle");
        setProgress(0);
        confirmedResourceRef.current = null;
        if (inputRef.current) inputRef.current.value = "";
        onCreated(audio);
      });
    } catch (requestError) {
      updateIfMounted(() => {
        const cancelled =
          requestError?.kind === "aborted" ||
          requestError?.name === "AbortError";
        setStage(cancelled ? "cancelled" : "failed");
        setError(
          cancelled
            ? "上传已取消。"
            : getErrorMessage(
                requestError,
                "音频上传或创建失败，请保留当前文件后重试。",
              ),
        );
      });
    } finally {
      controllerRef.current = null;
      requestRef.current = null;
    }
  };

  const handleCancel = () => {
    controllerRef.current?.abort();
    requestRef.current?.abort?.();
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
    <section className="space-y-3 border-y py-4" data-uploading={uploading}>
      <div>
        <h3 className="text-sm font-semibold">上传新音频</h3>
        <p className="text-xs text-muted-foreground">
          {capability
            ? `支持 ${capability.allowedTypes.map((item) => item.extension).join("、")}，最大 ${formatFileSize(capability.maxSizeBytes)}。`
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
      <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
        <div className="space-y-1.5">
          <Label htmlFor="word-audio-title">音频标题</Label>
          <Input
            id="word-audio-title"
            value={title}
            maxLength={200}
            disabled={uploading}
            placeholder="选择文件后自动填充"
            onChange={(event) => setTitle(event.target.value)}
          />
        </div>
        <div className="self-end">
          <input
            ref={inputRef}
            id="word-audio-file"
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
            size="sm"
            variant="outline"
            disabled={capabilityLoading || !capability || uploading}
            onClick={() => inputRef.current?.click()}
          >
            <FileAudio aria-hidden="true" />
            {file ? "重新选择音频" : "选择音频"}
          </Button>
        </div>
      </div>
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <Button
          type="button"
          size="sm"
          disabled={!file || uploading || !language?.trim()}
          onClick={handleUpload}
        >
          <Upload aria-hidden="true" />
          上传并提交处理
        </Button>
        {uploading ? (
          <Button
            type="button"
            size="sm"
            variant="ghost"
            onClick={handleCancel}
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
