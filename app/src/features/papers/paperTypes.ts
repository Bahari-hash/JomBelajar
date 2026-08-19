export type PaperQuestionType =
  "SingleChoice" | "TrueFalse" | "FillBlank" | "Dictation";
export type PaperAttemptStatus = "InProgress" | "Submitted";
export type AnswerSaveStatus = "idle" | "saving" | "saved" | "error";

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PaperCatalogItem {
  id: string;
  title: string;
  description: string | null;
  categories: PaperCategorySummary[];
  questionCount: number;
  totalScore: number;
  passingScore: number;
  publishedAt: string;
}

export interface PaperCategorySummary {
  id: string;
  name: string;
  slug: string;
}

export interface PaperListQuery {
  page: number;
  pageSize: number;
  keyword?: string;
  categoryId?: string;
}

export interface PaperDetails extends PaperCatalogItem {
  instructions: string | null;
}

export interface PaperAttemptOption {
  id: string;
  text: string;
  sortOrder: number;
}

export interface PaperAttemptSavedAnswer {
  selectedOptionId: string | null;
  booleanAnswer: boolean | null;
  textAnswer: string | null;
  textAnswers?: string[] | null;
  savedAt: string | null;
}

export interface PaperAttemptQuestion {
  id: string;
  type: PaperQuestionType;
  prompt: string;
  points: number;
  sortOrder: number;
  options: PaperAttemptOption[];
  savedAnswer: PaperAttemptSavedAnswer | null;
  audioResourceId?: string | null;
  dictationBlanks?: { sortOrder: number }[];
}

export interface PaperAttempt {
  id: string;
  paperId: string;
  attemptNumber: number;
  status: PaperAttemptStatus;
  title: string;
  description: string | null;
  instructions: string | null;
  questionCount: number;
  paperTotalScore: number;
  paperPassingScore: number;
  startedAt: string;
  submittedAt: string | null;
  questions: PaperAttemptQuestion[];
}

export interface SavePaperAnswerRequest {
  selectedOptionId?: string | null;
  booleanAnswer?: boolean | null;
  textAnswer?: string | null;
  textAnswers?: string[] | null;
}

export interface PaperQuestionResult {
  questionId: string;
  type: PaperQuestionType;
  prompt: string;
  explanation: string | null;
  points: number;
  sortOrder: number;
  options: PaperAttemptOption[];
  selectedOptionId: string | null;
  booleanAnswer: boolean | null;
  textAnswer: string | null;
  textAnswers?: string[] | null;
  isAnswered: boolean;
  correctOptionId: string | null;
  correctBoolean: boolean | null;
  acceptedAnswers: string[];
  isCorrect: boolean;
  awardedPoints: number;
  audioResourceId?: string | null;
  dictationAnswers?: string[] | null;
}

export interface PaperAttemptResult {
  id: string;
  paperId: string;
  attemptNumber: number;
  paperTitle: string;
  score: number;
  paperTotalScore: number;
  paperPassingScore: number;
  isPassed: boolean;
  startedAt: string;
  submittedAt: string;
  questions: PaperQuestionResult[];
}

export type PaperAnswerValue = string | boolean | string[] | null;

export interface LocalPaperAnswer {
  value: PaperAnswerValue;
  savedValue: PaperAnswerValue;
  status: AnswerSaveStatus;
  revision: number;
  errorMessage: string | null;
}
