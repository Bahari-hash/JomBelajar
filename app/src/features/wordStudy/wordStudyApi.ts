import { createApi } from "@reduxjs/toolkit/query/react";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import type {
  WordMemorizationResult,
  WordStudySettings,
  WordLearningOverview,
  WordReviewOverview,
  WordStudyCommandResponse,
  WordStudySessionState,
  PagedWordLibrary,
  WordFavorite,
  WordReviewExclusion,
  WordStudyTodayReview,
  WordStudyCheckInCalendar,
} from "@/features/wordStudy/wordStudyTypes";

/** Provides authenticated daily word-study queries and mutations through Axios. */
export const wordStudyApi = createApi({
  reducerPath: "wordStudyApi",
  baseQuery: axiosBaseQuery(),
  tagTypes: [
    "WordStudySettings",
    "WordStudySession",
    "LearningOverview",
    "ReviewOverview",
    "WordFavorites",
    "WordReviewExclusions",
    "TodayReview",
    "CheckInCalendar",
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
    submitLearningMemorization: builder.mutation<
      WordStudyCommandResponse,
      {
        sessionId: string;
        itemId: string;
        result: WordMemorizationResult;
        itemConcurrencyStamp: string;
      }
    >({
      query: ({ sessionId, itemId, ...data }) => ({
        url: `/word-study/learning/sessions/${sessionId}/items/${itemId}/memorization`,
        method: "POST",
        data,
      }),
      invalidatesTags: [
        "LearningOverview",
        "WordStudySession",
        "TodayReview",
        "CheckInCalendar",
      ],
    }),
    submitLearningSpelling: builder.mutation<
      WordStudyCommandResponse,
      {
        sessionId: string;
        itemId: string;
        answer: string;
        itemConcurrencyStamp: string;
      }
    >({
      query: ({ sessionId, itemId, ...data }) => ({
        url: `/word-study/learning/sessions/${sessionId}/items/${itemId}/spelling`,
        method: "POST",
        data,
      }),
      invalidatesTags: [
        "LearningOverview",
        "WordStudySession",
        "TodayReview",
        "CheckInCalendar",
      ],
    }),
    getReviewOverview: builder.query<WordReviewOverview, void>({
      query: () => ({ url: "/word-study/review/overview" }),
      providesTags: ["ReviewOverview"],
    }),
    getTodayReview: builder.query<
      WordStudyTodayReview,
      { page?: number; pageSize?: number } | void
    >({
      query: (params) => ({
        url: "/word-study/review/today",
        params: { page: 1, pageSize: 20, ...params },
      }),
      providesTags: ["TodayReview"],
    }),
    getCheckInCalendar: builder.query<
      WordStudyCheckInCalendar,
      { year?: number; month?: number } | void
    >({
      query: (params) => ({ url: "/word-study/check-ins", params }),
      providesTags: ["CheckInCalendar"],
    }),
    startReview: builder.mutation<WordStudySessionState, void>({
      query: () => ({ url: "/word-study/review/sessions", method: "POST" }),
      invalidatesTags: ["ReviewOverview"],
    }),
    getReviewSession: builder.query<WordStudySessionState, string>({
      query: (id) => ({ url: `/word-study/review/sessions/${id}` }),
      providesTags: (_r, _e, id) => [{ type: "WordStudySession", id }],
    }),
    submitReviewMemorization: builder.mutation<
      WordStudyCommandResponse,
      {
        sessionId: string;
        itemId: string;
        result: WordMemorizationResult;
        itemConcurrencyStamp: string;
      }
    >({
      query: ({ sessionId, itemId, ...data }) => ({
        url: `/word-study/review/sessions/${sessionId}/items/${itemId}/memorization`,
        method: "POST",
        data,
      }),
      invalidatesTags: ["ReviewOverview", "WordStudySession", "TodayReview"],
    }),
    submitReviewSpelling: builder.mutation<
      WordStudyCommandResponse,
      {
        sessionId: string;
        itemId: string;
        answer: string;
        itemConcurrencyStamp: string;
      }
    >({
      query: ({ sessionId, itemId, ...data }) => ({
        url: `/word-study/review/sessions/${sessionId}/items/${itemId}/spelling`,
        method: "POST",
        data,
      }),
      invalidatesTags: ["ReviewOverview", "WordStudySession", "TodayReview"],
    }),
    excludeReviewItem: builder.mutation<
      WordStudyCommandResponse,
      { sessionId: string; itemId: string; itemConcurrencyStamp: string }
    >({
      query: ({ sessionId, itemId, ...data }) => ({
        url: `/word-study/review/sessions/${sessionId}/items/${itemId}/exclude`,
        method: "POST",
        data,
      }),
      invalidatesTags: [
        "ReviewOverview",
        "TodayReview",
        "WordReviewExclusions",
        "WordStudySession",
      ],
    }),
    getFavorites: builder.query<
      PagedWordLibrary<WordFavorite>,
      { page: number; pageSize: number }
    >({
      query: (params) => ({ url: "/users/me/word-favorites", params }),
      providesTags: ["WordFavorites"],
    }),
    setFavorite: builder.mutation<void, { wordId: string; favorite: boolean }>({
      query: ({ wordId, favorite }) => ({
        url: `/users/me/word-favorites/${wordId}`,
        method: favorite ? "PUT" : "DELETE",
      }),
      invalidatesTags: ["WordFavorites", "WordStudySession", "TodayReview"],
    }),
    getReviewExclusions: builder.query<
      PagedWordLibrary<WordReviewExclusion>,
      { page: number; pageSize: number }
    >({
      query: (params) => ({ url: "/users/me/word-review-exclusions", params }),
      providesTags: ["WordReviewExclusions"],
    }),
    restoreReview: builder.mutation<void, string>({
      query: (wordId) => ({
        url: `/users/me/word-review-exclusions/${wordId}`,
        method: "DELETE",
      }),
      invalidatesTags: ["WordReviewExclusions", "ReviewOverview"],
    }),
    getSettings: builder.query<WordStudySettings, void>({
      query: () => ({ url: "/users/me/word-study-settings" }),
      providesTags: ["WordStudySettings"],
    }),
    updateSettings: builder.mutation<
      WordStudySettings,
      { dailyWordStudyCount: number; dailyWordReviewCount: number }
    >({
      query: (data) => ({
        url: "/users/me/word-study-settings",
        method: "PUT",
        data,
      }),
      invalidatesTags: [
        "WordStudySettings",
        "LearningOverview",
        "ReviewOverview",
      ],
    }),
  }),
});

export const {
  useGetLearningOverviewQuery,
  useStartLearningMutation,
  useGetLearningSessionQuery,
  useLazyGetLearningSessionQuery,
  useSubmitLearningMemorizationMutation,
  useSubmitLearningSpellingMutation,
  useGetReviewOverviewQuery,
  useGetTodayReviewQuery,
  useGetCheckInCalendarQuery,
  useStartReviewMutation,
  useGetReviewSessionQuery,
  useLazyGetReviewSessionQuery,
  useSubmitReviewMemorizationMutation,
  useSubmitReviewSpellingMutation,
  useExcludeReviewItemMutation,
  useGetFavoritesQuery,
  useSetFavoriteMutation,
  useGetReviewExclusionsQuery,
  useRestoreReviewMutation,
  useGetSettingsQuery,
  useUpdateSettingsMutation,
} = wordStudyApi;
