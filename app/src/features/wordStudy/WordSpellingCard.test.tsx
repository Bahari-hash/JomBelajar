import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordSpellingCard from "./WordSpellingCard";
import type { WordStudyCurrentItem } from "./wordStudyTypes";

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
  it("clears answer and feedback when the item changes", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue("Incorrect");
    const { rerender } = render(
      <WordSpellingCard
        item={item("one")}
        submitting={false}
        onSubmit={onSubmit}
      />,
    );

    const input = screen.getByRole("textbox", { name: "拼写单词" });
    await user.type(input, "wrong");
    await user.click(screen.getByRole("button", { name: "提交拼写" }));
    expect(await screen.findByRole("status")).toHaveTextContent("拼写不正确");

    rerender(
      <WordSpellingCard
        item={item("two")}
        submitting={false}
        onSubmit={onSubmit}
      />,
    );
    expect(screen.getByRole("textbox", { name: "拼写单词" })).toHaveValue("");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
