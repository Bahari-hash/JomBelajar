import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import WordMemorizationCard from "./WordMemorizationCard";
import type {
  WordMemorizationContent,
  WordStudyCurrentItem,
} from "./wordStudyTypes";

const item = (
  examples: WordMemorizationContent["senses"][number]["examples"],
  audioResourceId: string | null = null,
): Extract<WordStudyCurrentItem, { phase: "Memorization" }> => ({
  phase: "Memorization",
  itemId: "item-1",
  wordId: "word-1",
  itemConcurrencyStamp: "stamp-1",
  isFavorite: false,
  spelling: null,
  memorization: {
    headword: "study",
    audioResourceId,
    senses: [
      {
        partOfSpeech: "v.",
        definition: "学习",
        usageNote: "用于描述获取知识。",
        sortOrder: 1,
        examples,
      },
    ],
  },
});

describe("WordMemorizationCard", () => {
  it("keeps senses and examples collapsed until their summaries are opened", async () => {
    const user = userEvent.setup();
    render(
      <WordMemorizationCard
        item={item(
          [
            {
              sentence: "I study English.",
              translation: "我学习英语。",
              sortOrder: 1,
              audioResourceId: "example-audio",
            },
          ],
          "word-audio",
        )}
      />,
    );

    expect(screen.getByRole("button", { name: "播放单词音频" })).toBeVisible();
    const senseDetails = screen.getByText("v. 释义").closest("details");
    expect(senseDetails).not.toBeNull();
    expect(senseDetails).not.toHaveAttribute("open");
    expect(screen.queryByText("学习")).not.toBeVisible();
    expect(screen.queryByText("例句（1）")).not.toBeVisible();

    await user.click(screen.getByText("v. 释义"));
    expect(senseDetails).toHaveAttribute("open");
    expect(screen.getByText("学习")).toBeVisible();
    expect(screen.getByText("用于描述获取知识。")).toBeVisible();

    const examplesDetails = screen.getByText("例句（1）").closest("details");
    expect(examplesDetails).not.toBeNull();
    expect(examplesDetails).not.toHaveAttribute("open");
    expect(screen.queryByText("I study English.")).not.toBeVisible();

    await user.click(screen.getByText("例句（1）"));
    expect(examplesDetails).toHaveAttribute("open");
    expect(screen.getByText("I study English.")).toBeVisible();
    expect(screen.getByText("我学习英语。")).toBeVisible();
    expect(screen.getByRole("button", { name: "播放例句音频" })).toBeVisible();
  });

  it("does not render an examples entry when a sense has no examples", async () => {
    const user = userEvent.setup();
    render(<WordMemorizationCard item={item([])} />);

    await user.click(screen.getByText("v. 释义"));
    expect(screen.queryByText(/^例句（/)).not.toBeInTheDocument();
  });
});
