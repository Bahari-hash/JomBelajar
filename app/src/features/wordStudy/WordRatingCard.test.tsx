import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordRatingCard from "./WordRatingCard";
import type { WordStudyCurrentItem } from "./wordStudyTypes";

const item: Extract<WordStudyCurrentItem, { phase: "Memorization" }> = {
  phase: "Memorization", itemId: "one", wordId: "buku", itemConcurrencyStamp: "v1",
  isFavorite: false, spelling: null,
  memorization: { headword: "buku", audioResourceId: null,
    senses: [{ partOfSpeech: "n.", definition: "书", usageNote: null, sortOrder: 1, examples: [] }],
    ratingPreviews: ["Again", "Hard", "Good", "Easy"].map((rating, index) => ({
      rating: rating as "Again" | "Hard" | "Good" | "Easy", dueAt: "2026-10-03T00:00:00Z",
      intervalSeconds: [60, 330, 600, 1382400][index],
    })),
  },
};

describe("WordRatingCard", () => {
  it("hides the answer until space, displays server intervals and submits the selected rating", async () => {
    const user = userEvent.setup(); const rate = vi.fn().mockResolvedValue(undefined);
    render(<WordRatingCard item={item} submitting={false} onRate={rate} />);
    expect(screen.getByText("buku")).toBeVisible(); expect(screen.queryByText("书")).not.toBeInTheDocument();
    await user.keyboard("3"); expect(rate).not.toHaveBeenCalled();
    await user.keyboard(" "); expect(screen.getByText("书")).toBeVisible();
    expect(screen.queryByText("本组稍后再练")).not.toBeInTheDocument();
    for (const interval of ["1 分钟", "5.5 分钟", "10 分钟", "16 天"]) expect(screen.getByText(interval)).toBeVisible();
    await user.keyboard("3"); expect(rate).toHaveBeenCalledWith("Good");
  });
  it("blocks double submissions, permits retry after failure and resets when revision changes", async () => {
    const user = userEvent.setup(); let reject!: (reason: Error) => void;
    const rate = vi.fn().mockImplementationOnce(() => new Promise((_, r) => { reject = r; })).mockResolvedValue(undefined);
    const view = render(<WordRatingCard key="v1" item={item} submitting={false} onRate={rate} />);
    await user.click(screen.getByRole("button", { name: "显示答案" }));
    await user.keyboard("11"); expect(rate).toHaveBeenCalledTimes(1);
    reject(new Error("offline")); await waitFor(() => expect(screen.getByRole("button", { name: /重来/ })).toBeEnabled());
    await user.keyboard("1"); expect(rate).toHaveBeenCalledTimes(2);
    view.rerender(<WordRatingCard key="v2" item={{ ...item, itemConcurrencyStamp: "v2" }} submitting={false} onRate={rate} />);
    expect(screen.queryByText("书")).not.toBeInTheDocument();
  });
  it("does not rate from a dialog, while externally disabled, or without interval previews", async () => {
    const user = userEvent.setup(); const rate = vi.fn();
    const view = render(<><WordRatingCard item={item} submitting={false} onRate={rate} /><div role="dialog"><input aria-label="dialog input" /></div></>);
    await user.click(screen.getByRole("button", { name: "显示答案" }));
    fireEvent.keyDown(screen.getByLabelText("dialog input"), { key: "1" }); expect(rate).not.toHaveBeenCalled();
    view.rerender(<WordRatingCard item={item} submitting onRate={rate} />);
    await user.keyboard("1"); expect(rate).not.toHaveBeenCalled();
    view.rerender(<WordRatingCard item={{ ...item, memorization: { ...item.memorization, ratingPreviews: [] } }} submitting={false} onRate={rate} />);
    await user.keyboard("1"); expect(rate).not.toHaveBeenCalled();
  });
});
