import { createApi } from "@reduxjs/toolkit/query/react";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import type {
  WordStudyNextItem,
  WordStudyResult,
  WordStudySession,
  WordStudySessionItem,
  WordStudySettings,
  WordStudyToday,
  WordLearningOverview,
  WordReviewOverview,
  WordStudyCommandResponse,
  WordStudySessionState,
  PagedWordLibrary,
  WordFavorite,
  WordReviewExclusion,
} from "@/features/wordStudy/wordStudyTypes";

/** Provides authenticated daily word-study queries and mutations through Axios. */
export const wordStudyApi = createApi({
  reducerPath: "wordStudyApi",
  baseQuery: axiosBaseQuery(),
  tagTypes: [
    "WordStudySettings",
    "WordStudyToday",
    "WordStudySession",
    "WordStudySessionItems",
    "LearningOverview",
    "ReviewOverview",
    "WordFavorites",
    "WordReviewExclusions",
  ],
  endpoints: (builder) => ({
    getLearningOverview: builder.query<WordLearningOverview, void>({
      query: () => ({ url: "/word-study/learning/overview" }),
      providesTags: ["LearningOverview"],
    }),
    startLearning: builder.mutation<WordStudySessionState, void>({
      query: () => ({ url: "/word-study/learning/sessions", method: "POST" }),
      invalidatesTags: ["LearningOverview"],
    }),
    getLearningSession: builder.query<WordStudySessionState, string>({
      query: (id) => ({ url: `/word-study/learning/sessions/${id}` }),
      providesTags: (_r, _e, id) => [{ type: "WordStudySession", id }],
    }),
    submitLearningMemorization: builder.mutation<WordStudyCommandResponse, { sessionId: string; itemId: string; result: WordStudyResult; itemConcurrencyStamp: string }>({
      query: ({ sessionId, itemId, ...data }) => ({ url: `/word-study/learning/sessions/${sessionId}/items/${itemId}/memorization`, method: "POST", data }),
      invalidatesTags: ["LearningOverview", "WordStudySession"],
    }),
    submitLearningSpelling: builder.mutation<WordStudyCommandResponse, { sessionId: string; itemId: string; answer: string; itemConcurrencyStamp: string }>({
      query: ({ sessionId, itemId, ...data }) => ({ url: `/word-study/learning/sessions/${sessionId}/items/${itemId}/spelling`, method: "POST", data }),
      invalidatesTags: ["LearningOverview", "WordStudySession"],
    }),
    getReviewOverview: builder.query<WordReviewOverview, void>({
      query: () => ({ url: "/word-study/review/overview" }),
      providesTags: ["ReviewOverview"],
    }),
    startReview: builder.mutation<WordStudySessionState, void>({
      query: () => ({ url: "/word-study/review/sessions", method: "POST" }),
      invalidatesTags: ["ReviewOverview"],
    }),
    getReviewSession: builder.query<WordStudySessionState, string>({
      query: (id) => ({ url: `/word-study/review/sessions/${id}` }),
      providesTags: (_r, _e, id) => [{ type: "WordStudySession", id }],
    }),
    submitReviewMemorization: builder.mutation<WordStudyCommandResponse, { sessionId: string; itemId: string; result: WordStudyResult; itemConcurrencyStamp: string }>({
      query: ({ sessionId, itemId, ...data }) => ({ url: `/word-study/review/sessions/${sessionId}/items/${itemId}/memorization`, method: "POST", data }),
      invalidatesTags: ["ReviewOverview", "WordStudySession"],
    }),
    submitReviewSpelling: builder.mutation<WordStudyCommandResponse, { sessionId: string; itemId: string; answer: string; itemConcurrencyStamp: string }>({
      query: ({ sessionId, itemId, ...data }) => ({ url: `/word-study/review/sessions/${sessionId}/items/${itemId}/spelling`, method: "POST", data }),
      invalidatesTags: ["ReviewOverview", "WordStudySession"],
    }),
    excludeReviewItem: builder.mutation<WordStudyCommandResponse, { sessionId: string; itemId: string; itemConcurrencyStamp: string }>({
      query: ({ sessionId, itemId, ...data }) => ({ url: `/word-study/review/sessions/${sessionId}/items/${itemId}/exclude`, method: "POST", data }),
      invalidatesTags: ["ReviewOverview", "WordReviewExclusions", "WordStudySession"],
    }),
    getFavorites: builder.query<PagedWordLibrary<WordFavorite>, { page: number; pageSize: number }>({
      query: (params) => ({ url: "/users/me/word-favorites", params }),
      providesTags: ["WordFavorites"],
    }),
    setFavorite: builder.mutation<void, { wordId: string; favorite: boolean }>({
      query: ({ wordId, favorite }) => ({ url: `/users/me/word-favorites/${wordId}`, method: favorite ? "PUT" : "DELETE" }),
      invalidatesTags: ["WordFavorites", "WordStudySession"],
    }),
    getReviewExclusions: builder.query<PagedWordLibrary<WordReviewExclusion>, { page: number; pageSize: number }>({
      query: (params) => ({ url: "/users/me/word-review-exclusions", params }),
      providesTags: ["WordReviewExclusions"],
    }),
    restoreReview: builder.mutation<void, string>({
      query: (wordId) => ({ url: `/users/me/word-review-exclusions/${wordId}`, method: "DELETE" }),
      invalidatesTags: ["WordReviewExclusions", "ReviewOverview"],
    }),
    getSettings: builder.query<WordStudySettings, void>({
      query: () => ({ url: "/users/me/word-study-settings" }),
      providesTags: ["WordStudySettings"],
    }),
    updateSettings: builder.mutation<WordStudySettings, number>({
      query: (dailyWordStudyCount) => ({
        url: "/users/me/word-study-settings",
        method: "PUT",
        data: { dailyWordStudyCount },
      }),
      invalidatesTags: ["WordStudySettings", "WordStudyToday"],
    }),
    getToday: builder.query<WordStudyToday, void>({
      query: () => ({ url: "/word-study/today" }),
      providesTags: ["WordStudyToday"],
    }),
    startToday: builder.mutation<WordStudySession, void>({
      query: () => ({ url: "/word-study/today/start", method: "POST" }),
      invalidatesTags: ["WordStudyToday", "WordStudySession"],
    }),
    getNextItem: builder.query<WordStudyNextItem | null, string>({
      query: (sessionId) => ({ url: `/word-study/sessions/${sessionId}/next` }),
      providesTags: (_result, _error, sessionId) => [
        { type: "WordStudySession", id: sessionId },
      ],
    }),
    getSessionItems: builder.query<WordStudySessionItem[], string>({
      query: (sessionId) => ({
        url: `/word-study/sessions/${sessionId}/items`,
      }),
      providesTags: (_result, _error, sessionId) => [
        { type: "WordStudySessionItems", id: sessionId },
      ],
    }),
    submitResult: builder.mutation<
      WordStudySession,
      {
        sessionId: string;
        itemId: string;
        result: WordStudyResult;
      }
    >({
      query: ({ sessionId, itemId, result }) => ({
        url: `/word-study/sessions/${sessionId}/items/${itemId}/result`,
        method: "POST",
        data: { result },
      }),
      onQueryStarted: async (
        { sessionId, itemId, result },
        { dispatch, queryFulfilled },
      ) => {
        const patch = dispatch(
          wordStudyApi.util.updateQueryData(
            "getSessionItems",
            sessionId,
            (items) => {
              const item = items.find((value) => value.itemId === itemId);
              if (item) {
                item.status = result;
              }
            },
          ),
        );
        try {
          await queryFulfilled;
        } catch {
          patch.undo();
        }
      },
      invalidatesTags: (_result, _error, request) => [
        "WordStudyToday",
        { type: "WordStudySession", id: request.sessionId },
        { type: "WordStudySessionItems", id: request.sessionId },
      ],
    }),
    abandon: builder.mutation<WordStudySession, string>({
      query: (sessionId) => ({
        url: `/word-study/sessions/${sessionId}/abandon`,
        method: "POST",
      }),
      invalidatesTags: ["WordStudyToday", "WordStudySession"],
    }),
  }),
});

export const {
  useGetLearningOverviewQuery,
  useStartLearningMutation,
  useGetLearningSessionQuery,
  useSubmitLearningMemorizationMutation,
  useSubmitLearningSpellingMutation,
  useGetReviewOverviewQuery,
  useStartReviewMutation,
  useGetReviewSessionQuery,
  useSubmitReviewMemorizationMutation,
  useSubmitReviewSpellingMutation,
  useExcludeReviewItemMutation,
  useGetFavoritesQuery,
  useSetFavoriteMutation,
  useGetReviewExclusionsQuery,
  useRestoreReviewMutation,
  useGetSettingsQuery,
  useUpdateSettingsMutation,
  useGetTodayQuery,
  useStartTodayMutation,
  useLazyGetNextItemQuery,
  useGetSessionItemsQuery,
  useSubmitResultMutation,
  useAbandonMutation,
} = wordStudyApi;
