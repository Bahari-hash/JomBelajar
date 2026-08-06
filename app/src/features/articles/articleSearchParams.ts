export const ARTICLE_PAGE_SIZE = 12;
export const INITIAL_CATEGORY_PAGE_SIZE = 100;
export const CATEGORY_DIALOG_PAGE_SIZE = 20;
export const MAX_ARTICLE_KEYWORD_LENGTH = 200;

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface ArticleSearchState {
  keyword: string;
  categoryId: string | null;
  page: number;
}

export interface ParsedArticleSearch {
  state: ArticleSearchState;
  normalizedParams: URLSearchParams;
  needsNormalization: boolean;
}

/** Validates route identifiers before any public article request is made. */
export function isGuid(value: string | null | undefined): value is string {
  return typeof value === "string" && GUID_PATTERN.test(value);
}

/** Detects URL keyword characters rejected by the backend validator. */
export function hasControlCharacters(value: string) {
  return Array.from(value).some((character) => {
    const codePoint = character.codePointAt(0);
    return codePoint !== undefined && (codePoint <= 31 || codePoint === 127);
  });
}

function normalizeKeyword(value: string | null) {
  const keyword = value?.trim() ?? "";
  return keyword.length <= MAX_ARTICLE_KEYWORD_LENGTH &&
    !hasControlCharacters(keyword)
    ? keyword
    : "";
}

function normalizePage(value: string | null) {
  if (!value || !/^[1-9]\d*$/.test(value)) {
    return 1;
  }
  const page = Number(value);
  return Number.isSafeInteger(page) ? page : 1;
}

/** Parses article list URL state and provides its canonical search string. */
export function parseArticleSearchParams(
  params: URLSearchParams,
): ParsedArticleSearch {
  const keyword = normalizeKeyword(params.get("keyword"));
  const rawCategoryId = params.get("categoryId");
  const categoryId = isGuid(rawCategoryId) ? rawCategoryId.toLowerCase() : null;
  const page = normalizePage(params.get("page"));
  const normalizedParams = createArticleSearchParams({
    keyword,
    categoryId,
    page,
  });

  return {
    state: { keyword, categoryId, page },
    normalizedParams,
    needsNormalization: normalizedParams.toString() !== params.toString(),
  };
}

/** Serializes article list state without redundant default values. */
export function createArticleSearchParams(state: ArticleSearchState) {
  const params = new URLSearchParams();
  if (state.keyword) {
    params.set("keyword", state.keyword);
  }
  if (state.categoryId) {
    params.set("categoryId", state.categoryId);
  }
  if (state.page > 1) {
    params.set("page", String(state.page));
  }
  return params;
}
