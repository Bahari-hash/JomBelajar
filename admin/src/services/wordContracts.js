import dayjs from "dayjs";
import { PARTS_OF_SPEECH, WORD_STATUSES } from "@/constants/wordStatus.js";

export const AUDIO_KINDS = Object.freeze([
  "WordPronunciation",
  "ExampleSentence",
]);

const AUDIO_PROCESSING_STATUSES = Object.freeze([
  "Queued",
  "Processing",
  "Ready",
  "Failed",
]);
const AUDIO_PUBLICATION_STATUSES = Object.freeze([
  "Draft",
  "Published",
  "Unpublished",
]);
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
  if (typeof value !== "string" || value.length === 0) invalid(name);
  return value;
}

function uuid(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
  if (!UUID_PATTERN.test(result) || result.toLowerCase() === EMPTY_UUID)
    invalid(name);
  return result;
}

function number(value, name, options = {}) {
  if (options.nullable && value === null) return null;
  if (
    !Number.isFinite(value) ||
    value < 0 ||
    (options.integer && !Number.isInteger(value))
  )
    invalid(name);
  return value;
}

function boolean(value, name) {
  if (typeof value !== "boolean") invalid(name);
  return value;
}

function date(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
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

function auditUser(value) {
  const source = object(value, "word audit user");
  return {
    id: uuid(source.id, "word audit user id"),
    nickname: string(source.nickname, "word audit user nickname", true),
    avatarUrl: httpUrl(source.avatarUrl, "word audit avatar URL", true),
  };
}

function example(value) {
  const source = object(value, "word example");
  return {
    id: uuid(source.id, "word example id"),
    sentence: string(source.sentence, "word example sentence"),
    languageTag: string(source.languageTag, "word example language"),
    translation: string(source.translation, "word example translation"),
    translationLanguageTag: string(
      source.translationLanguageTag,
      "word example translation language",
    ),
    audioClipId: uuid(source.audioClipId, "word example audio id", true),
    sortOrder: number(source.sortOrder, "word example sort order", {
      integer: true,
    }),
  };
}

function sense(value) {
  const source = object(value, "word sense");
  return {
    id: uuid(source.id, "word sense id"),
    partOfSpeech: enumeration(
      source.partOfSpeech,
      PARTS_OF_SPEECH,
      "word part of speech",
    ),
    definition: string(source.definition, "word definition"),
    definitionLanguageTag: string(
      source.definitionLanguageTag,
      "word definition language",
    ),
    usageNote: string(source.usageNote, "word usage note", true),
    sortOrder: number(source.sortOrder, "word sense sort order", {
      integer: true,
    }),
    examples: array(source.examples, example, "word examples"),
  };
}

function pronunciation(value) {
  const source = object(value, "word pronunciation");
  return {
    id: uuid(source.id, "word pronunciation id"),
    audioClipId: uuid(source.audioClipId, "word pronunciation audio id"),
    accentTag: string(source.accentTag, "word accent tag", true),
    ipa: string(source.ipa, "word IPA", true),
    isDefault: boolean(source.isDefault, "word default pronunciation"),
    sortOrder: number(source.sortOrder, "word pronunciation sort order", {
      integer: true,
    }),
  };
}

export function normalizeWordListItem(value) {
  const source = object(value, "word list item");
  return {
    id: uuid(source.id, "word id"),
    languageTag: string(source.languageTag, "word language"),
    headword: string(source.headword, "word headword"),
    status: enumeration(source.status, WORD_STATUSES, "word status"),
    primaryPartOfSpeech:
      source.primaryPartOfSpeech === null
        ? null
        : enumeration(
            source.primaryPartOfSpeech,
            PARTS_OF_SPEECH,
            "word primary part of speech",
          ),
    primaryDefinition: string(
      source.primaryDefinition,
      "word primary definition",
      true,
    ),
    senseCount: number(source.senseCount, "word sense count", {
      integer: true,
    }),
    exampleCount: number(source.exampleCount, "word example count", {
      integer: true,
    }),
    pronunciationCount: number(
      source.pronunciationCount,
      "word pronunciation count",
      { integer: true },
    ),
    createdBy: auditUser(source.createdBy),
    lastEditor: auditUser(source.lastEditor),
    publishedAt: date(source.publishedAt, "word published at", true),
    archivedAt: date(source.archivedAt, "word archived at", true),
    createdAt: date(source.createdAt, "word created at"),
    updatedAt: date(source.updatedAt, "word updated at"),
    concurrencyStamp: uuid(source.concurrencyStamp, "word concurrency stamp"),
  };
}

export function normalizeAdminWord(value) {
  const source = object(value, "word details");
  return {
    ...normalizeWordListItem({
      ...source,
      primaryPartOfSpeech: null,
      primaryDefinition: null,
      senseCount: Array.isArray(source.senses) ? source.senses.length : -1,
      exampleCount: Array.isArray(source.senses)
        ? source.senses.reduce(
            (count, item) =>
              count +
              (Array.isArray(item?.examples) ? item.examples.length : 0),
            0,
          )
        : -1,
      pronunciationCount: Array.isArray(source.pronunciations)
        ? source.pronunciations.length
        : -1,
    }),
    senses: array(source.senses, sense, "word senses"),
    pronunciations: array(
      source.pronunciations,
      pronunciation,
      "word pronunciations",
    ),
  };
}

function page(value, normalize, name) {
  const source = object(value, `${name} pagination`);
  return {
    items: array(source.items, normalize, `${name} pagination items`),
    page: number(source.page, `${name} page`, { integer: true }),
    pageSize: number(source.pageSize, `${name} page size`, { integer: true }),
    totalCount: number(source.totalCount, `${name} total count`, {
      integer: true,
    }),
    totalPages: number(source.totalPages, `${name} total pages`, {
      integer: true,
    }),
  };
}

export function normalizeWordPage(value) {
  return page(value, normalizeWordListItem, "word");
}

function audioOption(value) {
  const source = object(value, "audio option");
  return {
    id: uuid(source.id, "audio id"),
    title: string(source.title, "audio title"),
    languageTag: string(source.languageTag, "audio language"),
    kind: enumeration(source.kind, AUDIO_KINDS, "audio kind"),
    processingStatus: enumeration(
      source.processingStatus,
      AUDIO_PROCESSING_STATUSES,
      "audio processing status",
    ),
    publicationStatus: enumeration(
      source.publicationStatus,
      AUDIO_PUBLICATION_STATUSES,
      "audio publication status",
    ),
    durationSeconds: number(source.durationSeconds, "audio duration", {
      nullable: true,
    }),
    failureCode: string(source.failureCode, "audio failure code", true),
    updatedAt: date(source.updatedAt, "audio updated at"),
  };
}

export function normalizeAudioOptionPage(value) {
  return page(value, audioOption, "audio");
}

function httpUrl(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
  try {
    if (!HTTP_PROTOCOLS.has(new URL(result).protocol)) invalid(name);
  } catch {
    invalid(name);
  }
  return result;
}

export function normalizeAudioPlayback(value) {
  const source = object(value, "audio playback");
  return {
    url: httpUrl(source.url, "audio playback URL"),
    expiresAt: date(source.expiresAt, "audio playback expiry", true),
    durationSeconds: number(source.durationSeconds, "audio playback duration"),
    languageTag: string(source.languageTag, "audio playback language"),
    audioClipKind: enumeration(
      source.audioClipKind,
      AUDIO_KINDS,
      "audio playback kind",
    ),
  };
}

function fieldError(value) {
  const source = object(value, "batch word field error");
  return {
    field: string(source.field, "batch word error field"),
    errorCodes: array(
      source.errorCodes,
      (item) => string(item, "batch word error code"),
      "batch word error codes",
    ),
    messages: array(
      source.messages,
      (item) => string(item, "batch word error message"),
      "batch word error messages",
    ),
  };
}

export function normalizeBatchValidation(value) {
  const source = object(value, "batch word validation");
  return {
    isValid: boolean(source.isValid, "batch word validation state"),
    errors: array(source.errors, fieldError, "batch word errors"),
    rows: array(
      source.rows,
      (item) => {
        const row = object(item, "batch word row validation");
        return {
          rowIndex: number(row.rowIndex, "batch word row index", {
            integer: true,
          }),
          normalized:
            row.normalized === null
              ? null
              : normalizeWordInputPreview(row.normalized),
          errors: array(row.errors, fieldError, "batch word row errors"),
        };
      },
      "batch word rows",
    ),
  };
}

function normalizeWordInputPreview(value) {
  const source = object(value, "batch normalized word");
  return {
    languageTag: string(source.languageTag, "batch word language"),
    headword: string(source.headword, "batch word headword"),
    senses: array(source.senses, normalizePreviewSense, "batch word senses"),
    pronunciations: array(
      source.pronunciations,
      normalizePreviewPronunciation,
      "batch word pronunciations",
    ),
  };
}

function normalizePreviewSense(value) {
  const source = object(value, "batch word sense");
  return {
    partOfSpeech: enumeration(
      source.partOfSpeech,
      PARTS_OF_SPEECH,
      "batch word part of speech",
    ),
    definition: string(source.definition, "batch word definition"),
    definitionLanguageTag: string(
      source.definitionLanguageTag,
      "batch word definition language",
    ),
    usageNote: string(source.usageNote, "batch word usage note", true),
    sortOrder: number(source.sortOrder, "batch word sense sort order", {
      integer: true,
    }),
    examples: array(
      source.examples,
      normalizePreviewExample,
      "batch word examples",
    ),
  };
}

function normalizePreviewExample(value) {
  const source = object(value, "batch word example");
  return {
    sentence: string(source.sentence, "batch word example sentence"),
    languageTag: string(source.languageTag, "batch word example language"),
    translation: string(source.translation, "batch word example translation"),
    translationLanguageTag: string(
      source.translationLanguageTag,
      "batch word example translation language",
    ),
    audioClipId: uuid(source.audioClipId, "batch word example audio id", true),
    sortOrder: number(source.sortOrder, "batch word example sort order", {
      integer: true,
    }),
  };
}

function normalizePreviewPronunciation(value) {
  const source = object(value, "batch word pronunciation");
  return {
    audioClipId: uuid(source.audioClipId, "batch word pronunciation audio id"),
    accentTag: string(source.accentTag, "batch word accent tag", true),
    ipa: string(source.ipa, "batch word IPA", true),
    isDefault: boolean(source.isDefault, "batch word default pronunciation"),
    sortOrder: number(source.sortOrder, "batch word pronunciation sort order", {
      integer: true,
    }),
  };
}

export function normalizeBatchImport(value) {
  const source = object(value, "batch word import");
  return {
    batchId: uuid(source.batchId, "batch word id"),
    createdCount: number(source.createdCount, "batch created count", {
      integer: true,
    }),
    items: array(
      source.items,
      (item) => {
        const created = object(item, "batch created word");
        return {
          rowIndex: number(created.rowIndex, "batch created row index", {
            integer: true,
          }),
          wordId: uuid(created.wordId, "batch created word id"),
        };
      },
      "batch created words",
    ),
  };
}
