import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordSpellingCard from "./WordSpellingCard";
import type {
  WordStudyCommandResponse,
  WordStudyCurrentItem,
  WordStudySessionState,
} from "./wordStudyTypes";

const item = (
  itemId: string,
): Extract<WordStudyCurrentItem, { phase: "Spelling" }> => ({
  phase: "Spelling",
  itemId,
  wordId: `word-${itemId}`,
  itemConcurrencyStamp: `stamp-${itemId}`,
  isFavorite: false,
  memorization: null,
  spelling: { senses: [{ partOfSpeech: "n.", definition: "definition" }] },
});

describe("WordSpellingCard", () => {
  it("waits for next-word confirmation and resets even when the queue repeats the item", async () => {
    const user = userEvent.setup();
    const repeatedSession: WordStudySessionState = {
      id: "session",
      sessionType: "Review",
      phase: "Spelling",
      status: "Active",
      actualCount: 1,
      completedCount: 0,
      memorizationPassedCount: 0,
      spellingPassedCount: 0,
      excludedCount: 0,
      skippedCount: 0,
      startedAt: "2026-08-18T00:00:00Z",
      completedAt: null,
      currentItem: item("one"),
    };
    const onAdvance = vi.fn();
    const onSubmit = vi.fn().mockResolvedValue({
      spellingOutcome: { result: "Incorrect", correctAnswer: "école" },
      session: repeatedSession,
    } satisfies WordStudyCommandResponse);
    render(
      <WordSpellingCard
        item={item("one")}
        submitting={false}
        onSubmit={onSubmit}
        onAdvance={onAdvance}
      />,
    );

    const input = screen.getByRole("textbox", { name: "拼写单词" });
    await user.type(input, "ecole");
    await user.keyboard("{Enter}");
    expect(await screen.findByText("拼写错误")).toBeInTheDocument();
    expect(screen.getByText("正确答案：école")).toBeInTheDocument();
    expect(input).toBeDisabled();
    expect(onAdvance).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "下一词" }));
    expect(onAdvance).toHaveBeenCalledWith(repeatedSession);
    expect(input).toHaveValue("");
    expect(screen.queryByText("拼写错误")).not.toBeInTheDocument();
  });

  it("clears answer and feedback when the item changes", async () => {
    const user = userEvent.setup();
    const response: WordStudyCommandResponse = {
      spellingOutcome: { result: "Incorrect", correctAnswer: "école" },
      session: {
        id: "session",
        sessionType: "Learning",
        phase: "Spelling",
        status: "Active",
        actualCount: 1,
        completedCount: 0,
        memorizationPassedCount: 0,
        spellingPassedCount: 0,
        excludedCount: 0,
        skippedCount: 0,
        startedAt: "2026-08-18T00:00:00Z",
        completedAt: null,
        currentItem: item("next"),
      } satisfies WordStudySessionState,
    };
    const onSubmit = vi.fn().mockResolvedValue(response);
    const { rerender } = render(
      <WordSpellingCard
        item={item("one")}
        submitting={false}
        onSubmit={onSubmit}
        onAdvance={vi.fn()}
      />,
    );

    const input = screen.getByRole("textbox", { name: "拼写单词" });
    await user.type(input, "wrong");
    await user.click(screen.getByRole("button", { name: "提交拼写" }));
    expect(await screen.findByRole("status")).toHaveTextContent("拼写错误");

    rerender(
      <WordSpellingCard
        item={item("two")}
        submitting={false}
        onSubmit={onSubmit}
        onAdvance={vi.fn()}
      />,
    );
    expect(screen.getByRole("textbox", { name: "拼写单词" })).toHaveValue("");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
