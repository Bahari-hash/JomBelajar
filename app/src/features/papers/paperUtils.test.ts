import { describe, expect, it } from "vitest";
import {
  formatPaperDate,
  getPaperErrorMessage,
  getQuestionTypeLabel,
  isPaperSubmitRecoveryError,
  isPaperNotFoundError,
} from "@/features/papers/paperUtils";

describe("paperUtils", () => {
  it("formats valid dates and rejects invalid or absent dates", () => {
    expect(formatPaperDate("2026-08-10T00:00:00Z")).not.toBe("时间未知");
    expect(formatPaperDate("not-a-date")).toBe("时间未知");
    expect(formatPaperDate(null)).toBe("时间未知");
    expect(formatPaperDate(undefined)).toBe("时间未知");
  });

  it.each([
    ["SingleChoice", "单选题"],
    ["TrueFalse", "判断题"],
    ["FillBlank", "填空题"],
  ] as const)("maps %s to its display label", (type, label) => {
    expect(getQuestionTypeLabel(type)).toBe(label);
  });

  it("exposes only safe adapter messages", () => {
    expect(getPaperErrorMessage({ message: "可安全显示" })).toBe("可安全显示");
    expect(getPaperErrorMessage(new Error("raw"))).toBe(
      "在线测试加载失败，请稍后重试。",
    );
    expect(getPaperErrorMessage(new Error("raw"), "自定义安全文案")).toBe(
      "自定义安全文案",
    );
    expect(getPaperErrorMessage({ message: 42 })).toBe(
      "在线测试加载失败，请稍后重试。",
    );
    expect(getPaperErrorMessage(null)).toBe("在线测试加载失败，请稍后重试。");
  });

  it("recognizes only the paper-not-found adapter code", () => {
    expect(isPaperNotFoundError({ code: "PaperNotFound" })).toBe(true);
    expect(isPaperNotFoundError({ code: "Other" })).toBe(false);
    expect(isPaperNotFoundError(new Error("PaperNotFound"))).toBe(false);
    expect(isPaperNotFoundError(null)).toBe(false);
  });

  it("recognizes submit conflicts that may already have a result", () => {
    expect(
      isPaperSubmitRecoveryError({
        code: "PaperAttemptConcurrencyConflict",
      }),
    ).toBe(true);
    expect(
      isPaperSubmitRecoveryError({ code: "PaperAttemptNotInProgress" }),
    ).toBe(false);
    expect(isPaperSubmitRecoveryError(null)).toBe(false);
  });
});
