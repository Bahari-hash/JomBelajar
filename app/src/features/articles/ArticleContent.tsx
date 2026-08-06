import { createElement, Fragment, useState, type ReactNode } from "react";
import parse, {
  domToReact,
  Element,
  type DOMNode,
  type HTMLReactParserOptions,
} from "html-react-parser";
import { ImageOff } from "lucide-react";
import {
  ARTICLE_CONTENT_TAGS,
  sanitizeArticleHtml,
} from "@/features/articles/articleContentSanitizer";
import { isSafeHttpUrl } from "@/features/articles/articleUtils";
import styles from "@/features/articles/ArticleContent.module.css";

const CONTENT_TAGS = new Set<string>(ARTICLE_CONTENT_TAGS);

interface ArticleContentProps {
  contentHtml: string;
}

interface ContentImageProps {
  src: string;
  alt?: string;
  title?: string;
}

function ContentImage({ src, alt = "", title }: ContentImageProps) {
  const [failed, setFailed] = useState(false);
  if (failed) {
    return (
      <span className={styles.imageFallback} role="img" aria-label="正文图片加载失败">
        <ImageOff aria-hidden="true" />
        正文图片暂时无法显示
      </span>
    );
  }
  return (
    <img
      alt={alt}
      decoding="async"
      loading="lazy"
      referrerPolicy="no-referrer"
      src={src}
      title={title}
      onError={() => setFailed(true)}
    />
  );
}

function hasRenderableContent(contentHtml: string) {
  return (
    /<img\b/i.test(contentHtml) ||
    contentHtml.replace(/<[^>]*>/g, "").replace(/&nbsp;/gi, "").trim() !== ""
  );
}

/** Safely renders canonical article HTML without raw HTML injection. */
export default function ArticleContent({ contentHtml }: ArticleContentProps) {
  const sanitizedHtml = sanitizeArticleHtml(contentHtml);
  if (!hasRenderableContent(sanitizedHtml)) {
    return (
      <div className={styles.unavailable} role="status">
        文章正文暂时不可用。
      </div>
    );
  }

  const options: HTMLReactParserOptions = {
    replace(domNode) {
      if (!(domNode instanceof Element)) {
        return undefined;
      }
      const tagName = domNode.name.toLowerCase();
      if (!CONTENT_TAGS.has(tagName)) {
        return createElement(Fragment);
      }
      const children = domToReact(domNode.children as DOMNode[], options);

      if (tagName === "a") {
        const href = domNode.attribs.href;
        return isSafeHttpUrl(href)
          ? createElement(
              "a",
              {
                href,
                title: domNode.attribs.title,
                target: "_blank",
                rel: "noreferrer noopener",
              },
              children,
            )
          : createElement("span", null, children);
      }
      if (tagName === "img") {
        const src = domNode.attribs.src;
        return isSafeHttpUrl(src) ? (
          <ContentImage
            src={src}
            alt={domNode.attribs.alt}
            title={domNode.attribs.title}
          />
        ) : createElement(Fragment);
      }
      return createElement(tagName, null, children as ReactNode);
    },
  };

  return <div className={styles.content}>{parse(sanitizedHtml, options)}</div>;
}
