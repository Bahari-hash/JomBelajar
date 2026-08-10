import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import PaperQuestionDirectory from "@/features/papers/PaperQuestionDirectory";

describe("PaperQuestionDirectory", () => {
  it("opens the mobile directory, selects freely, and restores focus", async () => {
    const user = userEvent.setup();
    const select = vi.fn();
    render(
      <PaperQuestionDirectory
        questions={[
          {
            id: "q1",
            type: "SingleChoice",
            prompt: "One",
            points: 1,
            sortOrder: 1,
            options: [],
            savedAnswer: null,
          },
        ]}
        answers={{
          q1: {
            value: null,
            savedValue: null,
            status: "idle",
            revision: 0,
            errorMessage: null,
          },
        }}
        currentQuestionId="q1"
        disabled={false}
        onSelect={select}
      />,
    );
    const open = screen.getByRole("button", { name: "打开题目目录" });
    await user.click(open);
    expect(
      screen.getByRole("dialog", { name: "题目目录" }),
    ).toBeInTheDocument();
    await user.click(
      screen.getAllByRole("button", { name: /第 1 题/ }).at(-1)!,
    );
    expect(select).toHaveBeenCalledWith("q1");
    expect(open).toHaveFocus();
  });
});
