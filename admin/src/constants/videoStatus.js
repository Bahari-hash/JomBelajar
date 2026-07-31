export const PROCESSING_STATUS_OPTIONS = Object.freeze([
  { value: "Queued", label: "排队中" },
  { value: "Processing", label: "处理中" },
  { value: "Ready", label: "已就绪" },
  { value: "Failed", label: "处理失败" },
]);

export const PUBLICATION_STATUS_OPTIONS = Object.freeze([
  { value: "Draft", label: "草稿" },
  { value: "Published", label: "已发布" },
  { value: "Unpublished", label: "已下架" },
  { value: "Archived", label: "已归档" },
]);

const PROCESSING_LABELS = new Map(
  PROCESSING_STATUS_OPTIONS.map((item) => [item.value, item.label]),
);
const PUBLICATION_LABELS = new Map(
  PUBLICATION_STATUS_OPTIONS.map((item) => [item.value, item.label]),
);

export function getProcessingStatusLabel(value) {
  return PROCESSING_LABELS.get(value) ?? "未知处理状态";
}

export function getPublicationStatusLabel(value) {
  return PUBLICATION_LABELS.get(value) ?? "未知发布状态";
}

export function getVideoFailureMessage(code) {
  const messages = {
    VideoProbeFailed: "无法读取视频媒体信息。",
    VideoTranscodeFailed: "视频转码失败。",
    VideoSourceDownloadFailed: "源视频下载失败。",
    VideoOutputUploadFailed: "处理结果上传失败。",
    VideoUnsupportedMedia: "视频格式或编码不受支持。",
    VideoProcessingTimedOut: "视频处理超时。",
  };
  return messages[code] ?? "视频处理失败，可查看最新任务并重试。";
}

export function getVideoActions(video) {
  const activeJob = ["Queued", "Processing"].includes(video.latestJob?.status);
  const actions = [];
  if (
    video.processingStatus === "Ready" &&
    video.publicationStatus !== "Published" &&
    video.publicationStatus !== "Archived"
  )
    actions.push("publish");
  if (video.publicationStatus === "Published") actions.push("unpublish");
  if (
    video.processingStatus === "Failed" &&
    video.publicationStatus !== "Archived"
  )
    actions.push("retry");
  if (
    video.publicationStatus !== "Published" &&
    !activeJob &&
    video.publicationStatus !== "Archived"
  )
    actions.push("archive");
  return actions;
}
