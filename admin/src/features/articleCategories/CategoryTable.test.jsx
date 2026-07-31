import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CategoryTable } from "@/features/articleCategories/CategoryTable.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { articleCategory } from "@/test/http.js";

describe("CategoryTable", () => {
  it("shows the category creation time from the API contract", () => {
    const category = articleCategory();

    render(
      <CategoryTable
        categories={[category]}
        onEdit={vi.fn()}
        onDelete={vi.fn()}
      />,
    );

    const table = screen.getByRole("table");
    expect(
      within(table).getByRole("columnheader", { name: "创建时间" }),
    ).toBeVisible();
    expect(
      within(table).getByText(formatDateTime(category.createdAt)),
    ).toBeVisible();
    expect(
      within(table).getByRole("button", { name: `管理分类 ${category.name}` }),
    ).toBeVisible();
  });
});
