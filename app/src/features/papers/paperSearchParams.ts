export const PAPER_PAGE_SIZE = 12;
export const MAX_PAPER_KEYWORD_LENGTH = 200;

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface PaperSearchState {
  keyword: string;
  categoryId: string | null;
  page: number;
}

export interface ParsedPaperSearch {
  state: PaperSearchState;
  normalizedParams: URLSearchParams;
  needsNormalization: boolean;
}

export function hasPaperControlCharacters(value: string) {
  return Array.from(value).some((character) => {
    const codePoint = character.codePointAt(0) ?? 0;
    return codePoint <= 0x1f || codePoint === 0x7f;
  });
}

function normalizeKeyword(value: string) {
  const keyword = value.trim();
  return keyword.length <= MAX_PAPER_KEYWORD_LENGTH &&
    !hasPaperControlCharacters(keyword)
    ? keyword
    : null;
}

function normalizePage(value: string) {
  if (!/^[1-9]\d*$/.test(value)) return null;
  const page = Number(value);
  return Number.isSafeInteger(page) ? page : null;
}

function getFirstSafeValue<T>(
  values: string[],
  normalize: (value: string) => T | null,
  fallback: T,
) {
  for (const value of values) {
    const normalized = normalize(value);
    if (normalized !== null) return normalized;
  }
  return fallback;
}

export function parsePaperSearchParams(
  params: URLSearchParams,
): ParsedPaperSearch {
  const keyword = getFirstSafeValue(
    params.getAll("keyword"),
    normalizeKeyword,
    "",
  );
  const categoryId = getFirstSafeValue(
    params.getAll("categoryId"),
    (value) => (GUID_PATTERN.test(value) ? value : null),
    null as string | null,
  );
  const page = getFirstSafeValue(params.getAll("page"), normalizePage, 1);
  const state = { keyword, categoryId, page };
  const normalizedParams = createPaperSearchParams(state);

  return {
    state,
    normalizedParams,
    needsNormalization: normalizedParams.toString() !== params.toString(),
  };
}

export function createPaperSearchParams(state: PaperSearchState) {
  const params = new URLSearchParams();
  const keyword = normalizeKeyword(state.keyword) ?? "";
  if (keyword) params.set("keyword", keyword);
  if (state.categoryId && GUID_PATTERN.test(state.categoryId))
    params.set("categoryId", state.categoryId);
  if (Number.isSafeInteger(state.page) && state.page > 1) {
    params.set("page", String(state.page));
  }
  return params;
}

export function isPaperGuid(value: string | null | undefined): value is string {
  return typeof value === "string" && GUID_PATTERN.test(value);
}
