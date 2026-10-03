import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAudioPlayback,
  normalizeAudioResourceDetails,
  normalizeAudioResourcePage,
  normalizeAudioUploadCapability,
  normalizeAudioUploadInitialization,
  normalizeMultipartStatus,
  normalizePartPresigns,
} from "@/services/audioContracts.js";

const AUDIO_CONTRACT_ERROR = Object.freeze({
  status: "CUSTOM_ERROR",
  detail: "服务端返回的音频数据格式无法识别，请刷新后重试。",
  errorCode: null,
  fieldErrors: {},
  kind: "contract",
});

function fileMetadata(file) {
  const dot = file.name.lastIndexOf(".");
  return {
    originalName: file.name,
    extension: dot >= 0 ? file.name.slice(dot).toLowerCase() : "",
    contentType: file.type,
    size: file.size,
  };
}

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.status) params.set("status", filters.status);
  return `/admin/audio?${params.toString()}`;
}

async function executeNormalized(args, baseQuery, normalize) {
  const result = await baseQuery(args);
  if (result.error) return result;
  try {
    return { data: normalize(result.data) };
  } catch {
    return { error: AUDIO_CONTRACT_ERROR };
  }
}

function normalizedQuery(buildArgs, normalize) {
  return (argument, _api, _extraOptions, baseQuery) =>
    executeNormalized(buildArgs(argument), baseQuery, normalize);
}

function audioListTags(result) {
  return result
    ? [
        { type: "AudioResource", id: "LIST" },
        ...result.items.map(({ id }) => ({ type: "AudioResource", id })),
      ]
    : [{ type: "AudioResource", id: "LIST" }];
}

function invalidateList(_result, error) {
  return error ? [] : [{ type: "AudioResource", id: "LIST" }];
}

function invalidateResource(_result, error, argument) {
  const audioResourceId =
    typeof argument === "string" ? argument : argument.audioResourceId;
  return error
    ? []
    : [
        { type: "AudioResource", id: audioResourceId },
        { type: "AudioResource", id: "LIST" },
      ];
}

/** RTK Query endpoints for the shared administrator audio resource library. */
export const audioApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminAudioResources: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildListUrl(filters) }),
        normalizeAudioResourcePage,
      ),
      providesTags: audioListTags,
    }),
    getAdminAudioResource: builder.query({
      queryFn: normalizedQuery(
        (audioResourceId) => ({ url: `/admin/audio/${audioResourceId}` }),
        normalizeAudioResourceDetails,
      ),
      providesTags: (_result, _error, audioResourceId) => [
        { type: "AudioResource", id: audioResourceId },
      ],
    }),
    getAudioUploadCapability: builder.query({
      queryFn: normalizedQuery(
        () => ({ url: "/uploads/admin/media/capabilities?module=Audio" }),
        normalizeAudioUploadCapability,
      ),
    }),
    initializeSimpleAudioUpload: builder.mutation({
      queryFn: normalizedQuery(
        (file) => ({
          url: "/admin/audio/uploads/simple",
          method: "POST",
          body: fileMetadata(file),
        }),
        normalizeAudioUploadInitialization,
      ),
      invalidatesTags: invalidateList,
    }),
    initializeMultipartAudioUpload: builder.mutation({
      queryFn: normalizedQuery(
        (file) => ({
          url: "/admin/audio/uploads/multipart",
          method: "POST",
          body: fileMetadata(file),
        }),
        normalizeAudioUploadInitialization,
      ),
      invalidatesTags: invalidateList,
    }),
    confirmAudioUpload: builder.mutation({
      query: (audioResourceId) => ({
        url: `/admin/audio/${audioResourceId}/upload/confirm`,
        method: "PUT",
      }),
      invalidatesTags: invalidateResource,
    }),
    presignAudioMultipartParts: builder.mutation({
      queryFn: normalizedQuery(
        ({ sessionId, partNumbers }) => ({
          url: `/admin/audio/multipart/${sessionId}/parts/presign`,
          method: "POST",
          body: { partNumbers },
        }),
        normalizePartPresigns,
      ),
    }),
    getAudioMultipartStatus: builder.query({
      queryFn: normalizedQuery(
        (sessionId) => ({ url: `/admin/audio/multipart/${sessionId}` }),
        normalizeMultipartStatus,
      ),
    }),
    completeAudioMultipart: builder.mutation({
      queryFn: normalizedQuery(
        ({ sessionId, parts }) => ({
          url: `/admin/audio/multipart/${sessionId}/complete`,
          method: "POST",
          body: { parts },
        }),
        normalizeMultipartStatus,
      ),
    }),
    abortAudioMultipart: builder.mutation({
      query: (sessionId) => ({
        url: `/admin/audio/multipart/${sessionId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateList,
    }),
    renameAudioResource: builder.mutation({
      queryFn: normalizedQuery(
        ({ audioResourceId, name }) => ({
          url: `/admin/audio/${audioResourceId}/name`,
          method: "PATCH",
          body: { name },
        }),
        normalizeAudioResourceDetails,
      ),
      invalidatesTags: invalidateResource,
    }),
    retryAudioUpload: builder.mutation({
      queryFn: normalizedQuery(
        ({ audioResourceId, file }) => ({
          url: `/admin/audio/${audioResourceId}/retry-upload`,
          method: "POST",
          body: fileMetadata(file),
        }),
        normalizeAudioUploadInitialization,
      ),
      invalidatesTags: invalidateResource,
    }),
    reprocessAudioResource: builder.mutation({
      queryFn: normalizedQuery(
        (audioResourceId) => ({
          url: `/admin/audio/${audioResourceId}/reprocess`,
          method: "POST",
        }),
        normalizeAudioResourceDetails,
      ),
      invalidatesTags: invalidateResource,
    }),
    deleteAudioResource: builder.mutation({
      query: (audioResourceId) => ({
        url: `/admin/audio/${audioResourceId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateResource,
    }),
    getAudioPlayback: builder.mutation({
      queryFn: normalizedQuery(
        (audioResourceId) => ({
          url: `/audio/${audioResourceId}/playback`,
          method: "POST",
        }),
        normalizeAudioPlayback,
      ),
    }),
  }),
});

export const {
  useLazyGetAdminAudioResourcesQuery,
  useAbortAudioMultipartMutation,
  useCompleteAudioMultipartMutation,
  useConfirmAudioUploadMutation,
  useDeleteAudioResourceMutation,
  useGetAdminAudioResourceQuery,
  useLazyGetAdminAudioResourceQuery,
  useGetAdminAudioResourcesQuery,
  useGetAudioMultipartStatusQuery,
  useLazyGetAudioMultipartStatusQuery,
  useGetAudioPlaybackMutation,
  useGetAudioUploadCapabilityQuery,
  useInitializeMultipartAudioUploadMutation,
  useInitializeSimpleAudioUploadMutation,
  usePresignAudioMultipartPartsMutation,
  useRenameAudioResourceMutation,
  useReprocessAudioResourceMutation,
  useRetryAudioUploadMutation,
} = audioApi;
