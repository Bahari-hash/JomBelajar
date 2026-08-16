import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAdminWord,
  normalizeAdminAudioClip,
  normalizeAudioOptionPage,
  normalizeAudioPlayback,
  normalizeAudioPresign,
  normalizeAudioUploadCapability,
  normalizeConfirmedAudioResource,
  normalizeWordPage,
} from "@/services/wordContracts.js";

const CONTRACT_ERROR = Object.freeze({
  status: "CUSTOM_ERROR",
  detail: "服务端返回的单词数据格式无法识别，请刷新后重试。",
  errorCode: null,
  fieldErrors: {},
  kind: "contract",
});

async function executeNormalized(args, baseQuery, normalize) {
  const result = await baseQuery(args);
  if (result.error) return result;
  try {
    return { data: normalize(result.data) };
  } catch {
    return { error: CONTRACT_ERROR };
  }
}

function normalizedQuery(buildArgs, normalize) {
  return (argument, _api, _extraOptions, baseQuery) =>
    executeNormalized(buildArgs(argument), baseQuery, normalize);
}

function buildWordListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  for (const key of [
    "keyword",
    "status",
    "partOfSpeech",
    "definition",
  ])
    if (filters[key]) params.set(key, filters[key]);
  return `/admin/words?${params.toString()}`;
}

function buildAudioListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    kind: filters.kind,
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.processingStatus)
    params.set("processingStatus", filters.processingStatus);
  if (filters.publicationStatus)
    params.set("publicationStatus", filters.publicationStatus);
  return `/admin/audio?${params.toString()}`;
}

function audioFileMetadata(file) {
  const dot = file.name.lastIndexOf(".");
  return {
    originalName: file.name,
    extension: dot >= 0 ? file.name.slice(dot).toLowerCase() : "",
    contentType: file.type,
    size: file.size,
    module: "Audio",
  };
}

function invalidateAudio(_result, error) {
  return error ? [] : [{ type: "AudioClip", id: "LIST" }];
}

function invalidateWord(_result, error, { wordId }) {
  return error
    ? []
    : [
        { type: "Word", id: wordId },
        { type: "Word", id: "LIST" },
      ];
}

/** RTK Query endpoints for global word management and referenced audio playback. */
export const wordsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminWords: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildWordListUrl(filters) }),
        normalizeWordPage,
      ),
      providesTags: (result) =>
        result
          ? [
              { type: "Word", id: "LIST" },
              ...result.items.map(({ id }) => ({ type: "Word", id })),
            ]
          : [{ type: "Word", id: "LIST" }],
    }),
    getAdminWord: builder.query({
      queryFn: normalizedQuery(
        (wordId) => ({ url: `/admin/words/${wordId}` }),
        normalizeAdminWord,
      ),
      providesTags: (_result, _error, wordId) => [{ type: "Word", id: wordId }],
    }),
    createWord: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({ url: "/admin/words", method: "POST", body }),
        normalizeAdminWord,
      ),
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "Word", id: "LIST" }],
    }),
    updateWord: builder.mutation({
      queryFn: normalizedQuery(
        ({ wordId, ...body }) => ({
          url: `/admin/words/${wordId}`,
          method: "PUT",
          body,
        }),
        normalizeAdminWord,
      ),
      invalidatesTags: invalidateWord,
    }),
    publishWord: builder.mutation({
      queryFn: normalizedQuery(
        ({ wordId, concurrencyStamp }) => ({
          url: `/admin/words/${wordId}/publish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminWord,
      ),
      invalidatesTags: invalidateWord,
    }),
    unpublishWord: builder.mutation({
      queryFn: normalizedQuery(
        ({ wordId, concurrencyStamp }) => ({
          url: `/admin/words/${wordId}/unpublish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminWord,
      ),
      invalidatesTags: invalidateWord,
    }),
    archiveWord: builder.mutation({
      queryFn: normalizedQuery(
        ({ wordId, concurrencyStamp }) => ({
          url: `/admin/words/${wordId}/archive`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminWord,
      ),
      invalidatesTags: invalidateWord,
    }),
    deleteWord: builder.mutation({
      query: ({ wordId, concurrencyStamp }) => ({
        url: `/admin/words/${wordId}`,
        method: "DELETE",
        body: { concurrencyStamp },
      }),
      invalidatesTags: invalidateWord,
    }),
    getWordAudioOptions: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildAudioListUrl(filters) }),
        normalizeAudioOptionPage,
      ),
      providesTags: (result) =>
        result
          ? [
              { type: "AudioClip", id: "LIST" },
              ...result.items.map(({ id }) => ({ type: "AudioClip", id })),
            ]
          : [{ type: "AudioClip", id: "LIST" }],
    }),
    getAudioUploadCapability: builder.query({
      queryFn: normalizedQuery(
        () => ({ url: "/uploads/admin/media/capabilities?module=Audio" }),
        normalizeAudioUploadCapability,
      ),
    }),
    presignWordAudio: builder.mutation({
      queryFn: normalizedQuery(
        (file) => ({
          url: "/uploads/admin/media/presign",
          method: "POST",
          body: audioFileMetadata(file),
        }),
        normalizeAudioPresign,
      ),
    }),
    confirmWordAudioResource: builder.mutation({
      queryFn: normalizedQuery(
        (resourceId) => ({
          url: `/uploads/resources/${resourceId}/confirm`,
          method: "PUT",
        }),
        normalizeConfirmedAudioResource,
      ),
    }),
    createWordAudio: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({ url: "/admin/audio", method: "POST", body }),
        normalizeAdminAudioClip,
      ),
      invalidatesTags: invalidateAudio,
    }),
    publishWordAudio: builder.mutation({
      queryFn: normalizedQuery(
        (audioClipId) => ({
          url: `/admin/audio/${audioClipId}/publish`,
          method: "POST",
        }),
        normalizeAdminAudioClip,
      ),
      invalidatesTags: invalidateAudio,
    }),
    retryWordAudio: builder.mutation({
      queryFn: normalizedQuery(
        (audioClipId) => ({
          url: `/admin/audio/${audioClipId}/retry`,
          method: "POST",
        }),
        normalizeAdminAudioClip,
      ),
      invalidatesTags: invalidateAudio,
    }),
    getAudioPlayback: builder.mutation({
      queryFn: normalizedQuery(
        (audioClipId) => ({
          url: `/audio/${audioClipId}/playback`,
          method: "POST",
        }),
        normalizeAudioPlayback,
      ),
    }),
  }),
});

export const {
  useArchiveWordMutation,
  useCreateWordMutation,
  useCreateWordAudioMutation,
  useConfirmWordAudioResourceMutation,
  useDeleteWordMutation,
  useGetAdminWordQuery,
  useGetAdminWordsQuery,
  useGetAudioPlaybackMutation,
  useGetAudioUploadCapabilityQuery,
  useGetWordAudioOptionsQuery,
  usePublishWordMutation,
  usePresignWordAudioMutation,
  usePublishWordAudioMutation,
  useRetryWordAudioMutation,
  useUnpublishWordMutation,
  useUpdateWordMutation,
} = wordsApi;
