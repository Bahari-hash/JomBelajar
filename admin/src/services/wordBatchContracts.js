const UUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const EMPTY_UUID = "00000000-0000-0000-0000-000000000000";

export const WORD_BATCH_ERROR_CODES = Object.freeze([
  "WordBatchRequired",
  "WordBatchCountLimit",
  "WordBatchChildCountLimit",
  "WordBatchTextLengthLimit",
  "WordBatchAudioNotFound",
  "WordBatchAudioFailed",
  "WordBatchConflict",
  "WordHeadwordRequired",
  "WordHeadwordLengthLimit",
  "WordPartOfSpeechInvalid",
  "WordDefinitionRequired",
  "WordDefinitionLengthLimit",
  "WordUsageNoteLengthLimit",
  "WordSentenceRequired",
  "WordSentenceLengthLimit",
  "WordTranslationRequired",
  "WordTranslationLengthLimit",
  "WordSenseRequired",
  "WordChildCollectionInvalid",
  "WordChildCountLimit",
  "WordSortOrderInvalid",
  "WordSortOrderConflict",
  "WordDuplicate",
]);

function invalid(name) {
  throw new Error(`API returned invalid ${name}.`);
}

function object(value, name) {
  if (!value || typeof value !== "object" || Array.isArray(value))
    invalid(name);
  return value;
}

function array(value, normalize, name) {
  if (!Array.isArray(value)) invalid(name);
  return value.map(normalize);
}

function string(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (typeof value !== "string" || value.length === 0) invalid(name);
  return value;
}

function boolean(value, name) {
  if (typeof value !== "boolean") invalid(name);
  return value;
}

function integer(value, name) {
  if (!Number.isInteger(value) || value < 0) invalid(name);
  return value;
}

function positiveInteger(value, name) {
  const result = integer(value, name);
  if (result < 1) invalid(name);
  return result;
}

function uuid(value, name) {
  const result = string(value, name);
  if (!UUID_PATTERN.test(result) || result.toLowerCase() === EMPTY_UUID)
    invalid(name);
  return result;
}

function enumeration(value, allowed, name) {
  if (!allowed.includes(value)) invalid(name);
  return value;
}

function normalizeRow(value) {
  const source = object(value, "word batch row");
  return {
    rowNumber: positiveInteger(source.rowNumber, "word batch row number"),
    headword: string(source.headword, "word batch headword", true),
    normalizedHeadword: string(
      source.normalizedHeadword,
      "word batch normalized headword",
      true,
    ),
    wordAudioName: string(source.wordAudioName, "word batch audio name", true),
    senseCount: integer(source.senseCount, "word batch row sense count"),
    exampleCount: integer(source.exampleCount, "word batch row example count"),
    audioReferenceCount: integer(
      source.audioReferenceCount,
      "word batch row audio reference count",
    ),
    matchedAudioCount: integer(
      source.matchedAudioCount,
      "word batch row matched audio count",
    ),
  };
}

function normalizeError(value) {
  const source = object(value, "word batch error");
  return {
    rowNumber:
      source.rowNumber === null
        ? null
        : positiveInteger(source.rowNumber, "word batch error row number"),
    field: string(source.field, "word batch error field"),
    errorCode: enumeration(
      source.errorCode,
      WORD_BATCH_ERROR_CODES,
      "word batch error code",
    ),
    message: string(source.message, "word batch error message"),
  };
}

export function normalizeWordBatchValidation(value) {
  const source = object(value, "word batch validation");
  const summarySource = object(source.summary, "word batch summary");
  return {
    isValid: boolean(source.isValid, "word batch validation state"),
    summary: {
      wordCount: integer(summarySource.wordCount, "word batch word count"),
      senseCount: integer(summarySource.senseCount, "word batch sense count"),
      exampleCount: integer(
        summarySource.exampleCount,
        "word batch example count",
      ),
      wordAudioReferenceCount: integer(
        summarySource.wordAudioReferenceCount,
        "word batch word audio reference count",
      ),
      exampleAudioReferenceCount: integer(
        summarySource.exampleAudioReferenceCount,
        "word batch example audio reference count",
      ),
      matchedAudioReferenceCount: integer(
        summarySource.matchedAudioReferenceCount,
        "word batch matched audio reference count",
      ),
    },
    rows: array(source.rows, normalizeRow, "word batch rows"),
    errors: array(source.errors, normalizeError, "word batch errors"),
  };
}

export function normalizeWordBatchImport(value) {
  const source = object(value, "word batch import");
  const items = array(
    source.items,
    (item) => {
      const created = object(item, "word batch created item");
      return {
        rowNumber: positiveInteger(
          created.rowNumber,
          "word batch created row number",
        ),
        wordId: uuid(created.wordId, "word batch created word id"),
      };
    },
    "word batch created items",
  );
  const createdCount = integer(source.createdCount, "word batch created count");
  if (createdCount !== items.length) invalid("word batch created count");
  return { createdCount, items };
}
