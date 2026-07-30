/** Stable ArticleStatus strings and the UI commands allowed by the server state machine. */
export const ARTICLE_STATUSES = Object.freeze({
  DRAFT: "Draft",
  PUBLISHED: "Published",
  ARCHIVED: "Archived",
});

export const ARTICLE_STATUS_OPTIONS = Object.freeze([
  { value: ARTICLE_STATUSES.DRAFT, label: "草稿" },
  { value: ARTICLE_STATUSES.PUBLISHED, label: "已发布" },
  { value: ARTICLE_STATUSES.ARCHIVED, label: "已归档" },
]);

const VALID_STATUSES = new Set(Object.values(ARTICLE_STATUSES));

export function parseArticleStatus(value) {
  if (typeof value !== "string" || !VALID_STATUSES.has(value)) {
    throw new Error("API returned an invalid article status.");
  }
  return value;
}

export function getArticleStatusLabel(status) {
  return (
    ARTICLE_STATUS_OPTIONS.find((option) => option.value === status)?.label ??
    "未知状态"
  );
}

export function getArticleActions(status) {
  if (status === ARTICLE_STATUSES.DRAFT)
    return ["preview", "edit", "publish", "archive"];
  if (status === ARTICLE_STATUSES.PUBLISHED) return ["preview", "unpublish"];
  return status === ARTICLE_STATUSES.ARCHIVED ? ["preview"] : [];
}
