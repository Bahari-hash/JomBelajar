import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ArticleFilters } from "@/features/articles/ArticleFilters.jsx";

const DEFAULT_FILTERS = Object.freeze({
  page: 1,
  pageSize: 20,
  keyword: "",
  categoryId: "",
  status: "",
});

describe("ArticleFilters", () => {
  it("resets a category selected only in the local draft", async () => {
    const user = userEvent.setup();
    const onReset = vi.fn();
    render(
      <ArticleFilters
        filters={DEFAULT_FILTERS}
        categories={[
          {
            id: "0198c8d0-1234-7abc-8def-0123456789ab",
            name: "语法",
            isActive: true,
          },
        ]}
        onApply={vi.fn()}
        onReset={onReset}
      />,
    );
    const category = screen.getByRole("combobox", { name: "分类" });

    await user.click(category);
    await user.click(await screen.findByRole("option", { name: "语法" }));
    expect(category).toHaveTextContent("语法");

    await user.click(screen.getByRole("button", { name: "重置筛选" }));

    expect(category).toHaveTextContent("全部分类");
    expect(onReset).toHaveBeenCalledOnce();
  });
});
