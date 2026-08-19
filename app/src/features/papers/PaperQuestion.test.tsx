import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import PaperQuestion from "@/features/papers/PaperQuestion";
import type {
  LocalPaperAnswer,
  PaperAttemptQuestion,
} from "@/features/papers/paperTypes";

vi.mock("@/features/audio/AudioPlaybackButton", () => ({
  default: ({ audioResourceId }: { audioResourceId: string }) => (
    <button type="button">播放 {audioResourceId}</button>
  ),
}));

const answer: LocalPaperAnswer = {
  value: null,
  savedValue: null,
  status: "idle",
  revision: 0,
  errorMessage: null,
};

describe("PaperQuestion", () => {
  it("maps choice, boolean, and fill inputs to controlled values", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const base = {
      id: "question",
      prompt: "Prompt",
      points: 2,
      sortOrder: 1,
      savedAnswer: null,
    };
    const choice: PaperAttemptQuestion = {
      ...base,
      type: "SingleChoice",
      options: [{ id: "option", text: "Choice A", sortOrder: 1 }],
    };
    const { rerender } = render(
      <PaperQuestion
        question={choice}
        index={0}
        answer={answer}
        disabled={false}
        onChange={onChange}
        onBlur={vi.fn()}
        onRetry={vi.fn()}
      />,
    );
    await user.click(screen.getByRole("radio", { name: "Choice A" }));
    expect(onChange).toHaveBeenLastCalledWith("option");
    const booleanQuestion: PaperAttemptQuestion = {
      ...base,
      type: "TrueFalse",
      options: [],
    };
    rerender(
      <PaperQuestion
        question={booleanQuestion}
        index={0}
        answer={answer}
        disabled={false}
        onChange={onChange}
        onBlur={vi.fn()}
        onRetry={vi.fn()}
      />,
    );
    await user.click(screen.getByRole("radio", { name: "正确" }));
    expect(onChange).toHaveBeenLastCalledWith(true);
    const fill: PaperAttemptQuestion = {
      ...base,
      type: "FillBlank",
      options: [],
    };
    rerender(
      <PaperQuestion
        question={fill}
        index={0}
        answer={{ ...answer, value: "text" }}
        disabled={false}
        onChange={onChange}
        onBlur={vi.fn()}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.getByRole("textbox", { name: "填空答案" })).toHaveValue(
      "text",
    );
  });

  it("shows save failure and retry", async () => {
    const retry = vi.fn();
    const question: PaperAttemptQuestion = {
      id: "q",
      type: "FillBlank",
      prompt: "Prompt",
      points: 1,
      sortOrder: 1,
      options: [],
      savedAnswer: null,
    };
    render(
      <PaperQuestion
        question={question}
        index={0}
        answer={{ ...answer, status: "error", errorMessage: "failed" }}
        disabled={false}
        onChange={vi.fn()}
        onBlur={vi.fn()}
        onRetry={retry}
      />,
    );
    await userEvent.click(screen.getByRole("button", { name: "重试保存" }));
    expect(retry).toHaveBeenCalled();
  });

  it("edits ordered dictation blanks and exposes audio playback", async () => {
    const onChange = vi.fn();
    render(
      <PaperQuestion
        question={{
          id: "dictation",
          type: "Dictation",
          prompt: "Listen",
          points: 2,
          sortOrder: 1,
          options: [],
          savedAnswer: null,
          audioResourceId: "audio-id",
          dictationBlanks: [{ sortOrder: 0 }, { sortOrder: 1 }],
        }}
        index={0}
        answer={{ ...answer, value: ["hello", ""] }}
        disabled={false}
        onChange={onChange}
        onBlur={vi.fn()}
        onRetry={vi.fn()}
      />,
    );

    expect(screen.getByRole("button", { name: "播放 audio-id" })).toBeVisible();
    fireEvent.change(screen.getByRole("textbox", { name: "第 2 空" }), {
      target: { value: "world" },
    });
    expect(onChange).toHaveBeenLastCalledWith(["hello", "world"]);
  });
});
