const GUID = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
function integer(value, fallback, min, max) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= min && parsed <= max
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
export function readPaperCategoryFilters(params) {
  const inactive = params.get("includeInactive");
  return {
    page: integer(params.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: integer(params.get("pageSize"), 20, 1, 100),
    keyword: keyword(params.get("keyword")),
    includeInactive: inactive === null ? true : inactive === "true",
  };
}
export function writePaperCategoryFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (!filters.includeInactive) params.set("includeInactive", "false");
  return params;
}
export function readPaperCategoryId(value) {
  return GUID.test(value ?? "") ? value : "";
}
