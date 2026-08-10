import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import PaperAttemptWorkspace from "@/features/papers/PaperAttemptWorkspace";

const attempt = {
  id: "attempt",
  paperId: "paper",
  attemptNumber: 1,
  status: "InProgress" as const,
  title: "Test",
  description: null,
  instructions: null,
  languageTag: "en",
  questionCount: 2,
  paperTotalScore: 2,
  paperPassingScore: 1,
  startedAt: "2026-08-10T00:00:00Z",
  submittedAt: null,
  questions: [
    {
      id: "q1",
      type: "SingleChoice" as const,
      prompt: "One",
      points: 1,
      sortOrder: 1,
      options: [{ id: "a", text: "A", sortOrder: 1 }],
      savedAnswer: null,
    },
    {
      id: "q2",
      type: "FillBlank" as const,
      prompt: "Two",
      points: 1,
      sortOrder: 2,
      options: [],
      savedAnswer: null,
    },
  ],
};
const answers = {
  q1: {
    value: null,
    savedValue: null,
    status: "idle" as const,
    revision: 0,
    errorMessage: null,
  },
  q2: {
    value: null,
    savedValue: null,
    status: "idle" as const,
    revision: 0,
    errorMessage: null,
  },
};

describe("PaperAttemptWorkspace", () => {
  it("navigates questions and flushes before confirmed submit", async () => {
    const user = userEvent.setup();
    const flushAll = vi
      .fn()
      .mockResolvedValue({ ok: true, failedQuestionIds: [] });
    const submit = vi.fn().mockResolvedValue(undefined);
    render(
      <PaperAttemptWorkspace
        attempt={attempt}
        answers={answers}
        isFlushing={false}
        changeAnswer={vi.fn()}
        flushQuestion={vi.fn()}
        retryQuestion={vi.fn()}
        flushAll={flushAll}
        submitting={false}
        submitError={null}
        onSubmit={submit}
      />,
    );
    expect(screen.getByRole("heading", { name: /One/ })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "下一题" }));
    expect(screen.getByRole("heading", { name: /Two/ })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /提交试卷/ }));
    expect(
      screen.getByRole("dialog", { name: "确认提交试卷" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "确认提交" }));
    expect(flushAll).toHaveBeenCalled();
    expect(submit).toHaveBeenCalled();
  });
});
