import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { VideoCategoryTable } from "@/features/videoCategories/VideoCategoryTable.jsx";
import { formatDateTime } from "@/lib/dateTime.js";
import { videoCategory } from "@/test/http.js";

describe("VideoCategoryTable", () => {
  it("shows the category creation time from the API contract", () => {
    const category = videoCategory();

    render(
      <VideoCategoryTable
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
      within(table).getByRole("button", {
        name: `管理视频分类 ${category.name}`,
      }),
    ).toBeVisible();
  });
});
