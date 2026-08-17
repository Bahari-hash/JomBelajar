import { useCallback } from "react";
import { putObject } from "@/services/objectStorageTransport.js";
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
const INTERRUPTIBLE_STAGES = new Set([
  "initializing",
  "uploading",
  "processing",
]);

export function formatAudioFileSize(size) {
  if (size < 1024 * 1024) return `${Math.ceil(size / 1024)} KB`;
  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

export function validateAudioFile(file, capability) {
  if (file.size <= 0) return "不能上传空文件。";
  if (file.size > capability.maxSizeBytes)
    return `文件不能超过 ${formatAudioFileSize(capability.maxSizeBytes)}。`;
  const dot = file.name.lastIndexOf(".");
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  const allowed = capability.allowedTypes.some(
    (item) =>
      item.extension.toLowerCase() === extension &&
      item.contentTypes.includes(file.type),
  );
  return allowed ? null : "文件扩展名或媒体类型不受支持。";
}

function abortError() {
  return new DOMException("Aborted", "AbortError");
}

function delay(ms, signal) {
  if (signal.aborted) return Promise.reject(abortError());
  return new Promise((resolve, reject) => {
    const timer = window.setTimeout(resolve, ms);
    signal.addEventListener(
      "abort",
      () => {
        window.clearTimeout(timer);
        reject(abortError());
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

/** Runs independent administrator audio uploads through the shared lifecycle. */
export function useAudioUploadRunner() {
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

  const uploadAudio = useCallback(
    async ({
      file,
      resource = null,
      waitForProcessing = true,
      signal: externalSignal,
      onStarted,
      onProgress,
      onStage,
      shouldContinue = () => true,
    }) => {
      if (!capability) throw new Error("上传限制尚未加载，请稍后重试。");
      const validationError = validateAudioFile(file, capability);
      if (validationError) throw new Error(validationError);

      const fallbackController = externalSignal ? null : new AbortController();
      const signal = externalSignal ?? fallbackController.signal;
      if (signal.aborted) throw abortError();

      let currentRequest = null;
      let multipartSessionId = null;
      let stage = "idle";
      const transitionTo = (nextStage) => {
        stage = nextStage;
        onStage?.(nextStage);
      };
      const abortCurrentRequest = () => {
        if (INTERRUPTIBLE_STAGES.has(stage)) currentRequest?.abort?.();
      };
      signal.addEventListener("abort", abortCurrentRequest);

      const waitForMultipart = async (sessionId, initial) => {
        let status = initial;
        for (let count = 0; count < MAX_MULTIPART_POLLS; count += 1) {
          if (status.status === "Completed") return status;
          if (["Failed", "Expired", "Aborted"].includes(status.status))
            throw new Error(`分片上传已进入终态：${status.status}。`);
          await delay(POLL_INTERVAL_MS, signal);
          currentRequest = getMultipartStatus(sessionId, false);
          status = await currentRequest.unwrap();
        }
        throw new Error("服务端仍在完成分片上传，请稍后刷新列表查看状态。");
      };

      const waitForProcessingResult = async (audioResourceId) => {
        for (let count = 0; count < MAX_PROCESSING_POLLS; count += 1) {
          currentRequest = getAudioResource(audioResourceId, false);
          const audio = await currentRequest.unwrap();
          if (["Ready", "Failed"].includes(audio.status)) return audio;
          await delay(POLL_INTERVAL_MS, signal);
        }
        throw new Error("音频仍在处理中，请稍后刷新列表查看状态。");
      };

      const uploadMultipart = async (initialized) => {
        const {
          multipartSessionId: sessionId,
          partSize,
          partCount,
        } = initialized;
        if (!sessionId || !partSize || !partCount)
          throw new Error("服务端未返回完整的分片上传信息。");
        multipartSessionId = sessionId;
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
          currentRequest = presignParts({ sessionId, partNumbers });
          const presigns = await currentRequest.unwrap();
          const returnedNumbers = presigns.map(({ partNumber }) => partNumber);
          if (
            returnedNumbers.length !== partNumbers.length ||
            new Set(returnedNumbers).size !== returnedNumbers.length ||
            returnedNumbers.some(
              (partNumber) => !partNumbers.includes(partNumber),
            )
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
                throw new Error(
                  `第 ${part.partNumber} 片长度与服务端契约不一致。`,
                );
              const eTag = await putObject({
                url: part.presignedUrl,
                body: blob,
                contentType: file.type,
                signal,
              });
              if (!eTag)
                throw new Error(
                  "对象存储未返回 ETag，请检查对象存储 CORS 配置。",
                );
              uploadedParts.push({ partNumber: part.partNumber, eTag });
              uploadedBytes += blob.size;
              onProgress?.(
                Math.min(99, Math.round((uploadedBytes / file.size) * 100)),
              );
            }),
            MAX_CONCURRENT_PARTS,
          );
        }
        const parts = uploadedParts.sort(
          (left, right) => left.partNumber - right.partNumber,
        );
        transitionTo("finalizing");
        currentRequest = completeMultipart({ sessionId, parts });
        const completing = await currentRequest.unwrap();
        await waitForMultipart(sessionId, completing);
        multipartSessionId = null;
      };

      try {
        transitionTo("initializing");
        currentRequest = resource
          ? retryUpload({ audioResourceId: resource.id, file })
          : file.size < capability.multipartThresholdBytes
            ? initializeSimple(file)
            : initializeMultipart(file);
        const initialized = await currentRequest.unwrap();
        onStarted?.(initialized.audioResourceId);
        transitionTo("uploading");
        if (initialized.presignedUrl) {
          await putObject({
            url: initialized.presignedUrl,
            body: file,
            contentType: file.type,
            signal,
            onProgress,
          });
        } else {
          await uploadMultipart(initialized);
        }
        transitionTo("confirming");
        currentRequest = confirmUpload(initialized.audioResourceId);
        await currentRequest.unwrap();
        if (!shouldContinue()) return null;

        if (!waitForProcessing) {
          currentRequest = getAudioResource(initialized.audioResourceId, false);
          return await currentRequest.unwrap();
        }

        transitionTo("processing");
        return await waitForProcessingResult(initialized.audioResourceId);
      } catch (error) {
        const sessionId = multipartSessionId;
        multipartSessionId = null;
        if (sessionId) {
          try {
            await abortMultipart(sessionId).unwrap();
          } catch {
            // Preserve the original upload error as the actionable failure.
          }
        }
        throw error;
      } finally {
        signal.removeEventListener("abort", abortCurrentRequest);
        currentRequest = null;
      }
    },
    [
      abortMultipart,
      capability,
      completeMultipart,
      confirmUpload,
      getAudioResource,
      getMultipartStatus,
      initializeMultipart,
      initializeSimple,
      presignParts,
      retryUpload,
    ],
  );

  return {
    capability,
    capabilityError,
    capabilityLoading,
    refetchCapability,
    uploadAudio,
  };
}
