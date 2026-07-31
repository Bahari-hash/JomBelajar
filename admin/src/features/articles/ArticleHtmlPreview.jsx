import { useEffect, useRef } from "react";

/** The only rendering boundary for canonical HTML sanitized by the article backend. */
export function ArticleHtmlPreview({ canonicalHtml }) {
  const containerRef = useRef(null);
  useEffect(() => {
    containerRef.current?.querySelectorAll("a[href]").forEach((link) => {
      link.setAttribute("target", "_blank");
      link.setAttribute("rel", "noreferrer noopener");
    });
  }, [canonicalHtml]);
  return (
    <div
      ref={containerRef}
      className="min-w-0 wrap-break-word text-sm leading-7 [&_a]:underline [&_blockquote]:border-l-2 [&_blockquote]:pl-4 [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_h1]:my-4 [&_h1]:text-2xl [&_h1]:font-semibold [&_h2]:my-3 [&_h2]:text-xl [&_h2]:font-semibold [&_h3]:my-3 [&_h3]:text-lg [&_h3]:font-semibold [&_img]:my-4 [&_img]:max-h-128 [&_img]:max-w-full [&_img]:rounded-lg [&_ol]:list-decimal [&_ol]:pl-6 [&_p]:my-3 [&_pre]:my-4 [&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:bg-muted [&_pre]:p-4 [&_ul]:list-disc [&_ul]:pl-6"
      dangerouslySetInnerHTML={{ __html: canonicalHtml }}
    />
  );
}
