import { act, renderHook } from "@testing-library/react";

import { beforeEach, describe, expect, it, vi } from "vitest";
import { usePaperAttemptAnswers } from "@/features/papers/usePaperAttemptAnswers";
import type { PaperAttemptQuestion } from "@/features/papers/paperTypes";

const SINGLE_ID = "11111111-2222-3333-4444-555555555555";
const FILL_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

const questions: PaperAttemptQuestion[] = [
  {
    id: SINGLE_ID,
    type: "SingleChoice",
    prompt: "Choose",
    points: 1,
    sortOrder: 1,
    options: [],
    savedAnswer: null,
  },
  {
    id: FILL_ID,
    type: "FillBlank",
    prompt: "Fill",
    points: 1,
    sortOrder: 2,
    options: [],
    savedAnswer: {
      selectedOptionId: null,
      booleanAnswer: null,
      textAnswer: "old",
      savedAt: null,
    },
  },
];

describe("usePaperAttemptAnswers", () => {
  beforeEach(() => vi.useRealTimers());

  it("updates controlled answers after the StrictMode remount check", () => {
    const { result } = renderHook(
      () =>
        usePaperAttemptAnswers({
          attemptId: "attempt",
          questions,
          saveAnswer: vi.fn().mockResolvedValue(undefined),
          clearAnswer: vi.fn().mockResolvedValue(undefined),
        }),
      { reactStrictMode: true },
    );

    act(() => result.current.changeAnswer(SINGLE_ID, "option"));
    expect(result.current.answers[SINGLE_ID]?.value).toBe("option");

    act(() => result.current.changeAnswer(FILL_ID, "typed text"));
    expect(result.current.answers[FILL_ID]?.value).toBe("typed text");
  });

  it("saves choice answers immediately and fill answers after debounce", async () => {
    vi.useFakeTimers();
    const save = vi.fn().mockResolvedValue(undefined);
    const clear = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() =>
      usePaperAttemptAnswers({
        attemptId: "attempt",
        questions,
        saveAnswer: save,
        clearAnswer: clear,
        debounceMs: 400,
      }),
    );
    act(() => result.current.changeAnswer(SINGLE_ID, "option"));
    expect(save).toHaveBeenCalledWith(SINGLE_ID, {
      selectedOptionId: "option",
    });
    act(() => result.current.changeAnswer(FILL_ID, "new text"));
    expect(save).toHaveBeenCalledTimes(1);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(400);
    });
    expect(save).toHaveBeenCalledWith(FILL_ID, { textAnswer: "new text" });
  });

  it("ignores an older request completion after a newer revision", async () => {
    const controls: Array<{ resolve: () => void }> = [];
    const save = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          controls.push({ resolve });
        }),
    );
    const { result } = renderHook(() =>
      usePaperAttemptAnswers({
        attemptId: "attempt",
        questions,
        saveAnswer: save,
        clearAnswer: vi.fn().mockResolvedValue(undefined),
      }),
    );
    act(() => result.current.changeAnswer(SINGLE_ID, "first"));
    act(() => result.current.changeAnswer(SINGLE_ID, "second"));
    expect(save).toHaveBeenCalledTimes(2);
    await act(async () => {
      controls[0]?.resolve();
    });
    expect(result.current.answers[SINGLE_ID]?.status).toBe("saving");
    await act(async () => {
      controls[1]?.resolve();
    });
    expect(result.current.answers[SINGLE_ID]?.status).toBe("saved");
    expect(result.current.answers[SINGLE_ID]?.savedValue).toBe("second");
  });

  it("retries failures, clears empty values, and flushes all dirty answers", async () => {
    const save = vi
      .fn()
      .mockRejectedValueOnce(new Error("failed"))
      .mockResolvedValue(undefined);
    const clear = vi.fn().mockResolvedValue(undefined);
    const { result } = renderHook(() =>
      usePaperAttemptAnswers({
        attemptId: "attempt",
        questions,
        saveAnswer: save,
        clearAnswer: clear,
      }),
    );
    await act(async () => {
      result.current.changeAnswer(SINGLE_ID, "option");
      await Promise.resolve();
    });
    expect(result.current.answers[SINGLE_ID]?.status).toBe("error");
    await act(async () => {
      await result.current.retryQuestion(SINGLE_ID);
    });
    expect(result.current.answers[SINGLE_ID]?.status).toBe("saved");
    act(() => result.current.changeAnswer(FILL_ID, "   "));
    await act(async () => {
      await result.current.flushQuestion(FILL_ID);
    });
    expect(clear).toHaveBeenCalledWith(FILL_ID);
    act(() => result.current.changeAnswer(SINGLE_ID, "next"));
    const flushed = await act(async () => result.current.flushAll());
    expect(flushed.ok).toBe(true);
  });
});
