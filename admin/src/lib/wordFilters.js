import { PARTS_OF_SPEECH, WORD_STATUSES } from "@/constants/wordStatus.js";

const VALID_STATUSES = new Set(WORD_STATUSES);
const VALID_PARTS_OF_SPEECH = new Set(PARTS_OF_SPEECH);

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

/** Normalizes shareable administrator word list filters. */
export function readWordFilters(searchParams) {
  const status = searchParams.get("status");
  const partOfSpeech = searchParams.get("partOfSpeech");
  return {
    page: parseInteger(searchParams.get("page"), 1, 1, Number.MAX_SAFE_INTEGER),
    pageSize: parseInteger(searchParams.get("pageSize"), 20, 1, 100),
    keyword: parseText(searchParams.get("keyword"), 200),
    language: parseText(searchParams.get("language"), 35),
    status: VALID_STATUSES.has(status) ? status : "",
    partOfSpeech: VALID_PARTS_OF_SPEECH.has(partOfSpeech) ? partOfSpeech : "",
    definition: parseText(searchParams.get("definition"), 200),
  };
}

export function writeWordFilters(filters) {
  const params = new URLSearchParams();
  if (filters.page !== 1) params.set("page", String(filters.page));
  if (filters.pageSize !== 20) params.set("pageSize", String(filters.pageSize));
  for (const key of [
    "keyword",
    "language",
    "status",
    "partOfSpeech",
    "definition",
  ]) {
    if (filters[key]) params.set(key, filters[key]);
  }
  return params;
}
