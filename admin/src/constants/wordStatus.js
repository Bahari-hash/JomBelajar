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

export const PARTS_OF_SPEECH = Object.freeze([
  "Noun",
  "Verb",
  "Adjective",
  "Adverb",
  "Pronoun",
  "Determiner",
  "Preposition",
  "Conjunction",
  "Interjection",
  "Numeral",
  "Particle",
  "Other",
]);

export const PART_OF_SPEECH_OPTIONS = Object.freeze([
  { value: "Noun", label: "名词" },
  { value: "Verb", label: "动词" },
  { value: "Adjective", label: "形容词" },
  { value: "Adverb", label: "副词" },
  { value: "Pronoun", label: "代词" },
  { value: "Determiner", label: "限定词" },
  { value: "Preposition", label: "介词" },
  { value: "Conjunction", label: "连词" },
  { value: "Interjection", label: "感叹词" },
  { value: "Numeral", label: "数词" },
  { value: "Particle", label: "小品词" },
  { value: "Other", label: "其他" },
]);

export function getWordStatusLabel(value) {
  return (
    WORD_STATUS_OPTIONS.find((option) => option.value === value)?.label ?? value
  );
}

export function getPartOfSpeechLabel(value) {
  return (
    PART_OF_SPEECH_OPTIONS.find((option) => option.value === value)?.label ??
    value
  );
}

export function getWordActions(word) {
  if (word.status === "Published") return ["unpublish"];
  if (word.status === "Archived") return [];
  return ["publish", "archive", "delete"];
}
