import dayjs from "dayjs";
import {
  PAPER_QUESTION_TYPES,
  PAPER_STATUSES,
} from "@/constants/paperStatus.js";

const UUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const EMPTY_UUID = "00000000-0000-0000-0000-000000000000";
const HTTP_PROTOCOLS = new Set(["http:", "https:"]);

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
  if (typeof value !== "string") invalid(name);
  return value;
}

function nonEmptyString(value, name) {
  const result = string(value, name);
  if (result.length === 0) invalid(name);
  return result;
}

function uuid(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = nonEmptyString(value, name);
  if (!UUID_PATTERN.test(result) || result.toLowerCase() === EMPTY_UUID)
    invalid(name);
  return result;
}

function number(value, name, options = {}) {
  if (options.nullable && value === null) return null;
  const minimum = options.positive ? Number.MIN_VALUE : (options.minimum ?? 0);
  if (
    !Number.isFinite(value) ||
    value < minimum ||
    (options.maximum !== undefined && value > options.maximum) ||
    (options.integer && !Number.isInteger(value)) ||
    (options.positive && value <= 0)
  )
    invalid(name);
  return value;
}

function boolean(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (typeof value !== "boolean") invalid(name);
  return value;
}

function date(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = nonEmptyString(value, name);
  if (!dayjs(result).isValid()) invalid(name);
  return result;
}

function enumeration(value, allowed, name) {
  if (!allowed.includes(value)) invalid(name);
  return value;
}

function array(value, normalize, name) {
  if (!Array.isArray(value)) invalid(name);
  return value.map(normalize);
}

function paperTags(value) {
  const tags = array(
    value,
    (tag) => nonEmptyString(tag, "paper tag"),
    "paper tags",
  );
  if (tags.length > 10) invalid("paper tags");
  if (
    tags.some(
      (tag) =>
        tag.length > 30 ||
        tag !== tag.trim() ||
        tag !== tag.toLowerCase() ||
        [...tag].some((character) => {
          const codePoint = character.codePointAt(0);
          return codePoint <= 0x1f || (codePoint >= 0x7f && codePoint <= 0x9f);
        }),
    )
  )
    invalid("paper tags");
  if (new Set(tags).size !== tags.length) invalid("paper tags");
  return tags;
}

function httpUrl(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = nonEmptyString(value, name);
  try {
    if (!HTTP_PROTOCOLS.has(new URL(result).protocol)) invalid(name);
  } catch {
    invalid(name);
  }
  return result;
}

function auditUser(value) {
  const source = object(value, "paper audit user");
  return {
    id: uuid(source.id, "paper audit user id"),
    nickname: string(source.nickname, "paper audit nickname", true),
    avatarUrl: httpUrl(source.avatarUrl, "paper audit avatar URL", true),
  };
}

function option(value) {
  const source = object(value, "paper option");
  return {
    id: uuid(source.id, "paper option id"),
    text: string(source.text, "paper option text"),
    isCorrect: boolean(source.isCorrect, "paper option correctness"),
    sortOrder: number(source.sortOrder, "paper option sort order", {
      integer: true,
    }),
  };
}

function acceptedAnswer(value) {
  const source = object(value, "paper accepted answer");
  return {
    id: uuid(source.id, "paper accepted answer id"),
    text: string(source.text, "paper accepted answer text"),
    sortOrder: number(source.sortOrder, "paper accepted answer sort order", {
      integer: true,
    }),
  };
}

function question(value) {
  const source = object(value, "paper question");
  const normalized = {
    id: uuid(source.id, "paper question id"),
    type: enumeration(source.type, PAPER_QUESTION_TYPES, "paper question type"),
    prompt: string(source.prompt, "paper question prompt"),
    explanation: string(source.explanation, "paper explanation", true),
    points: number(source.points, "paper question points", { integer: true }),
    sortOrder: number(source.sortOrder, "paper question sort order", {
      integer: true,
      maximum: 10_000,
    }),
    correctBoolean: boolean(
      source.correctBoolean,
      "paper correct boolean",
      true,
    ),
    fillBlankCaseSensitive: boolean(
      source.fillBlankCaseSensitive,
      "paper fill blank case sensitivity",
    ),
    options: array(source.options, option, "paper options"),
    acceptedAnswers: array(
      source.acceptedAnswers,
      acceptedAnswer,
      "paper accepted answers",
    ),
  };
  if (
    normalized.points < 1 ||
    normalized.points > 100 ||
    normalized.options.length > 10 ||
    normalized.acceptedAnswers.length > 20 ||
    (normalized.type === "SingleChoice" &&
      normalized.options.filter((item) => item.isCorrect).length > 1)
  )
    invalid("paper question boundaries");
  if (
    (normalized.type === "SingleChoice" &&
      (normalized.correctBoolean !== null ||
        normalized.fillBlankCaseSensitive ||
        normalized.acceptedAnswers.length > 0)) ||
    (normalized.type === "TrueFalse" &&
      (normalized.options.length > 0 ||
        normalized.acceptedAnswers.length > 0 ||
        normalized.fillBlankCaseSensitive)) ||
    (normalized.type === "FillBlank" &&
      (normalized.options.length > 0 || normalized.correctBoolean !== null))
  )
    invalid("paper question shape");
  return normalized;
}

function commonPaper(source) {
  const normalized = {
    id: uuid(source.id, "paper id"),
    title: string(source.title, "paper title"),
    tags: paperTags(source.tags),
    status: enumeration(source.status, PAPER_STATUSES, "paper status"),
    totalScore: number(source.totalScore, "paper total score", {
      integer: true,
    }),
    passingScore: number(source.passingScore, "paper passing score", {
      integer: true,
    }),
    attemptCount: number(source.attemptCount, "paper attempt count", {
      integer: true,
    }),
    createdBy: auditUser(source.createdBy),
    lastEditor: auditUser(source.lastEditor),
    publishedAt: date(source.publishedAt, "paper published at", true),
    archivedAt: date(source.archivedAt, "paper archived at", true),
    createdAt: date(source.createdAt, "paper created at"),
    updatedAt: date(source.updatedAt, "paper updated at"),
    concurrencyStamp: uuid(source.concurrencyStamp, "paper concurrency stamp"),
  };
  if (
    normalized.totalScore > 20_000 ||
    normalized.passingScore > normalized.totalScore ||
    (normalized.status === "Archived" && normalized.archivedAt === null)
  )
    invalid("paper scoring or archive state");
  return normalized;
}

export function normalizePaperListItem(value) {
  const source = object(value, "paper list item");
  return {
    ...commonPaper(source),
    questionCount: number(source.questionCount, "paper question count", {
      integer: true,
      maximum: 200,
    }),
  };
}

export function normalizeAdminPaper(value) {
  const source = object(value, "paper details");
  const normalized = {
    ...commonPaper(source),
    passingScorePercentage: number(
      source.passingScorePercentage,
      "paper passing score percentage",
      { integer: true, minimum: 1, maximum: 100 },
    ),
    description: string(source.description, "paper description", true),
    instructions: string(source.instructions, "paper instructions", true),
    questions: array(source.questions, question, "paper questions"),
  };
  if (normalized.questions.length > 200) invalid("paper question count");
  return normalized;
}

export function normalizePaperValidation(value) {
  const source = object(value, "paper validation");
  return {
    isValid: boolean(source.isValid, "paper validation state"),
    issues: array(
      source.issues,
      (item) => {
        const issue = object(item, "paper validation issue");
        return {
          field: nonEmptyString(issue.field, "paper validation field"),
          errorCode: nonEmptyString(
            issue.errorCode,
            "paper validation error code",
          ),
          message: nonEmptyString(issue.message, "paper validation message"),
          questionId: uuid(issue.questionId, "paper validation question", true),
          childId: uuid(issue.childId, "paper validation child", true),
        };
      },
      "paper validation issues",
    ),
  };
}

export function normalizePaperTag(value) {
  const source = object(value, "Paper tag");
  return {
    name: nonEmptyString(source.name, "Paper tag name"),
    paperCount: number(source.paperCount, "Paper tag count", {
      integer: true,
    }),
  };
}

function normalizePage(value, normalizeItem, name) {
  const source = object(value, name);
  return {
    items: array(source.items, normalizeItem, `${name} items`),
    page: number(source.page, `${name} page`, { integer: true, minimum: 1 }),
    pageSize: number(source.pageSize, `${name} page size`, {
      integer: true,
      minimum: 1,
      maximum: 100,
    }),
    totalCount: number(source.totalCount, `${name} total count`, {
      integer: true,
    }),
    totalPages: number(source.totalPages, `${name} total pages`, {
      integer: true,
    }),
  };
}

export const normalizePaperTagPage = (value) =>
  normalizePage(value, normalizePaperTag, "Paper tag page");

export function normalizePaperPage(value) {
  return normalizePage(value, normalizePaperListItem, "paper pagination");
}
