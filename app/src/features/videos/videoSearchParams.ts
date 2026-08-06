export const VIDEO_PAGE_SIZE = 12;
export const INITIAL_VIDEO_CATEGORY_PAGE_SIZE = 100;
export const VIDEO_CATEGORY_PAGE_SIZE = 20;
export const MAX_VIDEO_KEYWORD_LENGTH = 200;

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface VideoSearchState {
  keyword: string;
  categoryId: string | null;
  page: number;
}

export interface ParsedVideoSearch {
  state: VideoSearchState;
  normalizedParams: URLSearchParams;
  needsNormalization: boolean;
}

/** Validates video identifiers and filter category values before requests. */
export function isVideoGuid(value: string | null | undefined): value is string {
  return typeof value === "string" && GUID_PATTERN.test(value);
}

/** Matches the backend's control-character restriction without a regular expression. */
export function hasVideoControlCharacters(value: string) {
  return Array.from(value).some((character) => {
    const codePoint = character.codePointAt(0);
    return codePoint !== undefined && (codePoint <= 31 || codePoint === 127);
  });
}

function normalizeKeyword(value: string | null) {
  const keyword = value?.trim() ?? "";
  return keyword.length <= MAX_VIDEO_KEYWORD_LENGTH &&
    !hasVideoControlCharacters(keyword)
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

/** Parses list search params into canonical, safe catalog state. */
export function parseVideoSearchParams(
  params: URLSearchParams,
): ParsedVideoSearch {
  const keyword = normalizeKeyword(params.get("keyword"));
  const rawCategoryId = params.get("categoryId");
  const categoryId = isVideoGuid(rawCategoryId)
    ? rawCategoryId.toLowerCase()
    : null;
  const page = normalizePage(params.get("page"));
  const normalizedParams = createVideoSearchParams({
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

/** Serializes catalog state while omitting empty/default values. */
export function createVideoSearchParams(state: VideoSearchState) {
  const params = new URLSearchParams();
  if (state.keyword) params.set("keyword", state.keyword);
  if (state.categoryId) params.set("categoryId", state.categoryId);
  if (state.page > 1) params.set("page", String(state.page));
  return params;
}
