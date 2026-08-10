import { createApi } from "@reduxjs/toolkit/query/react";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import { httpClient } from "@/services/httpClient";
import type {
  AudioPlayback,
  WordStudyNextItem,
  WordStudyResult,
  WordStudySession,
  WordStudySessionItem,
  WordStudySettings,
  WordStudyToday,
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
  ],
  endpoints: (builder) => ({
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
  useGetSettingsQuery,
  useUpdateSettingsMutation,
  useGetTodayQuery,
  useStartTodayMutation,
  useLazyGetNextItemQuery,
  useGetSessionItemsQuery,
  useSubmitResultMutation,
  useAbandonMutation,
} = wordStudyApi;

/** Requests a short-lived playback URL and keeps it out of Redux state. */
export async function requestWordAudio(
  audioClipId: string,
  signal?: AbortSignal,
) {
  const response = await httpClient.post<AudioPlayback>(
    `/audio/${audioClipId}/playback`,
    undefined,
    { signal },
  );
  return response.data;
}
