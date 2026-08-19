import dayjs from "dayjs";

const UUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;

function invalid(name) {
  throw new Error(`API returned invalid ${name}.`);
}
function object(value, name) {
  if (!value || typeof value !== "object" || Array.isArray(value))
    invalid(name);
  return value;
}
function string(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (typeof value !== "string" || value.length === 0) invalid(name);
  return value;
}
function uuid(value, name) {
  const result = string(value, name);
  if (!UUID_PATTERN.test(result)) invalid(name);
  return result;
}
function integer(value, name) {
  if (!Number.isInteger(value) || value < 0) invalid(name);
  return value;
}
function boolean(value, name) {
  if (typeof value !== "boolean") invalid(name);
  return value;
}
function date(value, name) {
  const result = string(value, name);
  if (!dayjs(result).isValid()) invalid(name);
  return result;
}

export function normalizePaperCategory(value) {
  const source = object(value, "paper category");
  return {
    id: uuid(source.id, "paper category id"),
    name: string(source.name, "paper category name"),
    slug: string(source.slug, "paper category slug"),
    description: string(source.description, "paper category description", true),
    isActive: boolean(source.isActive, "paper category state"),
    paperCount: integer(source.paperCount, "paper category count"),
    createdAt: date(source.createdAt, "paper category createdAt"),
  };
}

export function normalizePaperCategoryPage(value) {
  const source = object(value, "paper category page");
  if (!Array.isArray(source.items)) invalid("paper category items");
  return {
    items: source.items.map(normalizePaperCategory),
    page: integer(source.page, "paper category page number") || 1,
    pageSize: integer(source.pageSize, "paper category page size") || 20,
    totalCount: integer(source.totalCount, "paper category total count"),
    totalPages: integer(source.totalPages, "paper category total pages"),
  };
}
