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
function integer(value, name) {
  if (!Number.isInteger(value) || value < 0) invalid(name);
  return value;
}
function string(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (typeof value !== "string") invalid(name);
  return value;
}

function normalizeError(value) {
  const source = object(value, "paper batch error");
  return {
    paperIndex:
      source.paperIndex === null
        ? null
        : integer(source.paperIndex, "paper index"),
    questionIndex:
      source.questionIndex === null
        ? null
        : integer(source.questionIndex, "question index"),
    field: string(source.field, "paper batch field"),
    errorCode: string(source.errorCode, "paper batch error code"),
    message: string(source.message, "paper batch message"),
  };
}

export function normalizePaperBatchValidation(value) {
  const source = object(value, "paper batch validation");
  const summary = object(source.summary, "paper batch summary");
  if (typeof source.isValid !== "boolean")
    invalid("paper batch validation state");
  return {
    isValid: source.isValid,
    summary: {
      paperCount: integer(summary.paperCount, "paper count"),
      questionCount: integer(summary.questionCount, "question count"),
      dictationQuestionCount: integer(
        summary.dictationQuestionCount,
        "dictation count",
      ),
      dictationBlankCount: integer(
        summary.dictationBlankCount,
        "dictation blank count",
      ),
      categoryReferenceCount: integer(
        summary.categoryReferenceCount,
        "category references",
      ),
      matchedCategoryReferenceCount: integer(
        summary.matchedCategoryReferenceCount,
        "matched categories",
      ),
      audioReferenceCount: integer(
        summary.audioReferenceCount,
        "audio references",
      ),
      matchedAudioReferenceCount: integer(
        summary.matchedAudioReferenceCount,
        "matched audio",
      ),
    },
    papers: array(
      source.papers,
      (item) => {
        const paper = object(item, "paper batch preview");
        if (typeof paper.isValid !== "boolean")
          invalid("paper validation state");
        return {
          paperIndex: integer(paper.paperIndex, "paper index"),
          title: string(paper.title, "paper title", true),
          isValid: paper.isValid,
          questionCount: integer(paper.questionCount, "question count"),
          categoryNames: array(
            paper.categoryNames,
            (name) => string(name, "category name"),
            "category names",
          ),
          matchedCategoryCount: integer(
            paper.matchedCategoryCount,
            "matched category count",
          ),
          audioFileNames: array(
            paper.audioFileNames,
            (name) => string(name, "audio name"),
            "audio names",
          ),
          matchedAudioCount: integer(
            paper.matchedAudioCount,
            "matched audio count",
          ),
        };
      },
      "paper batch papers",
    ),
    errors: array(source.errors, normalizeError, "paper batch errors"),
  };
}

export function normalizePaperBatchImport(value) {
  const source = object(value, "paper batch import");
  const items = array(
    source.items,
    (item) => {
      const created = object(item, "created paper");
      return {
        paperIndex: integer(created.paperIndex, "paper index"),
        paperId: string(created.paperId, "paper id"),
      };
    },
    "created papers",
  );
  const importedCount = integer(source.importedCount, "imported count");
  if (importedCount !== items.length) invalid("imported count");
  return { importedCount, items };
}
