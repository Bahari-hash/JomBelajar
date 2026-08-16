import dayjs from "dayjs";
import { PARTS_OF_SPEECH, WORD_STATUSES } from "@/constants/wordStatus.js";

export const AUDIO_KINDS = Object.freeze([
  "WordPronunciation",
  "ExampleSentence",
]);

export const AUDIO_PROCESSING_STATUSES = Object.freeze([
  "Queued",
  "Processing",
  "Ready",
  "Failed",
]);
export const AUDIO_PUBLICATION_STATUSES = Object.freeze([
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
    (options.integer && !Number.isInteger(value)) ||
    (options.positive && value <= 0)
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
    translation: string(source.translation, "word example translation"),
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

export function normalizeAdminAudioClip(value) {
  const source = object(value, "audio details");
  return {
    id: uuid(source.id, "audio id"),
    sourceMediaResourceId: uuid(
      source.sourceMediaResourceId,
      "audio source media resource id",
    ),
    title: string(source.title, "audio title"),
    description: string(source.description, "audio description", true),
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
    sampleRate: number(source.sampleRate, "audio sample rate", {
      nullable: true,
      integer: true,
    }),
    channels: number(source.channels, "audio channels", {
      nullable: true,
      integer: true,
    }),
    containerFormat: string(
      source.containerFormat,
      "audio container format",
      true,
    ),
    sourceCodec: string(source.sourceCodec, "audio source codec", true),
    failureCode: string(source.failureCode, "audio failure code", true),
    publishedAt: date(source.publishedAt, "audio published at", true),
    createdAt: date(source.createdAt, "audio created at"),
    updatedAt: date(source.updatedAt, "audio updated at"),
  };
}

export function normalizeAudioUploadCapability(value) {
  const source = object(value, "Audio upload capability");
  if (source.module !== "Audio") invalid("Audio upload capability module");
  return {
    module: source.module,
    maxSizeBytes: number(source.maxSizeBytes, "Audio maximum upload size", {
      integer: true,
      positive: true,
    }),
    allowedTypes: array(
      source.allowedTypes,
      (item) => {
        const allowed = object(item, "Audio allowed media type");
        return {
          extension: string(allowed.extension, "Audio allowed extension"),
          contentTypes: array(
            allowed.contentTypes,
            (contentType) => string(contentType, "Audio allowed content type"),
            "Audio allowed content types",
          ),
        };
      },
      "Audio allowed types",
    ),
    multipartThresholdBytes: number(
      source.multipartThresholdBytes,
      "Audio multipart threshold",
      { integer: true, positive: true },
    ),
  };
}

export function normalizeAudioPresign(value) {
  const source = object(value, "Audio upload presign");
  return {
    resourceId: uuid(source.resourceId, "Audio media resource id"),
    presignedUrl: httpUrl(source.presignedUrl, "Audio presigned URL"),
  };
}

export function normalizeConfirmedAudioResource(value) {
  const source = object(value, "confirmed Audio media resource");
  if (source.module !== "Audio" || source.status !== "Active")
    invalid("active Audio media resource");
  return {
    id: uuid(source.id, "Audio media resource id"),
    originalName: string(source.originalName, "Audio original name"),
    size: number(source.size, "Audio media size", {
      integer: true,
      positive: true,
    }),
    extension: string(source.extension, "Audio media extension"),
    contentType: string(source.contentType, "Audio media content type"),
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
    audioClipKind: enumeration(
      source.audioClipKind,
      AUDIO_KINDS,
      "audio playback kind",
    ),
  };
}
