import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import WordStudyWorkspace from "./WordStudyWorkspace";
import type { WordStudySessionState } from "./wordStudyTypes";

const ITEM_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

function memorizationSession(): WordStudySessionState {
  return {
    id: "session",
    sessionType: "Review",
    phase: "Memorization",
    status: "Active",
    actualCount: 1,
    completedCount: 0,
    memorizationPassedCount: 0,
    spellingPassedCount: 0,
    excludedCount: 0,
    skippedCount: 0,
    startedAt: "2026-08-18T00:00:00Z",
    completedAt: null,
    currentItem: {
      phase: "Memorization",
      itemId: ITEM_ID,
      wordId: "word",
      itemConcurrencyStamp: "stamp",
      isFavorite: false,
      memorization: {
        headword: "école",
        senses: [
          {
            partOfSpeech: "Noun",
            definition: "学校",
            usageNote: null,
            sortOrder: 0,
            examples: [],
          },
        ],
        audioResourceId: null,
      },
      spelling: null,
    },
  };
}

describe("WordStudyWorkspace", () => {
  it("offers the next group after a completed session", async () => {
    const user = userEvent.setup();
    const continueStudy = vi.fn().mockResolvedValue(undefined);
    render(
      <MemoryRouter>
        <WordStudyWorkspace
          mode="learning"
          session={{
            ...memorizationSession(),
            sessionType: "Learning",
            status: "Completed",
            currentItem: null,
            completedCount: 1,
          }}
          submitting={false}
          onMemorization={vi.fn()}
          onSpelling={vi.fn()}
          onToggleFavorite={vi.fn()}
          onContinue={continueStudy}
        />
      </MemoryRouter>,
    );

    await user.click(screen.getByRole("button", { name: "再学一组" }));
    expect(continueStudy).toHaveBeenCalledOnce();
  });

  it("reports a favorite failure without changing the current item", async () => {
    const user = userEvent.setup();
    render(
      <WordStudyWorkspace
        mode="learning"
        session={{ ...memorizationSession(), sessionType: "Learning" }}
        submitting={false}
        onMemorization={vi.fn()}
        onSpelling={vi.fn()}
        onToggleFavorite={vi.fn().mockRejectedValue(new Error("failed"))}
      />,
    );

    await user.click(screen.getByRole("button", { name: "收藏" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "收藏状态更新失败",
    );
    expect(screen.getByRole("heading", { name: "école" })).toBeInTheDocument();
  });

  it("requires confirmation before excluding a review item", async () => {
    const user = userEvent.setup();
    const exclude = vi.fn().mockResolvedValue(undefined);
    render(
      <WordStudyWorkspace
        mode="review"
        session={memorizationSession()}
        submitting={false}
        onMemorization={vi.fn()}
        onSpelling={vi.fn()}
        onToggleFavorite={vi.fn()}
        onExclude={exclude}
      />,
    );

    await user.click(screen.getByRole("button", { name: "不再复习此词" }));
    expect(
      screen.getByRole("heading", { name: "停止复习这个单词？" }),
    ).toBeInTheDocument();
    expect(exclude).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "确认停止复习" }));
    expect(exclude).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: ITEM_ID,
        itemConcurrencyStamp: "stamp",
      }),
    );
  });

  it("never exposes the answer in the spelling phase", async () => {
    const user = userEvent.setup();
    const session: WordStudySessionState = {
      ...memorizationSession(),
      phase: "Spelling",
      currentItem: {
        phase: "Spelling",
        itemId: ITEM_ID,
        wordId: "word",
        itemConcurrencyStamp: "stamp",
        isFavorite: false,
        memorization: null,
        spelling: {
          senses: [
            {
              partOfSpeech: "Noun",
              definition: "学校",
            },
          ],
        },
      },
    };
    const spell = vi.fn().mockResolvedValue("Incorrect");
    render(
      <WordStudyWorkspace
        mode="review"
        session={session}
        submitting={false}
        onMemorization={vi.fn()}
        onSpelling={spell}
        onToggleFavorite={vi.fn()}
      />,
    );

    expect(screen.queryByText("école")).not.toBeInTheDocument();
    await user.type(screen.getByRole("textbox", { name: "拼写单词" }), "ecole");
    await user.click(screen.getByRole("button", { name: "提交拼写" }));
    expect(
      await screen.findByText("拼写不正确，稍后再试一次"),
    ).toBeInTheDocument();
  });

  it("clears spelling input and feedback when the queue advances", async () => {
    const user = userEvent.setup();
    const first = {
      ...memorizationSession(),
      phase: "Spelling" as const,
      currentItem: {
        phase: "Spelling" as const,
        itemId: "item-1",
        wordId: "word-1",
        itemConcurrencyStamp: "stamp-1",
        isFavorite: false,
        memorization: null,
        spelling: { senses: [] },
      },
    };
    const props = {
      mode: "learning" as const,
      submitting: false,
      onMemorization: vi.fn(),
      onSpelling: vi.fn().mockResolvedValue("Incorrect"),
      onToggleFavorite: vi.fn(),
    };
    const view = render(<WordStudyWorkspace {...props} session={first} />);
    await user.type(screen.getByRole("textbox", { name: "拼写单词" }), "wrong");
    await user.click(screen.getByRole("button", { name: "提交拼写" }));
    expect(
      await screen.findByText("拼写不正确，稍后再试一次"),
    ).toBeInTheDocument();

    view.rerender(
      <WordStudyWorkspace
        {...props}
        session={{
          ...first,
          currentItem: {
            ...first.currentItem,
            itemId: "item-2",
            wordId: "word-2",
            itemConcurrencyStamp: "stamp-2",
          },
        }}
      />,
    );

    expect(screen.getByRole("textbox", { name: "拼写单词" })).toHaveValue("");
    expect(
      screen.queryByText("拼写不正确，稍后再试一次"),
    ).not.toBeInTheDocument();
  });
});
