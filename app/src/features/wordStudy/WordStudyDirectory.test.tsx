import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordStudyDirectory from "@/features/wordStudy/WordStudyDirectory";
import type {
  WordStudySessionItem,
  WordStudySessionItemStatus,
} from "@/features/wordStudy/wordStudyTypes";

function createItem(
  itemId: string,
  status: WordStudySessionItemStatus,
  contentAvailable = true,
): WordStudySessionItem {
  return {
    itemId,
    wordId: `word-${itemId}`,
    position: Number(itemId.replace(/\D/g, "")) - 1,
    status,
    contentAvailable,
    content: contentAvailable
      ? { headword: itemId, senses: [], audioResourceId: null }
      : null,
  };
}

describe("WordStudyDirectory", () => {
  it("shows item statuses and selects an available word", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(
      <WordStudyDirectory
        items={[
          createItem("first1", "Remembered"),
          createItem("second2", "Pending"),
          createItem("hidden3", "Skipped", false),
        ]}
        selectedItemId="first1"
        onSelect={onSelect}
      />,
    );

    expect(screen.getAllByText("已记住").length).toBeGreaterThan(0);
    expect(screen.getAllByText("待背诵").length).toBeGreaterThan(0);
    expect(screen.getAllByText("内容不可用").length).toBeGreaterThan(0);

    await user.click(
      screen.getAllByRole("button", { name: /second2.*待背诵/ })[0],
    );

    expect(onSelect).toHaveBeenCalledWith("second2");
    expect(
      screen.getAllByRole("button", { name: /内容不可用/ })[0],
    ).toBeDisabled();
  });
});
