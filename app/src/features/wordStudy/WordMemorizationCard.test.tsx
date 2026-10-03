import { render, screen } from "@testing-library/react";
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
  it("shows all revealed senses and examples without disclosure controls", async () => {
    render(
      <WordMemorizationCard revealed
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
    expect(document.querySelector("details, summary")).toBeNull();
    expect(screen.getByText("学习")).toBeVisible();
    expect(screen.getByText("用于描述获取知识。")).toBeVisible();

    expect(screen.getByText("I study English.")).toBeVisible();
    expect(screen.getByText("我学习英语。")).toBeVisible();
    expect(screen.getByRole("button", { name: "播放例句音频" })).toBeVisible();
  });

  it("does not render an examples entry when a sense has no examples", () => {
    render(<WordMemorizationCard revealed item={item([])} />);

    expect(screen.queryByText(/^例句（/)).not.toBeInTheDocument();
  });
});
