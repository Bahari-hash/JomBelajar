import type { PaperQuestionType } from "@/features/papers/paperTypes";

const PAPER_ERROR_FALLBACK = "在线测试加载失败，请稍后重试。";

function isPlainObject(value: unknown): value is Record<string, unknown> {
  if (typeof value !== "object" || value === null) return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

export function formatPaperDate(value: string | null | undefined) {
  if (!value) return "时间未知";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "时间未知";
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(date);
}

export function getQuestionTypeLabel(type: PaperQuestionType) {
  switch (type) {
    case "SingleChoice":
      return "单选题";
    case "TrueFalse":
      return "判断题";
    case "FillBlank":
      return "填空题";
  }
  const exhaustiveCheck: never = type;
  return exhaustiveCheck;
}

export function getPaperErrorMessage(
  error: unknown,
  fallback = PAPER_ERROR_FALLBACK,
) {
  if (isPlainObject(error) && typeof error.message === "string") {
    return error.message;
  }
  return fallback;
}

export function isPaperNotFoundError(error: unknown) {
  return isPlainObject(error) && error.code === "PaperNotFound";
}

export function isPaperSubmitRecoveryError(error: unknown) {
  return (
    isPlainObject(error) && error.code === "PaperAttemptConcurrencyConflict"
  );
}

export function formatLanguageTag(value: string | null | undefined) {
  const languageTag = value?.trim();
  if (!languageTag) return "语言未知";

  try {
    return (
      new Intl.DisplayNames(["zh-CN"], { type: "language" }).of(languageTag) ??
      languageTag
    );
  } catch {
    return languageTag;
  }
}
