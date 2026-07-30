import { fromMarkdown } from "mdast-util-from-markdown";

function visitImages(node, images) {
  if (
    node?.type === "image" &&
    Number.isInteger(node.position?.start?.offset) &&
    Number.isInteger(node.position?.end?.offset)
  ) {
    images.push({
      url: node.url,
      alt: node.alt ?? "",
      start: node.position.start.offset,
      end: node.position.end.offset,
    });
  }
  if (Array.isArray(node?.children))
    node.children.forEach((child) => visitImages(child, images));
}

/** Parses CommonMark only to preserve the server's body-media ID/URL relationship. */
export function getMarkdownImages(markdown) {
  const images = [];
  visitImages(fromMarkdown(markdown), images);
  return images;
}

export function getBodyMediaResourceIds(markdown, mediaReferences) {
  const mediaByUrl = new Map(
    mediaReferences.map((media) => [media.url, media.id]),
  );
  const ids = [];
  for (const image of getMarkdownImages(markdown)) {
    const id = mediaByUrl.get(image.url);
    if (!id)
      throw new Error("正文包含尚未确认或无法识别的图片，请重新上传后插入。");
    if (!ids.includes(id)) ids.push(id);
  }
  return ids;
}

export function removeMarkdownImage(markdown, mediaUrl) {
  const matches = getMarkdownImages(markdown)
    .filter((image) => image.url === mediaUrl)
    .sort((left, right) => right.start - left.start);
  return matches.reduce(
    (value, image) => value.slice(0, image.start) + value.slice(image.end),
    markdown,
  );
}
