export type WordMemorizationResult = "Remembered" | "Forgotten";

export interface WordStudySettings {
  dailyWordStudyCount: number;
  dailyWordReviewCount: number;
}

export type WordStudyPhase = "Memorization" | "Spelling";
export type WordStudySessionType = "Learning" | "Review";
export type WordSpellingResult = "Correct" | "Incorrect";

export interface WordSpellingSense {
  partOfSpeech: string;
  definition: string;
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

export type WordStudyActivityType = "Learning" | "Review";

export interface WordStudyTodayReviewItem {
  wordId: string;
  headword: string;
  activityType: WordStudyActivityType;
  completedAtUtc: string;
  senses: WordSense[];
  audioResourceId: string | null;
  isFavorite: boolean;
}

export interface WordStudyTodayReview {
  studyDateUtc: string;
  items: WordStudyTodayReviewItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface WordStudyCheckInDate {
  studyDateUtc: string;
  checkedInAtUtc: string;
}

export interface WordStudyCheckInCalendar {
  year: number;
  month: number;
  checkedInDates: WordStudyCheckInDate[];
  currentStreak: number;
  longestStreak: number;
  totalCheckInDays: number;
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
