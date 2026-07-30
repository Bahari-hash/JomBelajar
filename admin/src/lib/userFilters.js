import { USER_ROLES } from "@/services/roles.js";

export const USER_STATUS_OPTIONS = Object.freeze([
  { value: "Active", label: "正常" },
  { value: "Banned", label: "已封禁" },
  { value: "Deleted", label: "已删除" },
]);

const VALID_ROLES = new Set(Object.values(USER_ROLES));
const VALID_STATUSES = new Set(
  USER_STATUS_OPTIONS.map((option) => option.value),
);

function parseInteger(value, fallback, minimum, maximum) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum
    ? parsed
    : fallback;
}

/** Normalizes shareable user-list state to the exact query parameters supported by the API. */
export function readUserFilters(searchParams) {
  const keywordValue = searchParams.get("keyword")?.trim() ?? "";
  const keyword =
    keywordValue.length <= 200 &&
    !Array.from(keywordValue).some((character) => /\p{Cc}/u.test(character))
      ? keywordValue
      : "";
  const roleValue = searchParams.get("role");
  const statusValue = searchParams.get("status");

  return {
    page: parseInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: parseInteger(searchParams.get("pageSize"), 20, 1, 100),
    keyword,
    role: VALID_ROLES.has(roleValue) ? roleValue : "",
    status: VALID_STATUSES.has(statusValue) ? statusValue : "",
  };
}

export function writeUserFilters(filters) {
  const searchParams = new URLSearchParams();
  if (filters.page !== 1) searchParams.set("page", String(filters.page));
  if (filters.pageSize !== 20)
    searchParams.set("pageSize", String(filters.pageSize));
  if (filters.keyword) searchParams.set("keyword", filters.keyword);
  if (filters.role) searchParams.set("role", filters.role);
  if (filters.status) searchParams.set("status", filters.status);
  return searchParams;
}
