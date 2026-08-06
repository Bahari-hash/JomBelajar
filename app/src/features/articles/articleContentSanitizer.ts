import DOMPurify, { type UponSanitizeAttributeHook } from "dompurify";
import { isSafeHttpUrl } from "@/features/articles/articleUtils";

export const ARTICLE_CONTENT_TAGS = [
  "p",
  "br",
  "strong",
  "em",
  "u",
  "s",
  "blockquote",
  "h1",
  "h2",
  "h3",
  "h4",
  "h5",
  "h6",
  "ul",
  "ol",
  "li",
  "pre",
  "code",
  "a",
  "img",
] as const;

/** Applies the consumer article whitelist before any HTML is parsed into React. */
export function sanitizeArticleHtml(contentHtml: string) {
  const restrictAttributes: UponSanitizeAttributeHook = (node, data) => {
    const tagName = node.nodeName.toLowerCase();
    const allowedForTag =
      tagName === "a"
        ? new Set(["href", "title"])
        : tagName === "img"
          ? new Set(["src", "alt", "title"])
          : new Set<string>();
    if (!allowedForTag.has(data.attrName)) {
      data.keepAttr = false;
    } else if (
      (data.attrName === "href" || data.attrName === "src") &&
      !isSafeHttpUrl(data.attrValue)
    ) {
      data.keepAttr = false;
    }
  };

  DOMPurify.addHook("uponSanitizeAttribute", restrictAttributes);
  try {
    return DOMPurify.sanitize(contentHtml, {
      ALLOWED_TAGS: [...ARTICLE_CONTENT_TAGS],
      ALLOWED_ATTR: ["href", "title", "src", "alt"],
      ALLOW_ARIA_ATTR: false,
      ALLOW_DATA_ATTR: false,
      ALLOWED_URI_REGEXP: /^https?:\/\//i,
    });
  } finally {
    DOMPurify.removeHook("uponSanitizeAttribute", restrictAttributes);
  }
}
