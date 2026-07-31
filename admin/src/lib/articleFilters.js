import { ARTICLE_STATUS_OPTIONS } from "@/constants/articleStatus.js";

const VALID_STATUSES = new Set(
  ARTICLE_STATUS_OPTIONS.map((option) => option.value),
);
const GUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

function parseInteger(value, fallback, minimum, maximum) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

function parseKeyword(value) {
  const keyword = value?.trim() ?? "";
  return keyword.length <= 200 &&
    !Array.from(keyword).some((character) => /\p{Cc}/u.test(character))
    ? keyword
    : "";
}

/** Normalizes the shareable filters supported by the editor article endpoint. */
export function readArticleFilters(searchParams) {
  const status = searchParams.get("status");
  const categoryId = searchParams.get("categoryId") ?? "";
  return {
    page: parseInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: parseInteger(searchParams.get("pageSize"), 20, 1, 100),
    keyword: parseKeyword(searchParams.get("keyword")),
    categoryId:
      GUID_PATTERN.test(categoryId) && categoryId.toLowerCase() !== EMPTY_GUID
        ? categoryId
        : "",
    status: VALID_STATUSES.has(status) ? status : "",
  };
}

export function writeArticleFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.categoryId) params.set("categoryId", filters.categoryId);
  if (filters.status) params.set("status", filters.status);
  return params;
}

/** Normalizes the shareable filters supported by the administrator category endpoint. */
export function readArticleCategoryFilters(searchParams) {
  const includeInactiveValue = searchParams.get("includeInactive");
  return {
    page: parseInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: parseInteger(searchParams.get("pageSize"), 20, 1, 100),
    keyword: parseKeyword(searchParams.get("keyword")),
    includeInactive:
      includeInactiveValue === null ? true : includeInactiveValue === "true",
  };
}

export function writeArticleCategoryFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (!filters.includeInactive) params.set("includeInactive", "false");
  return params;
}
