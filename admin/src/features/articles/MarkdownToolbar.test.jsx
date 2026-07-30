import { useRef, useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { MarkdownToolbar } from "@/features/articles/MarkdownToolbar.jsx";

function ToolbarHarness() {
  const [value, setValue] = useState("开头 选中 结尾");
  const textareaRef = useRef(null);
  return (
    <>
      <MarkdownToolbar
        textareaRef={textareaRef}
        value={value}
        onChange={setValue}
      />
      <textarea ref={textareaRef} aria-label="正文" value={value} readOnly />
    </>
  );
}

describe("MarkdownToolbar", () => {
  it("prefixes a line without deleting content before a partial selection", async () => {
    const user = userEvent.setup();
    render(
      <TooltipProvider>
        <ToolbarHarness />
      </TooltipProvider>,
    );
    const textarea = screen.getByRole("textbox", { name: "正文" });
    fireEvent.select(textarea, {
      target: { selectionStart: 3, selectionEnd: 5 },
    });

    await user.click(screen.getByRole("button", { name: "引用" }));

    expect(textarea).toHaveValue("> 开头 选中 结尾");
  });
});
