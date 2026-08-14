import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordStudyCard from "@/features/wordStudy/WordStudyCard";
import type { WordStudySessionItem } from "@/features/wordStudy/wordStudyTypes";

function createItem(
  status: WordStudySessionItem["status"],
  contentAvailable = true,
  itemId = "item-1",
  headword = "hello",
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
              examples: [
                {
                  sentence: `Example ${headword}`,
                  translation: `例句 ${headword}`,
                  audioClipId: null,
                  sortOrder: 0,
                },
              ],
            },
          ],
          pronunciations: [],
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
    render(<WordStudyCard item={createItem("Pending")} {...defaultProps} />);

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
