import { createApi } from "@reduxjs/toolkit/query/react";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import type {
  PagedResponse,
  PaperQuestionType,
} from "@/features/papers/paperTypes";

export interface WrongQuestionItem {
  id: string;
  questionId: string;
  paperId: string;
  paperTitle: string;
  status: "Pending" | "Mastered";
  type: PaperQuestionType;
  prompt: string;
  audioResourceId: string | null;
  wrongCount: number;
  redoCount: number;
  firstWrongAt: string;
  lastWrongAt: string;
  lastRedoAt: string | null;
  masteredAt: string | null;
  concurrencyStamp: string;
}
export interface WrongQuestionDetail extends WrongQuestionItem {
  points: number;
  options: { id: string; text: string; sortOrder: number }[];
  dictationBlanks: { sortOrder: number }[];
}
export interface WrongQuestionRedo {
  id: string;
  questionId: string;
  status: "Pending" | "Mastered";
  type: PaperQuestionType;
  isCorrect: boolean;
  explanation: string | null;
  selectedOptionId: string | null;
  booleanAnswer: boolean | null;
  textAnswer: string | null;
  textAnswers: string[] | null;
  correctOptionId: string | null;
  correctBoolean: boolean | null;
  acceptedAnswers: string[];
  dictationAnswers: string[];
  wrongCount: number;
  redoCount: number;
  lastWrongAt: string;
  lastRedoAt: string;
  masteredAt: string | null;
  concurrencyStamp: string;
}

export const wrongQuestionApi = createApi({
  reducerPath: "wrongQuestionApi",
  baseQuery: axiosBaseQuery(),
  tagTypes: ["WrongQuestion"],
  endpoints: (builder) => ({
    getWrongQuestions: builder.query<
      PagedResponse<WrongQuestionItem>,
      {
        page: number;
        pageSize: number;
        status?: "Pending" | "Mastered";
        keyword?: string;
      }
    >({
      query: (params) => ({ url: "/paper-wrong-questions", params }),
      providesTags: ["WrongQuestion"],
    }),
    getWrongQuestion: builder.query<WrongQuestionDetail, string>({
      query: (id) => ({ url: `/paper-wrong-questions/${id}` }),
    }),
    redoWrongQuestion: builder.mutation<
      WrongQuestionRedo,
      { id: string; answer: Record<string, unknown> }
    >({
      query: ({ id, answer }) => ({
        url: `/paper-wrong-questions/${id}/redo`,
        method: "POST",
        data: answer,
      }),
      invalidatesTags: ["WrongQuestion"],
    }),
  }),
});

export const {
  useGetWrongQuestionsQuery,
  useGetWrongQuestionQuery,
  useRedoWrongQuestionMutation,
} = wrongQuestionApi;
