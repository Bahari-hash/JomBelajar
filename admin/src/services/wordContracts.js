import dayjs from "dayjs";
import { PARTS_OF_SPEECH } from "@/constants/wordOptions.js";
import { AUDIO_RESOURCE_STATUSES } from "@/services/audioContracts.js";

const UUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const EMPTY_UUID = "00000000-0000-0000-0000-000000000000";

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

function date(value, name) {
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

function example(value) {
  const source = object(value, "word example");
  return {
    id: uuid(source.id, "word example id"),
    sentence: string(source.sentence, "word example sentence"),
    translation: string(source.translation, "word example translation"),
    audio: audio(source.audio, "word example audio"),
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

function audio(value, name = "word audio") {
  if (value === null) return null;
  const source = object(value, name);
  return {
    id: uuid(source.id, `${name} id`),
    name: string(source.name, `${name} name`),
    status: enumeration(
      source.status,
      AUDIO_RESOURCE_STATUSES,
      `${name} status`,
    ),
    durationSeconds: number(source.durationSeconds, `${name} duration`, {
      nullable: true,
    }),
    lastFailureCode: string(
      source.lastFailureCode,
      `${name} failure code`,
      true,
    ),
  };
}

export function normalizeWordListItem(value) {
  const source = object(value, "word list item");
  return {
    id: uuid(source.id, "word id"),
    headword: string(source.headword, "word headword"),
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
    hasAudio: boolean(source.hasAudio, "word audio association"),
    createdAt: date(source.createdAt, "word created at"),
    updatedAt: date(source.updatedAt, "word updated at"),
    concurrencyStamp: uuid(source.concurrencyStamp, "word concurrency stamp"),
  };
}

export function normalizeAdminWord(value) {
  const source = object(value, "word details");
  return {
    id: uuid(source.id, "word id"),
    headword: string(source.headword, "word headword"),
    audio: audio(source.audio),
    concurrencyStamp: uuid(source.concurrencyStamp, "word concurrency stamp"),
    senses: array(source.senses, sense, "word senses"),
    createdAt: date(source.createdAt, "word created at"),
    updatedAt: date(source.updatedAt, "word updated at"),
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
