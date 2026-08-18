export type WordStudyTodayState = "NotStarted" | "Active" | "Completed";
export type WordStudyResult = "Remembered" | "Forgotten";
export type WordMemorizationResult = WordStudyResult;
export type WordStudySessionItemStatus =
  "Pending" | "Remembered" | "Forgotten" | "Skipped";

export interface WordStudySettings {
  dailyWordStudyCount: number;
  dailyWordReviewCount?: number;
}

export type WordStudyPhase = "Memorization" | "Spelling";
export type WordStudySessionType = "Learning" | "Review";
export type WordSpellingResult = "Correct" | "Incorrect";

export interface WordSpellingSense {
  partOfSpeech: string;
  definition: string;
  usageNote: string | null;
  sortOrder: number;
}

export interface WordMemorizationContent {
  headword: string;
  senses: WordSense[];
  audioResourceId: string | null;
}

export interface WordSpellingPrompt {
  senses: WordSpellingSense[];
}

export type WordStudyCurrentItem =
  | {
      phase: "Memorization";
      itemId: string;
      wordId: string;
      itemConcurrencyStamp: string;
      isFavorite: boolean;
      memorization: WordMemorizationContent;
      spelling: null;
    }
  | {
      phase: "Spelling";
      itemId: string;
      wordId: string;
      itemConcurrencyStamp: string;
      isFavorite: boolean;
      memorization: null;
      spelling: WordSpellingPrompt;
    };

export interface WordStudySessionState {
  id: string;
  sessionType: WordStudySessionType;
  phase: WordStudyPhase;
  status: "Active" | "Completed";
  actualCount: number;
  completedCount: number;
  memorizationPassedCount: number;
  spellingPassedCount: number;
  excludedCount: number;
  skippedCount: number;
  startedAt: string;
  completedAt: string | null;
  currentItem: WordStudyCurrentItem | null;
}

export interface WordStudyCommandResponse {
  spellingResult: WordSpellingResult | null;
  session: WordStudySessionState;
}

export interface WordLearningOverview {
  totalLearnedCount: number;
  todayLearnedCount: number;
  hasMoreWords: boolean;
  activeSession: WordStudySessionState | null;
}

export interface WordReviewOverview {
  dueCount: number;
  overdueCount: number;
  activeSession: WordStudySessionState | null;
}

export interface PagedWordLibrary<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface WordFavorite {
  wordId: string;
  headword: string;
  senses: WordSense[];
  audioResourceId: string | null;
  createdAt: string;
}

export interface WordReviewExclusion extends Omit<WordFavorite, "createdAt"> {
  excludedAt: string;
}

export interface WordStudySession {
  id: string;
  requestedCount: number;
  actualCount: number;
  includePreviouslyStudied: boolean;
  selectionMode: "Sequential" | "Random";
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
  translation: string;
  sortOrder: number;
  audioResourceId: string | null;
}

export interface WordSense {
  partOfSpeech: string;
  definition: string;
  usageNote: string | null;
  sortOrder: number;
  examples: ExampleSentence[];
}

export interface WordStudyNextItem {
  sessionId: string;
  itemId: string;
  position: number;
  actualCount: number;
  wordId: string;
  headword: string;
  senses: WordSense[];
  audioResourceId: string | null;
}

export interface WordStudySessionItemContent {
  headword: string;
  senses: WordSense[];
  audioResourceId: string | null;
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
