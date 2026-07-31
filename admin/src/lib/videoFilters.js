import {
  PROCESSING_STATUS_OPTIONS,
  PUBLICATION_STATUS_OPTIONS,
} from "@/constants/videoStatus.js";

const PROCESSING = new Set(PROCESSING_STATUS_OPTIONS.map(({ value }) => value));
const PUBLICATION = new Set(
  PUBLICATION_STATUS_OPTIONS.map(({ value }) => value),
);
const UUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function integer(value, fallback, minimum, maximum) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

function keyword(value) {
  const result = value?.trim() ?? "";
  return result.length <= 200 &&
    !Array.from(result).some((item) => /\p{Cc}/u.test(item))
    ? result
    : "";
}

function uuid(value) {
  return UUID_PATTERN.test(value ?? "") ? value : "";
}

/** Reads canonical shareable state for the administrator video list. */
export function readVideoFilters(searchParams) {
  const processingStatus = searchParams.get("processingStatus");
  const publicationStatus = searchParams.get("publicationStatus");
  return {
    page: integer(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: integer(searchParams.get("pageSize"), 20, 1, 100),
    keyword: keyword(searchParams.get("keyword")),
    processingStatus: PROCESSING.has(processingStatus) ? processingStatus : "",
    publicationStatus: PUBLICATION.has(publicationStatus)
      ? publicationStatus
      : "",
    categoryId: uuid(searchParams.get("categoryId")),
    createdById: uuid(searchParams.get("createdById")),
  };
}

export function writeVideoFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  for (const key of [
    "keyword",
    "processingStatus",
    "publicationStatus",
    "categoryId",
    "createdById",
  ])
    if (filters[key]) params.set(key, filters[key]);
  return params;
}

export function readVideoCategoryFilters(searchParams) {
  const includeInactive = searchParams.get("includeInactive");
  return {
    page: integer(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: integer(searchParams.get("pageSize"), 20, 1, 100),
    keyword: keyword(searchParams.get("keyword")),
    includeInactive:
      includeInactive === null ? true : includeInactive === "true",
  };
}

export function writeVideoCategoryFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (!filters.includeInactive) params.set("includeInactive", "false");
  return params;
}
