import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { WordFavorite } from "./wordStudyTypes";
import WordFavoriteDetails from "./WordFavoriteDetails";

vi.mock("@/features/audio/AudioPlaybackButton", () => ({
  default: ({
    audioResourceId,
    label,
  }: {
    audioResourceId: string;
    label?: string;
  }) => (
    <button type="button" aria-label={label} data-audio-id={audioResourceId}>
      {label}
    </button>
  ),
}));

const word: WordFavorite = {
  wordId: "word-1",
  headword: "study",
  audioResourceId: "word-audio",
  createdAt: "2026-08-18T00:00:00Z",
  senses: [
    {
      partOfSpeech: "n.",
      definition: "书房",
      usageNote: null,
      sortOrder: 2,
      examples: [
        {
          sentence: "The study is quiet.",
          translation: "书房很安静。",
          sortOrder: 2,
          audioResourceId: null,
        },
      ],
    },
    {
      partOfSpeech: "v.",
      definition: "学习",
      usageNote: "用于描述获取知识。",
      sortOrder: 1,
      examples: [
        {
          sentence: "I study English.",
          translation: "我学习英语。",
          sortOrder: 2,
          audioResourceId: "example-two",
        },
        {
          sentence: "She studies every day.",
          translation: "她每天学习。",
          sortOrder: 1,
          audioResourceId: "example-one",
        },
      ],
    },
  ],
};

describe("WordFavoriteDetails", () => {
  it("renders ordered word details, available audio, and actions", async () => {
    const onClose = vi.fn();
    const onRemove = vi.fn();
    const user = userEvent.setup();

    render(
      <WordFavoriteDetails
        word={word}
        onClose={onClose}
        onRemove={onRemove}
      />,
    );

    expect(screen.getByRole("dialog", { name: "study" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "朗读 study" })).toHaveAttribute(
      "data-audio-id",
      "word-audio",
    );
    expect(screen.getByText("v.")).toBeInTheDocument();
    expect(screen.getByText("学习")).toBeInTheDocument();
    expect(screen.getByText("用于描述获取知识。")).toBeInTheDocument();
    expect(screen.getByText("n.")).toBeInTheDocument();
    expect(screen.getByText("书房")).toBeInTheDocument();
    expect(screen.getByText("I study English.")).toBeInTheDocument();
    expect(screen.getByText("我学习英语。")).toBeInTheDocument();
    expect(screen.getByText("She studies every day.")).toBeInTheDocument();
    expect(screen.getByText("她每天学习。")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "朗读例句 She studies every day." }),
    ).toHaveAttribute("data-audio-id", "example-one");
    expect(
      screen.getByRole("button", { name: "朗读例句 I study English." }),
    ).toHaveAttribute("data-audio-id", "example-two");
    expect(screen.queryByRole("button", { name: /朗读例句 The study/ })).not.toBeInTheDocument();

    const senseHeadings = screen.getAllByRole("heading", { level: 3 });
    expect(senseHeadings[0]).toHaveTextContent("v.");
    expect(senseHeadings[1]).toHaveTextContent("n.");
    expect(screen.getByText("学习")).toBeInTheDocument();
    expect(screen.getByText("书房")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "关闭收藏详情" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onRemove).toHaveBeenCalledTimes(1);
  });
});
