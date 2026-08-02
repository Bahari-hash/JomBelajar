import { PAPER_STATUSES } from "@/constants/paperStatus.js";

const VALID_STATUSES = new Set(PAPER_STATUSES);

function parseInteger(value, fallback, minimum, maximum) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

function parseText(value, maximum) {
  const text = value?.trim() ?? "";
  return text.length <= maximum &&
    !Array.from(text).some((character) => /\p{Cc}/u.test(character))
    ? text
    : "";
}

/** Normalizes shareable administrator paper list filters. */
export function readPaperFilters(searchParams) {
  const status = searchParams.get("status");
  return {
    page: parseInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: parseInteger(searchParams.get("pageSize"), 20, 1, 100),
    keyword: parseText(searchParams.get("keyword"), 200),
    language: parseText(searchParams.get("language"), 35),
    status: VALID_STATUSES.has(status) ? status : "",
  };
}

export function writePaperFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  for (const key of ["keyword", "language", "status"]) {
    if (filters[key]) params.set(key, filters[key]);
  }
  return params;
}
