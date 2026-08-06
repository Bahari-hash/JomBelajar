import type { ApiQueryError } from "@/services/axiosBaseQuery";

/** Accepts only absolute HTTP(S) URLs for article-owned remote resources. */
export function isSafeHttpUrl(value: string | null | undefined) {
  if (!value) {
    return false;
  }
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

/** Formats backend DateTimeOffset values in the reader's local timezone. */
export function formatArticleDate(value: string | null | undefined) {
  if (!value) {
    return "时间未知";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "时间未知";
  }
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(date);
}

/** Reads only the safe message exposed by the Axios RTK Query adapter. */
export function getArticleErrorMessage(
  error: unknown,
  fallback = "文章加载失败，请稍后重试。",
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

export function isArticleNotFoundError(error: unknown) {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    (error as ApiQueryError).code === "ArticleNotFound"
  );
}
