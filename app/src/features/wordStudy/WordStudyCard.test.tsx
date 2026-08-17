import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordStudyCard from "@/features/wordStudy/WordStudyCard";
import type {
  ExampleSentence,
  WordStudySessionItem,
} from "@/features/wordStudy/wordStudyTypes";

const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";

vi.mock("@/features/audio/AudioPlaybackButton", () => ({
  default: ({
    audioResourceId,
    label,
    variant,
  }: {
    audioResourceId: string;
    label?: string;
    variant?: string;
  }) => (
    <button
      type="button"
      aria-label={`播放${label}`}
      data-audio-resource-id={audioResourceId}
      data-variant={variant}
    />
  ),
}));

function createItem(
  status: WordStudySessionItem["status"],
  contentAvailable = true,
  itemId = "item-1",
  headword = "hello",
  audioResourceId: string | null = null,
  examples: ExampleSentence[] = [],
): WordStudySessionItem {
  return {
    itemId,
    wordId: `word-${itemId}`,
    position: 1,
    status,
    contentAvailable,
    content: contentAvailable
      ? {
          headword,
          senses: [
            {
              partOfSpeech: "Interjection",
              definition: `释义 ${headword}`,
              usageNote: `用法 ${headword}`,
              sortOrder: 0,
              examples,
            },
          ],
          audioResourceId,
        }
      : null,
  };
}

const defaultProps = {
  total: 3,
  submitting: false,
  hasPrevious: false,
  hasNext: false,
  onPrevious: vi.fn(),
  onNext: vi.fn(),
  onResult: vi.fn(),
};

describe("WordStudyCard", () => {
  it("supports result actions and ordered navigation for a pending item", async () => {
    const user = userEvent.setup();
    const onPrevious = vi.fn();
    const onNext = vi.fn();
    const onResult = vi.fn();
    render(
      <WordStudyCard
        item={createItem("Pending")}
        total={3}
        submitting={false}
        hasPrevious
        hasNext
        onPrevious={onPrevious}
        onNext={onNext}
        onResult={onResult}
      />,
    );

    expect(screen.getByRole("heading", { name: "hello" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "上一词" }));
    await user.click(screen.getByRole("button", { name: "下一词" }));
    await user.click(screen.getByRole("button", { name: "记住了" }));

    expect(onPrevious).toHaveBeenCalledOnce();
    expect(onNext).toHaveBeenCalledOnce();
    expect(onResult).toHaveBeenCalledWith("Remembered");
  });

  it("renders answered and unavailable items without result actions", () => {
    const props = {
      total: 3,
      submitting: false,
      hasPrevious: false,
      hasNext: false,
      onPrevious: vi.fn(),
      onNext: vi.fn(),
      onResult: vi.fn(),
    };
    const { rerender } = render(
      <WordStudyCard item={createItem("Remembered")} {...props} />,
    );

    expect(screen.getByRole("status")).toHaveTextContent("结果：已记住");
    expect(
      screen.queryByRole("button", { name: "记住了" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "没记住" }),
    ).not.toBeInTheDocument();

    rerender(<WordStudyCard item={createItem("Skipped")} {...props} />);

    expect(screen.getByRole("status")).toHaveTextContent("结果：已跳过");

    rerender(<WordStudyCard item={createItem("Pending", false)} {...props} />);

    expect(screen.getByText("该单词当前不可查看")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "记住了" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "显示释义和例句" }),
    ).not.toBeInTheDocument();
  });

  it("keeps definitions collapsed until the user reveals them", async () => {
    const user = userEvent.setup();
    render(
      <WordStudyCard
        item={createItem("Pending", true, "item-1", "hello", null, [
          {
            sentence: "Example hello",
            translation: "例句 hello",
            sortOrder: 0,
          },
        ])}
        {...defaultProps}
      />,
    );

    const toggle = screen.getByRole("button", { name: "显示释义和例句" });
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("释义 hello")).not.toBeInTheDocument();
    expect(screen.queryByText("Example hello")).not.toBeInTheDocument();

    await user.click(toggle);
    expect(
      screen.getByRole("button", { name: "隐藏释义和例句" }),
    ).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("释义 hello")).toBeInTheDocument();
    expect(screen.getByText("用法 hello")).toBeInTheDocument();
    expect(screen.getByText("Example hello")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "隐藏释义和例句" }));
    expect(screen.queryByText("释义 hello")).not.toBeInTheDocument();
  });

  it("shows one shared word audio button only when audio is associated", () => {
    const { rerender } = render(
      <WordStudyCard
        item={createItem("Pending", true, "item-1", "hello", AUDIO_ID)}
        {...defaultProps}
      />,
    );

    const playback = screen.getByRole("button", { name: "播放单词发音" });
    expect(playback).toHaveAttribute("data-audio-resource-id", AUDIO_ID);
    expect(playback).toHaveAttribute("data-variant", "icon");
    expect(
      screen.getAllByRole("button", { name: "播放单词发音" }),
    ).toHaveLength(1);

    rerender(
      <WordStudyCard
        item={createItem("Pending", true, "item-2", "world", null)}
        {...defaultProps}
      />,
    );
    expect(
      screen.queryByRole("button", { name: "播放单词发音" }),
    ).not.toBeInTheDocument();
  });

  it("renders examples as text only and expands a sense with no examples", async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <WordStudyCard
        item={createItem("Pending", true, "item-1", "hello", null, [
          {
            sentence: "Example hello",
            translation: "例句 hello",
            sortOrder: 0,
          },
        ])}
        {...defaultProps}
      />,
    );

    await user.click(screen.getByRole("button", { name: "显示释义和例句" }));
    expect(screen.getByText("Example hello")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /例句音频/ }),
    ).not.toBeInTheDocument();

    rerender(
      <WordStudyCard
        item={createItem("Pending", true, "item-2", "world", null, [])}
        {...defaultProps}
      />,
    );
    await user.click(screen.getByRole("button", { name: "显示释义和例句" }));
    expect(screen.getByText("释义 world")).toBeInTheDocument();
    expect(screen.queryByRole("blockquote")).not.toBeInTheDocument();
  });

  it("collapses content whenever the selected item changes", async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <WordStudyCard item={createItem("Pending")} {...defaultProps} />,
    );
    await user.click(screen.getByRole("button", { name: "显示释义和例句" }));

    rerender(
      <WordStudyCard
        item={createItem("Pending", true, "item-2", "world")}
        {...defaultProps}
      />,
    );

    expect(
      screen.getByRole("button", { name: "显示释义和例句" }),
    ).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("释义 world")).not.toBeInTheDocument();
  });
});
