import { useEffect, useRef, useState } from "react";
import { putObject } from "@/services/objectStorageTransport.js";
import {
  useAbortCourseVideoMultipartMutation,
  useCompleteCourseVideoMultipartMutation,
  useConfirmCourseVideoResourceMutation,
  useCreateCourseVideoMultipartMutation,
  useLazyGetCourseVideoMultipartStatusQuery,
  usePresignCourseVideoMutation,
  usePresignCourseVideoPartsMutation,
} from "@/services/videoUploadApi.js";

const STORAGE_KEY = "tinylang.courseVideoUpload.v1";
const MAX_CONCURRENT_PARTS = 3;
const FINALIZATION_POLL_MS = 1500;
const MAX_FINALIZATION_POLLS = 120;

function fingerprint(file) {
  return {
    name: file.name,
    size: file.size,
    type: file.type,
    lastModified: file.lastModified,
  };
}

function sameFingerprint(file, saved) {
  const current = fingerprint(file);
  return Object.keys(current).every((key) => current[key] === saved?.[key]);
}

export function readCourseVideoResumeRecord() {
  try {
    const parsed = JSON.parse(sessionStorage.getItem(STORAGE_KEY));
    if (
      typeof parsed?.sessionId !== "string" ||
      typeof parsed?.resourceId !== "string" ||
      !parsed.fingerprint ||
      typeof parsed.fingerprint.name !== "string" ||
      !Number.isFinite(parsed.fingerprint.size)
    ) {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return parsed;
  } catch {
    sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

function saveResumeRecord(session) {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

function clearResumeRecord() {
  sessionStorage.removeItem(STORAGE_KEY);
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

/** Coordinates capability-driven simple and resumable multipart CourseVideo uploads. */
export function useCourseVideoUpload() {
  const controllerRef = useRef(null);
  const sessionRef = useRef(readCourseVideoResumeRecord());
  const mountedRef = useRef(true);
  const [progress, setProgress] = useState(0);
  const [stage, setStage] = useState("idle");
  const [resumeRecord, setResumeRecord] = useState(sessionRef.current);
  const [presignSimple] = usePresignCourseVideoMutation();
  const [createMultipart] = useCreateCourseVideoMultipartMutation();
  const [presignParts] = usePresignCourseVideoPartsMutation();
  const [getStatus] = useLazyGetCourseVideoMultipartStatusQuery();
  const [completeMultipart] = useCompleteCourseVideoMultipartMutation();
  const [abortMultipart] = useAbortCourseVideoMultipartMutation();
  const [confirmResource] = useConfirmCourseVideoResourceMutation();

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      controllerRef.current?.abort();
    };
  }, []);

  const updateStage = (value) => mountedRef.current && setStage(value);
  const updateProgress = (value) => mountedRef.current && setProgress(value);

  const waitForCompletion = async (sessionId, initial, signal) => {
    let status = initial;
    for (let count = 0; count < MAX_FINALIZATION_POLLS; count += 1) {
      if (status.status === "Completed") return status;
      if (["Failed", "Expired", "Aborted"].includes(status.status))
        throw new Error(`上传会话已进入终态：${status.status}。`);
      await delay(FINALIZATION_POLL_MS, signal);
      status = await getStatus(sessionId, false).unwrap();
    }
    throw new Error("服务端仍在归档视频，请稍后重新选择同一文件继续检查。");
  };

  const uploadMultipart = async (file, capability, signal) => {
    let record = sessionRef.current;
    let status;
    if (record) {
      if (!sameFingerprint(file, record.fingerprint))
        throw new Error("请选择与待恢复会话完全相同的文件，或先取消旧会话。");
      updateStage("checking");
      status = await getStatus(record.sessionId, false).unwrap();
      if (["Failed", "Expired", "Aborted"].includes(status.status)) {
        clearResumeRecord();
        sessionRef.current = null;
        setResumeRecord(null);
        throw new Error("旧上传会话已不可恢复，请重新开始上传。");
      }
    } else {
      updateStage("initializing");
      const created = await createMultipart(file).unwrap();
      record = {
        sessionId: created.sessionId,
        resourceId: created.resourceId,
        fingerprint: fingerprint(file),
      };
      sessionRef.current = record;
      saveResumeRecord(record);
      setResumeRecord(record);
      status = { ...created, status: "Initiated", uploadedParts: [] };
    }

    if (status.status === "Completed") {
      updateStage("confirming");
      const resource = await confirmResource(record.resourceId).unwrap();
      clearResumeRecord();
      sessionRef.current = null;
      setResumeRecord(null);
      return resource;
    }
    if (status.status !== "Initiated") {
      updateStage("finalizing");
      const completed = await waitForCompletion(
        record.sessionId,
        status,
        signal,
      );
      updateStage("confirming");
      const resource = await confirmResource(completed.resourceId).unwrap();
      clearResumeRecord();
      sessionRef.current = null;
      setResumeRecord(null);
      return resource;
    }

    updateStage("uploading");
    const uploaded = new Map(
      status.uploadedParts.map((part) => [part.partNumber, part.eTag]),
    );
    let completedBytes = status.uploadedParts.reduce(
      (sum, part) => sum + (part.size ?? 0),
      0,
    );
    updateProgress(Math.round((completedBytes / file.size) * 100));
    const missing = Array.from(
      { length: status.partCount },
      (_, index) => index + 1,
    ).filter((partNumber) => !uploaded.has(partNumber));
    for (
      let offset = 0;
      offset < missing.length;
      offset += capability.partPresignBatchLimit
    ) {
      const partNumbers = missing.slice(
        offset,
        offset + capability.partPresignBatchLimit,
      );
      const presigns = await presignParts({
        sessionId: record.sessionId,
        partNumbers,
      }).unwrap();
      const returnedNumbers = presigns.map(({ partNumber }) => partNumber);
      if (
        returnedNumbers.length !== partNumbers.length ||
        new Set(returnedNumbers).size !== returnedNumbers.length ||
        returnedNumbers.some((partNumber) => !partNumbers.includes(partNumber))
      )
        throw new Error("服务端返回的分片预签名集合与请求不一致。");
      const tasks = presigns.map((part) => async () => {
        const start = (part.partNumber - 1) * status.partSize;
        const blob = file.slice(
          start,
          Math.min(start + status.partSize, file.size),
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
          throw new Error(
            "对象存储未返回 ETag。请确认 CORS 暴露 Access-Control-Expose-Headers: ETag。",
          );
        uploaded.set(part.partNumber, eTag);
        completedBytes += blob.size;
        updateProgress(
          Math.min(99, Math.round((completedBytes / file.size) * 100)),
        );
      });
      await runPool(tasks, MAX_CONCURRENT_PARTS);
    }
    if (uploaded.size !== status.partCount)
      throw new Error("上传分片不完整，无法提交归档。");
    updateStage("finalizing");
    const parts = Array.from(uploaded, ([partNumber, eTag]) => ({
      partNumber,
      eTag,
    })).sort((left, right) => left.partNumber - right.partNumber);
    const completing = await completeMultipart({
      sessionId: record.sessionId,
      parts,
    }).unwrap();
    const completed = await waitForCompletion(
      record.sessionId,
      completing,
      signal,
    );
    updateStage("confirming");
    const resource = await confirmResource(completed.resourceId).unwrap();
    clearResumeRecord();
    sessionRef.current = null;
    setResumeRecord(null);
    updateProgress(100);
    return resource;
  };

  const upload = async (file, capability) => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    updateProgress(0);
    try {
      let resource;
      if (file.size < capability.multipartThresholdBytes) {
        updateStage("initializing");
        const presign = await presignSimple(file).unwrap();
        updateStage("uploading");
        await putObject({
          url: presign.presignedUrl,
          body: file,
          contentType: file.type,
          signal: controller.signal,
          onProgress: updateProgress,
        });
        updateStage("confirming");
        resource = await confirmResource(presign.resourceId).unwrap();
      } else {
        resource = await uploadMultipart(file, capability, controller.signal);
      }
      updateStage("completed");
      return resource;
    } catch (error) {
      if (error?.name === "AbortError" || error?.kind === "aborted")
        updateStage("cancelled");
      else updateStage("failed");
      throw error;
    }
  };

  const cancel = async ({ abortSession = false } = {}) => {
    controllerRef.current?.abort();
    const record = sessionRef.current;
    if (abortSession && record) {
      updateStage("aborting");
      await abortMultipart({ sessionId: record.sessionId }).unwrap();
      clearResumeRecord();
      sessionRef.current = null;
      setResumeRecord(null);
    }
    updateStage("cancelled");
  };

  return { upload, cancel, progress, stage, resumeRecord };
}
