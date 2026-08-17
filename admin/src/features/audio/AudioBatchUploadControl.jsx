import { useEffect, useRef, useState } from "react";
import { FileAudio, RotateCcw, X } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import {
  formatAudioFileSize,
  useAudioUploadRunner,
  validateAudioFile,
} from "@/features/audio/useAudioUploadRunner.js";
import { getErrorMessage } from "@/services/problemDetails.js";

export const MAX_CONCURRENT_FILES = 2;

const INTERRUPTIBLE_STAGES = new Set([
  "initializing",
  "uploading",
  "processing",
]);

function stageText(stage, progress) {
  const labels = {
    waiting: "等待上传",
    initializing: "正在创建音频资源",
    uploading: `正在上传 ${progress}%`,
    finalizing: "正在完成分片上传",
    confirming: "正在确认上传",
    processing: "正在等待音频处理",
    queued: "后台处理中",
    completed: "上传完成",
    cancelled: "上传已取消。",
    failed: "上传失败",
  };
  return labels[stage];
}

function createEntry(file, index, capability) {
  const error = validateAudioFile(file, capability);
  return {
    id: `${file.name}:${file.size}:${file.lastModified}:${index}`,
    file,
    stage: error ? "failed" : "waiting",
    progress: 0,
    audioResourceId: null,
    finalName: null,
    error,
    controller: null,
  };
}

/** Uploads multiple independent audio files with a bounded worker queue. */
export function AudioBatchUploadControl({ onStarted, onTerminal }) {
  const inputRef = useRef(null);
  const mountedRef = useRef(true);
  const entriesRef = useRef([]);
  const queueRef = useRef([]);
  const activeCountRef = useRef(0);
  const selectionIndexRef = useRef(0);
  const pumpRef = useRef(() => {});
  const [entries, setEntries] = useState([]);
  const {
    capability,
    capabilityError,
    capabilityLoading,
    refetchCapability,
    uploadAudio,
  } = useAudioUploadRunner();

  const replaceEntry = (entryId, update) => {
    const next = entriesRef.current.map((entry) =>
      entry.id === entryId ? update(entry) : entry,
    );
    entriesRef.current = next;
    if (mountedRef.current) setEntries(next);
    return next.find((entry) => entry.id === entryId);
  };

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      for (const entry of entriesRef.current) entry.controller?.abort();
    };
  }, []);

  const runEntry = async (entryId) => {
    const entry = entriesRef.current.find((value) => value.id === entryId);
    if (!entry || entry.stage !== "waiting") return;

    const validationError = validateAudioFile(entry.file, capability);
    if (validationError) {
      const terminal = replaceEntry(entryId, (value) => ({
        ...value,
        stage: "failed",
        error: validationError,
      }));
      onTerminal?.(terminal);
      return;
    }

    activeCountRef.current += 1;
    const controller = new AbortController();
    replaceEntry(entryId, (value) => ({
      ...value,
      stage: "initializing",
      progress: 0,
      error: null,
      controller,
    }));

    try {
      const current = entriesRef.current.find((value) => value.id === entryId);
      const result = await uploadAudio({
        file: current.file,
        resource: current.audioResourceId
          ? { id: current.audioResourceId }
          : null,
        signal: controller.signal,
        onStarted: (audioResourceId) => {
          replaceEntry(entryId, (value) => ({
            ...value,
            audioResourceId,
          }));
          onStarted?.(audioResourceId, current.file.name);
        },
        onProgress: (progress) =>
          replaceEntry(entryId, (value) => ({ ...value, progress })),
        onStage: (stage) =>
          replaceEntry(entryId, (value) => ({ ...value, stage })),
        shouldContinue: () => mountedRef.current,
      });
      if (!result) return;

      const processingFailed = result.status === "Failed";
      const terminal = replaceEntry(entryId, (value) => ({
        ...value,
        stage: processingFailed
          ? "failed"
          : result.status === "Ready"
            ? "completed"
            : "queued",
        progress: 100,
        audioResourceId: result.id ?? value.audioResourceId,
        finalName: result.name ?? value.file.name,
        error: processingFailed ? "音频处理失败，可使用原文件重试上传。" : null,
        controller: null,
      }));
      onTerminal?.(terminal);
    } catch (error) {
      const cancelled =
        error?.kind === "aborted" || error?.name === "AbortError";
      const terminal = replaceEntry(entryId, (value) => ({
        ...value,
        stage: cancelled ? "cancelled" : "failed",
        error: cancelled
          ? null
          : getErrorMessage(error, error?.message ?? "音频上传失败，请重试。"),
        controller: null,
      }));
      onTerminal?.(terminal);
    } finally {
      activeCountRef.current -= 1;
      pumpRef.current();
    }
  };

  pumpRef.current = () => {
    while (
      activeCountRef.current < MAX_CONCURRENT_FILES &&
      queueRef.current.length > 0
    ) {
      const entryId = queueRef.current.shift();
      void runEntry(entryId);
    }
  };

  const enqueue = (entryId) => {
    queueRef.current.push(entryId);
    pumpRef.current();
  };

  const handleFiles = (files) => {
    if (!capability || files.length === 0) return;
    const selected = Array.from(files, (file) => {
      const index = selectionIndexRef.current;
      selectionIndexRef.current += 1;
      return createEntry(file, index, capability);
    });
    const next = [...entriesRef.current, ...selected];
    entriesRef.current = next;
    setEntries(next);

    for (const entry of selected) {
      if (entry.stage === "waiting") queueRef.current.push(entry.id);
      else onTerminal?.(entry);
    }
    pumpRef.current();
  };

  const retry = (entryId) => {
    const entry = replaceEntry(entryId, (value) => ({
      ...value,
      stage: "waiting",
      progress: 0,
      finalName: null,
      error: null,
      controller: null,
    }));
    if (entry) enqueue(entryId);
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
    <section className="space-y-3 border-y py-4" aria-label="批量音频上传">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold">批量上传音频</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {capability
              ? `支持 ${capability.allowedTypes.map(({ extension }) => extension).join("、")}，单个文件最大 ${formatAudioFileSize(capability.maxSizeBytes)}。`
              : "正在读取服务端上传限制。"}
          </p>
        </div>
        <input
          ref={inputRef}
          id="audio-batch-upload-files"
          className="sr-only"
          type="file"
          multiple
          accept={accept}
          disabled={capabilityLoading || !capability}
          onClick={(event) => {
            event.currentTarget.value = "";
          }}
          onChange={(event) => handleFiles(event.target.files ?? [])}
        />
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={capabilityLoading || !capability}
          onClick={() => inputRef.current?.click()}
        >
          <FileAudio aria-hidden="true" />
          选择音频
        </Button>
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
              <RotateCcw aria-hidden="true" />
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}

      {entries.length > 0 ? (
        <ul className="divide-y border-y" aria-label="音频上传结果">
          {entries.map((entry) => {
            const displayName =
              entry.finalName && entry.finalName !== entry.file.name
                ? `${entry.file.name} -> ${entry.finalName}`
                : entry.file.name;
            return (
              <li
                key={entry.id}
                data-entry-id={entry.id}
                className="grid min-w-0 gap-2 py-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
              >
                <div className="min-w-0 space-y-1">
                  <p
                    className="truncate text-sm font-medium"
                    title={displayName}
                  >
                    {displayName}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatAudioFileSize(entry.file.size)} ·{" "}
                    {stageText(entry.stage, entry.progress)}
                  </p>
                  {INTERRUPTIBLE_STAGES.has(entry.stage) ? (
                    <Progress
                      value={entry.progress}
                      aria-label={`${entry.file.name} 上传进度 ${entry.progress}%`}
                    />
                  ) : null}
                  {entry.error ? (
                    <p className="text-xs text-destructive" role="alert">
                      {entry.error}
                    </p>
                  ) : null}
                </div>
                <div className="flex items-center gap-1">
                  {INTERRUPTIBLE_STAGES.has(entry.stage) ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="ghost"
                      aria-label={`取消 ${entry.file.name}`}
                      onClick={() => entry.controller?.abort()}
                    >
                      <X aria-hidden="true" />
                      取消
                    </Button>
                  ) : null}
                  {entry.stage === "failed" ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      aria-label={`重试 ${entry.file.name}`}
                      onClick={() => retry(entry.id)}
                    >
                      <RotateCcw aria-hidden="true" />
                      重试
                    </Button>
                  ) : null}
                </div>
              </li>
            );
          })}
        </ul>
      ) : null}
    </section>
  );
}
