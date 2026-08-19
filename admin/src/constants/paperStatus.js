export const PAPER_STATUSES = Object.freeze([
  "Draft",
  "Published",
  "Unpublished",
  "Archived",
]);

export const PAPER_STATUS_OPTIONS = Object.freeze([
  { value: "Draft", label: "草稿" },
  { value: "Published", label: "已发布" },
  { value: "Unpublished", label: "已下架" },
  { value: "Archived", label: "已归档" },
]);

export const PAPER_QUESTION_TYPES = Object.freeze([
  "SingleChoice",
  "TrueFalse",
  "FillBlank",
  "Dictation",
]);

export const PAPER_QUESTION_TYPE_OPTIONS = Object.freeze([
  { value: "SingleChoice", label: "单选题" },
  { value: "TrueFalse", label: "判断题" },
  { value: "FillBlank", label: "填空题" },
  { value: "Dictation", label: "听写题" },
]);

export function getPaperStatusLabel(value) {
  return (
    PAPER_STATUS_OPTIONS.find((option) => option.value === value)?.label ??
    value
  );
}

export function getPaperQuestionTypeLabel(value) {
  return (
    PAPER_QUESTION_TYPE_OPTIONS.find((option) => option.value === value)
      ?.label ?? value
  );
}

export function getPaperActions(paper) {
  if (paper.status === "Archived") return [];
  if (paper.status === "Published") return ["unpublish"];
  if (paper.attemptCount > 0) return ["publish", "archive"];
  return ["publish", "archive", "delete"];
}

export function isPaperEditable(paper) {
  return (
    (paper.status === "Draft" || paper.status === "Unpublished") &&
    paper.attemptCount === 0
  );
}
