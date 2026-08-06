import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import ArticleContent from "@/features/articles/ArticleContent";
import { sanitizeArticleHtml } from "@/features/articles/articleContentSanitizer";

describe("ArticleContent", () => {
  it("renders allowed semantic HTML as safe React nodes", () => {
    const { container } = render(
      <ArticleContent
        contentHtml={`
          <h2 class="server-class">Lesson</h2>
          <p><strong>Strong</strong> and <em>emphasis</em>.</p>
          <blockquote>Quote</blockquote>
          <ul><li>First</li></ul>
          <pre><code>const value = 1;</code></pre>
          <a href="https://example.test/lesson" title="Lesson">Read more</a>
          <img src="https://images.example.test/lesson.png" alt="Lesson image" title="Image">
        `}
      />,
    );

    expect(screen.getByRole("heading", { name: "Lesson" })).not.toHaveAttribute(
      "class",
    );
    const link = screen.getByRole("link", { name: "Read more" });
    expect(link).toHaveAttribute("href", "https://example.test/lesson");
    expect(link).toHaveAttribute("rel", "noreferrer noopener");
    expect(link).toHaveAttribute("target", "_blank");
    expect(screen.getByRole("img", { name: "Lesson image" })).toHaveAttribute(
      "loading",
      "lazy",
    );
    expect(container.querySelector("pre code")?.textContent).toContain(
      "const value",
    );
  });

  it("removes executable markup, attributes, and unsafe URLs", () => {
    const { container } = render(
      <ArticleContent
        contentHtml={`
          <script>window.stolen = true</script>
          <iframe src="https://evil.example"></iframe>
          <form><input value="secret"></form>
          <p id="private" class="hidden" style="color:red" data-secret="yes" onclick="steal()">Safe text</p>
          <a href="javascript:alert(1)" onclick="steal()">Bad link</a>
          <img src="data:image/png;base64,AAAA" onerror="steal()" alt="Bad image">
          <custom-element title="bad">Custom text</custom-element>
        `}
      />,
    );

    expect(
      container.querySelector("script, iframe, form, input, custom-element"),
    ).toBeNull();
    expect(container.querySelector("a, img")).toBeNull();
    const paragraph = screen.getByText("Safe text");
    expect(paragraph).not.toHaveAttribute("id");
    expect(paragraph).not.toHaveAttribute("class");
    expect(paragraph).not.toHaveAttribute("style");
    expect(paragraph).not.toHaveAttribute("data-secret");
    expect(paragraph).not.toHaveAttribute("onclick");
    expect(document.defaultView).not.toHaveProperty("stolen", true);
  });

  it("rejects relative and protocol-relative URLs", () => {
    const sanitized = sanitizeArticleHtml(`
      <a href="/relative">Relative</a>
      <a href="//example.test/path">Protocol relative</a>
      <img src="file:///private/image.png" alt="Private">
    `);

    expect(sanitized).not.toContain("href=");
    expect(sanitized).not.toContain("src=");
  });

  it("shows recoverable states for failed images and empty content", () => {
    const { rerender } = render(
      <ArticleContent contentHtml='<img src="https://images.example.test/broken.png" alt="Broken">' />,
    );
    fireEvent.error(screen.getByRole("img", { name: "Broken" }));
    expect(
      screen.getByRole("img", { name: "正文图片加载失败" }),
    ).toBeInTheDocument();

    rerender(<ArticleContent contentHtml="<script>bad()</script>" />);
    expect(screen.getByText("文章正文暂时不可用。")).toBeInTheDocument();
  });
});
