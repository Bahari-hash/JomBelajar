export type WordStudyTodayState = "NotStarted" | "Active" | "Completed";
export type WordStudyResult = "Remembered" | "Forgotten";
export type WordStudySessionItemStatus =
  | "Pending"
  | "Remembered"
  | "Forgotten"
  | "Skipped";

export interface WordStudySettings {
  dailyWordStudyCount: number;
}

export interface WordStudySession {
  id: string;
  requestedCount: number;
  actualCount: number;
  includePreviouslyStudied: boolean;
  selectionMode: "Sequential" | "Random";
  languageTag: string | null;
  status: "Active" | "Completed" | "Abandoned";
  completedCount: number;
  rememberedCount: number;
  forgottenCount: number;
  skippedCount: number;
  startedAt: string;
  completedAt: string | null;
  abandonedAt: string | null;
}

export interface ExampleSentence {
  sentence: string;
  languageTag: string;
  translation: string;
  translationLanguageTag: string;
  audioClipId: string | null;
  sortOrder: number;
}

export interface WordSense {
  partOfSpeech: string;
  definition: string;
  definitionLanguageTag: string;
  usageNote: string | null;
  sortOrder: number;
  examples: ExampleSentence[];
}

export interface WordPronunciation {
  audioClipId: string;
  accentTag: string | null;
  ipa: string | null;
  isDefault: boolean;
  sortOrder: number;
}

export interface WordStudyNextItem {
  sessionId: string;
  itemId: string;
  position: number;
  actualCount: number;
  wordId: string;
  headword: string;
  languageTag: string;
  senses: WordSense[];
  pronunciations: WordPronunciation[];
}

export interface WordStudySessionItemContent {
  headword: string;
  senses: WordSense[];
  pronunciations: WordPronunciation[];
}

export interface WordStudySessionItem {
  itemId: string;
  wordId: string;
  position: number;
  status: WordStudySessionItemStatus;
  contentAvailable: boolean;
  content: WordStudySessionItemContent | null;
}

export interface WordStudyToday {
  studyDateUtc: string;
  dailyWordStudyCount: number;
  state: WordStudyTodayState;
  session: WordStudySession | null;
}

export interface AudioPlayback {
  url: string;
  expiresAt: string | null;
  durationSeconds: number;
  languageTag: string;
  audioClipKind: string;
}
