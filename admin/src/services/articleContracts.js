import dayjs from "dayjs";
import { parseArticleStatus } from "@/constants/articleStatus.js";
import { AUDIO_RESOURCE_STATUSES } from "@/services/audioContracts.js";

const HTTP_PROTOCOLS = new Set(["http:", "https:"]);
const RESOURCE_MODULE = "ArticlePicture";
const RESOURCE_STATUS = "Active";

function assertObject(value, name) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`API returned invalid ${name} data.`);
  }
  return value;
}

function requireString(value, name) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`API returned invalid ${name}.`);
  }
  return value;
}

function optionalString(value, name) {
  if (value === null) return null;
  return requireString(value, name);
}

function requireInteger(value, name) {
  if (!Number.isInteger(value) || value < 0)
    throw new Error(`API returned invalid ${name}.`);
  return value;
}

function requireNumber(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (!Number.isFinite(value) || value < 0)
    throw new Error(`API returned invalid ${name}.`);
  return value;
}

function requireBoolean(value, name) {
  if (typeof value !== "boolean")
    throw new Error(`API returned invalid ${name}.`);
  return value;
}

function requireDate(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const date = requireString(value, name);
  if (!dayjs(date).isValid()) throw new Error(`API returned invalid ${name}.`);
  return date;
}

export function normalizeHttpUrl(value, name = "URL", nullable = false) {
  if (nullable && value === null) return null;
  const raw = requireString(value, name);
  let parsed;
  try {
    parsed = new URL(raw);
  } catch {
    throw new Error(`API returned invalid ${name}.`);
  }
  if (!HTTP_PROTOCOLS.has(parsed.protocol))
    throw new Error(`API returned invalid ${name}.`);
  return raw;
}

function normalizeUser(value) {
  const user = assertObject(value, "article user");
  return {
    id: requireString(user.id, "article user id"),
    nickname:
      user.nickname === null
        ? null
        : requireString(user.nickname, "article user nickname"),
    avatarUrl:
      user.avatarUrl === null
        ? null
        : normalizeHttpUrl(user.avatarUrl, "avatar URL"),
  };
}

export function normalizeArticleCategory(value) {
  const category = assertObject(value, "article category");
  return {
    id: requireString(category.id, "category id"),
    name: requireString(category.name, "category name"),
    slug: requireString(category.slug, "category slug"),
    description:
      category.description === null
        ? null
        : requireString(category.description, "category description"),
    isActive: requireBoolean(category.isActive, "category state"),
    articleCount: requireInteger(
      category.articleCount,
      "category article count",
    ),
    createdAt: requireDate(category.createdAt, "createdAt"),
  };
}

function normalizeCategorySummary(value) {
  const category = assertObject(value, "article category summary");
  return {
    id: requireString(category.id, "category id"),
    name: requireString(category.name, "category name"),
    slug: requireString(category.slug, "category slug"),
  };
}

function normalizeMediaReference(value) {
  const media = assertObject(value, "article media reference");
  return {
    id: requireString(media.id, "media id"),
    url: normalizeHttpUrl(media.url, "media URL"),
  };
}

function normalizeArticleReadingAudio(value) {
  if (value === null) return null;
  const audio = assertObject(value, "article reading audio");
  if (!AUDIO_RESOURCE_STATUSES.includes(audio.status))
    throw new Error("API returned invalid article reading audio status.");
  return {
    id: requireString(audio.id, "article reading audio id"),
    name: requireString(audio.name, "article reading audio name"),
    status: audio.status,
    durationSeconds: requireNumber(
      audio.durationSeconds,
      "article reading audio duration",
      true,
    ),
    lastFailureCode: optionalString(
      audio.lastFailureCode,
      "article reading audio failure code",
    ),
  };
}

export function normalizeArticleListItem(value) {
  const article = assertObject(value, "article list item");
  if (!Array.isArray(article.categories))
    throw new Error("API returned invalid article categories.");
  return {
    id: requireString(article.id, "article id"),
    title: requireString(article.title, "article title"),
    summary: optionalString(article.summary, "article summary"),
    status: parseArticleStatus(article.status),
    categories: article.categories.map(normalizeCategorySummary),
    coverUrl: normalizeHttpUrl(article.coverUrl, "cover URL", true),
    author: normalizeUser(article.author),
    publishedAt: requireDate(article.publishedAt, "publishedAt", true),
    updatedAt: requireDate(article.updatedAt, "updatedAt"),
  };
}

export function normalizeAdminArticle(value) {
  const source = assertObject(value, "editor article");
  if (!Array.isArray(source.categories))
    throw new Error("API returned invalid article categories.");
  if (!Array.isArray(source.bodyMedia))
    throw new Error("API returned invalid body media.");
  const coverMedia =
    source.coverMedia === null
      ? null
      : normalizeMediaReference(source.coverMedia);
  return {
    id: requireString(source.id, "article id"),
    title: requireString(source.title, "article title"),
    summary: optionalString(source.summary, "article summary"),
    status: parseArticleStatus(source.status),
    categories: source.categories.map(normalizeCategorySummary),
    coverUrl: coverMedia?.url ?? null,
    author: normalizeUser(source.author),
    publishedAt: requireDate(source.publishedAt, "publishedAt", true),
    updatedAt: requireDate(source.updatedAt, "updatedAt"),
    contentMarkdown:
      typeof source.contentMarkdown === "string"
        ? source.contentMarkdown
        : (() => {
            throw new Error("API returned invalid Markdown.");
          })(),
    contentHtml:
      typeof source.contentHtml === "string"
        ? source.contentHtml
        : (() => {
            throw new Error("API returned invalid article HTML.");
          })(),
    lastEditor: normalizeUser(source.lastEditor),
    coverMedia,
    bodyMedia: source.bodyMedia.map(normalizeMediaReference),
    readingAudio: normalizeArticleReadingAudio(source.readingAudio),
    concurrencyStamp: requireString(
      source.concurrencyStamp,
      "concurrency stamp",
    ),
    createdAt: requireDate(source.createdAt, "createdAt"),
  };
}

export function normalizePage(value, normalizeItem) {
  const page = assertObject(value, "pagination");
  if (!Array.isArray(page.items))
    throw new Error("API returned invalid pagination items.");
  return {
    items: page.items.map(normalizeItem),
    page: requireInteger(page.page, "page"),
    pageSize: requireInteger(page.pageSize, "pageSize"),
    totalCount: requireInteger(page.totalCount, "totalCount"),
    totalPages: requireInteger(page.totalPages, "totalPages"),
  };
}

export function normalizePreview(value) {
  const preview = assertObject(value, "article preview");
  return {
    contentHtml:
      typeof preview.contentHtml === "string"
        ? preview.contentHtml
        : (() => {
            throw new Error("API returned invalid preview HTML.");
          })(),
  };
}

export function normalizePresign(value) {
  const result = assertObject(value, "media presign");
  return {
    resourceId: requireString(result.resourceId, "media resource id"),
    presignedUrl: normalizeHttpUrl(result.presignedUrl, "presigned URL"),
    objectName: requireString(result.objectName, "object name"),
  };
}

export function normalizeConfirmedMedia(value) {
  const media = assertObject(value, "media resource");
  if (media.module !== RESOURCE_MODULE || media.status !== RESOURCE_STATUS) {
    throw new Error("API returned an inactive or mismatched article picture.");
  }
  return {
    id: requireString(media.id, "media id"),
    originalName: requireString(media.originalName, "media original name"),
    contentType: requireString(media.contentType, "media content type"),
    size: requireInteger(media.size, "media size"),
    url: normalizeHttpUrl(media.url, "media URL"),
  };
}
