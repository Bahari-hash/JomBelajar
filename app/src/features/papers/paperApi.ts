import { createApi } from "@reduxjs/toolkit/query/react";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import type {
  PaperAttempt,
  PaperAttemptResult,
  PaperCatalogItem,
  PaperDetails,
  PaperListQuery,
  PagedResponse,
  SavePaperAnswerRequest,
} from "@/features/papers/paperTypes";

export interface SaveAnswerArgs {
  attemptId: string;
  questionId: string;
  answer: SavePaperAnswerRequest;
}

export interface AnswerIdentity {
  attemptId: string;
  questionId: string;
}

export const paperApi = createApi({
  reducerPath: "paperApi",
  baseQuery: axiosBaseQuery(),
  tagTypes: ["PaperCatalog", "Paper", "PaperAttempt", "PaperAttemptResult"],
  endpoints: (builder) => ({
    getPapers: builder.query<PagedResponse<PaperCatalogItem>, PaperListQuery>({
      query: (params) => ({ url: "/papers", params }),
      providesTags: ["PaperCatalog"],
    }),
    getPaper: builder.query<PaperDetails, string>({
      query: (paperId) => ({ url: `/papers/${paperId}` }),
      providesTags: (_result, _error, paperId) => [
        { type: "Paper", id: paperId },
      ],
    }),
    startAttempt: builder.mutation<PaperAttempt, string>({
      query: (paperId) => ({
        url: `/papers/${paperId}/attempts`,
        method: "POST",
      }),
    }),
    getAttempt: builder.query<PaperAttempt, string>({
      query: (attemptId) => ({ url: `/paper-attempts/${attemptId}` }),
      providesTags: (_result, _error, attemptId) => [
        { type: "PaperAttempt", id: attemptId },
      ],
    }),
    saveAnswer: builder.mutation<void, SaveAnswerArgs>({
      query: ({ attemptId, questionId, answer }) => ({
        url: `/paper-attempts/${attemptId}/answers/${questionId}`,
        method: "PUT",
        data: answer,
      }),
      async onQueryStarted(
        { attemptId, questionId, answer },
        { dispatch, queryFulfilled },
      ) {
        await queryFulfilled;
        dispatch(
          paperApi.util.updateQueryData("getAttempt", attemptId, (draft) => {
            const question = draft.questions.find(
              (item) => item.id === questionId,
            );
            if (question) {
              question.savedAnswer = {
                selectedOptionId: answer.selectedOptionId ?? null,
                booleanAnswer: answer.booleanAnswer ?? null,
                textAnswer: answer.textAnswer ?? null,
                savedAt: new Date().toISOString(),
              };
            }
          }),
        );
      },
    }),
    clearAnswer: builder.mutation<void, AnswerIdentity>({
      query: ({ attemptId, questionId }) => ({
        url: `/paper-attempts/${attemptId}/answers/${questionId}`,
        method: "DELETE",
      }),
      async onQueryStarted(
        { attemptId, questionId },
        { dispatch, queryFulfilled },
      ) {
        await queryFulfilled;
        dispatch(
          paperApi.util.updateQueryData("getAttempt", attemptId, (draft) => {
            const question = draft.questions.find(
              (item) => item.id === questionId,
            );
            if (question) question.savedAnswer = null;
          }),
        );
      },
    }),
    submitAttempt: builder.mutation<PaperAttemptResult, string>({
      query: (attemptId) => ({
        url: `/paper-attempts/${attemptId}/submit`,
        method: "POST",
      }),
      async onQueryStarted(attemptId, { dispatch, queryFulfilled }) {
        try {
          const { data } = await queryFulfilled;
          await dispatch(
            paperApi.util.upsertQueryData("getAttemptResult", attemptId, data),
          ).unwrap();
        } catch {
          // The mutation state exposes submit failures to the page.
        }
      },
      invalidatesTags: (_result, _error, attemptId) => [
        { type: "PaperAttempt", id: attemptId },
      ],
    }),
    getAttemptResult: builder.query<PaperAttemptResult, string>({
      query: (attemptId) => ({ url: `/paper-attempts/${attemptId}/result` }),
      providesTags: (_result, _error, attemptId) => [
        { type: "PaperAttemptResult", id: attemptId },
      ],
    }),
  }),
});

export const {
  useGetPapersQuery,
  useGetPaperQuery,
  useStartAttemptMutation,
  useGetAttemptQuery,
  useSaveAnswerMutation,
  useClearAnswerMutation,
  useSubmitAttemptMutation,
  useGetAttemptResultQuery,
  useLazyGetAttemptResultQuery,
} = paperApi;
