import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ArticleHtmlPreview } from "@/features/articles/ArticleHtmlPreview.jsx";

describe("ArticleHtmlPreview", () => {
  it("renders only the provided canonical field and hardens external links", () => {
    render(
      <ArticleHtmlPreview
        canonicalHtml={
          '<h1>标题</h1><a href="https://example.test/path">外部链接</a>'
        }
      />,
    );
    const link = screen.getByRole("link", { name: "外部链接" });
    expect(screen.getByRole("heading", { name: "标题" })).toBeVisible();
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noreferrer noopener");
  });
});
