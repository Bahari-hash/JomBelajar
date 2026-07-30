import { describe, expect, it } from "vitest";
import {
  getBodyMediaResourceIds,
  getMarkdownImages,
  removeMarkdownImage,
} from "@/lib/markdownImages.js";

describe("Markdown image mapping", () => {
  const media = [
    { id: "image-1", url: "https://media.example.test/one.png" },
    { id: "image-2", url: "https://media.example.test/two.webp" },
  ];

  it("distinguishes image nodes from links and deduplicates resource ids", () => {
    const markdown =
      "[link](https://media.example.test/one.png)\n\n![一](https://media.example.test/one.png)\n![二](https://media.example.test/two.webp)\n![重复](https://media.example.test/one.png)";
    expect(getMarkdownImages(markdown)).toHaveLength(3);
    expect(getBodyMediaResourceIds(markdown, media)).toEqual([
      "image-1",
      "image-2",
    ]);
  });

  it("removes only the selected image nodes without rewriting other Markdown", () => {
    const markdown =
      "前文 **保持**\n\n![图片](https://media.example.test/one.png)\n\n后文";
    expect(removeMarkdownImage(markdown, media[0].url)).toBe(
      "前文 **保持**\n\n\n\n后文",
    );
  });

  it("rejects unconfirmed image URLs", () => {
    expect(() =>
      getBodyMediaResourceIds("![外部](https://unknown.test/a.png)", media),
    ).toThrow("尚未确认");
  });
});
