import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { PaperTagInput } from "./PaperTagInput.jsx";

function Harness({ initial = [] }) {
  const [value, setValue] = useState(initial);
  return <PaperTagInput value={value} onChange={setValue} />;
}

describe("PaperTagInput", () => {
  it("adds canonical lowercase tags, ignores duplicates, and removes tags", async () => {
    const user = userEvent.setup();
    render(<Harness />);
    const input = screen.getByRole("textbox", { name: "试卷标签" });

    await user.type(input, " Grammar ");
    await user.keyboard("{Enter}");
    await user.type(input, "grammar");
    await user.keyboard("{Enter}");
    await user.type(input, "A2");
    await user.click(screen.getByRole("button", { name: "添加标签" }));

    expect(screen.getAllByText("grammar")).toHaveLength(1);
    expect(screen.getByText("a2")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "删除标签 grammar" }));
    expect(screen.queryByText("grammar")).not.toBeInTheDocument();
  });

  it("shows validation feedback and prevents tags beyond the limit", async () => {
    render(
      <Harness
        initial={Array.from({ length: 10 }, (_, index) => `tag${index}`)}
      />,
    );
    const input = screen.getByRole("textbox", { name: "试卷标签" });
    expect(input).toBeDisabled();
    expect(screen.getByText("最多添加 10 个标签。")).toBeVisible();
  });
});
