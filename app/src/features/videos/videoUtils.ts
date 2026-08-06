import type { ApiQueryError } from "@/services/axiosBaseQuery";

/** Allows only absolute HTTP(S) media URLs from playback responses. */
export function isSafeVideoUrl(value: string | null | undefined) {
  if (!value) return false;
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

/** Formats a backend DateTimeOffset for the user's local display. */
export function formatVideoDate(value: string | null | undefined) {
  if (!value) return "时间未知";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "时间未知";
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(date);
}

/** Formats non-negative finite durations without asserting any course semantics. */
export function formatVideoDuration(value: number | null | undefined) {
  if (!Number.isFinite(value) || !value || value < 0) return "时长未知";
  const totalSeconds = Math.floor(value);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours)
    return `${hours}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}

/** Reads the safe error message returned by the shared Axios query adapter. */
export function getVideoErrorMessage(
  error: unknown,
  fallback = "视频加载失败，请稍后重试。",
) {
  if (
    typeof error === "object" &&
    error !== null &&
    "message" in error &&
    typeof error.message === "string"
  ) {
    return error.message;
  }
  return fallback;
}

export function isVideoNotFoundError(error: unknown) {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    (error as ApiQueryError).code === "VideoNotFound"
  );
}
