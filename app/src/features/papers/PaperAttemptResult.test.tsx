import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import PaperAttemptResult from "@/features/papers/PaperAttemptResult";

const result = {
  id: "attempt",
  paperId: "paper",
  attemptNumber: 2,
  paperTitle: "Grammar Check",
  score: 3,
  paperTotalScore: 4,
  paperPassingScore: 2,
  isPassed: true,
  startedAt: "2026-08-10T00:00:00Z",
  submittedAt: "2026-08-10T01:00:00Z",
  questions: [
    {
      questionId: "q1",
      type: "SingleChoice" as const,
      prompt: "Choose",
      explanation: "Pick the article.",
      points: 2,
      sortOrder: 1,
      options: [
        { id: "a", text: "A", sortOrder: 1 },
        { id: "b", text: "B", sortOrder: 2 },
      ],
      selectedOptionId: "a",
      booleanAnswer: null,
      textAnswer: null,
      isAnswered: true,
      correctOptionId: "a",
      correctBoolean: null,
      acceptedAnswers: [],
      isCorrect: true,
      awardedPoints: 2,
    },
    {
      questionId: "q2",
      type: "TrueFalse" as const,
      prompt: "True?",
      explanation: null,
      points: 2,
      sortOrder: 2,
      options: [],
      selectedOptionId: null,
      booleanAnswer: null,
      textAnswer: null,
      isAnswered: false,
      correctOptionId: null,
      correctBoolean: true,
      acceptedAnswers: [],
      isCorrect: false,
      awardedPoints: 0,
    },
  ],
};

describe("PaperAttemptResult", () => {
  it("shows score, pass state, answers, correct answers, and explanations", async () => {
    const restart = vi.fn();
    render(
      <MemoryRouter>
        <PaperAttemptResult
          result={result}
          restarting={false}
          restartError={null}
          onRestart={restart}
        />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("heading", { name: "测试结果" }),
    ).toBeInTheDocument();
    expect(screen.getByText("3 / 4")).toBeInTheDocument();
    expect(screen.getByText("已通过")).toBeInTheDocument();
    expect(
      screen.getAllByText("用户答案：")[0]!.parentElement,
    ).toHaveTextContent("用户答案：A");
    expect(
      screen.getAllByText("正确答案：")[0]!.parentElement,
    ).toHaveTextContent("正确答案：A");
    expect(screen.getByText("未作答")).toBeInTheDocument();
    expect(screen.getByText("Pick the article.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "再做一次" }));
    expect(restart).toHaveBeenCalled();
  });
});
