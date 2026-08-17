export const WORD_STATUSES = Object.freeze([
  "Draft",
  "Published",
  "Unpublished",
  "Archived",
]);

export const WORD_STATUS_OPTIONS = Object.freeze([
  { value: "Draft", label: "草稿" },
  { value: "Published", label: "已发布" },
  { value: "Unpublished", label: "已下架" },
  { value: "Archived", label: "已归档" },
]);

export {
  PARTS_OF_SPEECH,
  PART_OF_SPEECH_OPTIONS,
  getPartOfSpeechLabel,
} from "@/constants/wordOptions.js";

export function getWordStatusLabel(value) {
  return (
    WORD_STATUS_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

export function getWordActions(word) {
  if (word.status === "Published") return ["unpublish"];
  if (word.status === "Archived") return [];
  return ["publish", "archive", "delete"];
}
