import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAdminVideo,
  normalizePlayback,
  normalizeVideoPage,
} from "@/services/videoContracts.js";

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  for (const key of [
    "keyword",
    "processingStatus",
    "publicationStatus",
    "categoryId",
    "createdById",
  ]) {
    if (filters[key]) params.set(key, filters[key]);
  }
  return `/admin/videos?${params.toString()}`;
}

function videoTags(result) {
  return result
    ? [
        { type: "Video", id: "LIST" },
        ...result.items.map(({ id }) => ({ type: "Video", id })),
      ]
    : [{ type: "Video", id: "LIST" }];
}

function invalidateVideo(_result, error, { videoId }) {
  return error
    ? []
    : [
        { type: "Video", id: videoId },
        { type: "Video", id: "LIST" },
        { type: "VideoCategory", id: "LIST" },
        { type: "VideoCategory", id: "OPTIONS" },
      ];
}

const VIDEO_CONTRACT_ERROR = Object.freeze({
  status: "CUSTOM_ERROR",
  detail: "服务端返回的视频数据格式无法识别，请刷新后重试。",
  errorCode: null,
  fieldErrors: {},
  kind: "contract",
});

/** Executes an RTK Query request while containing strict video DTO failures. */
async function executeVideoQuery(args, baseQuery, normalize) {
  const result = await baseQuery(args);
  if (result.error) return result;
  try {
    return { data: normalize(result.data) };
  } catch {
    return { error: VIDEO_CONTRACT_ERROR };
  }
}

function normalizedQuery(buildArgs, normalize) {
  return (argument, _api, _extraOptions, baseQuery) =>
    executeVideoQuery(buildArgs(argument), baseQuery, normalize);
}

/** RTK Query endpoints for global administrator video management. */
export const videosApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminVideos: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildListUrl(filters) }),
        normalizeVideoPage,
      ),
      providesTags: videoTags,
    }),
    getAdminVideo: builder.query({
      queryFn: normalizedQuery(
        (videoId) => ({ url: `/admin/videos/${videoId}` }),
        normalizeAdminVideo,
      ),
      providesTags: (_result, _error, videoId) => [
        { type: "Video", id: videoId },
      ],
    }),
    createVideo: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({ url: "/admin/videos", method: "POST", body }),
        normalizeAdminVideo,
      ),
      invalidatesTags: (_result, error) =>
        error
          ? []
          : [
              { type: "Video", id: "LIST" },
              { type: "VideoCategory", id: "LIST" },
            ],
    }),
    updateVideo: builder.mutation({
      queryFn: normalizedQuery(
        ({ videoId, ...body }) => ({
          url: `/admin/videos/${videoId}`,
          method: "PUT",
          body,
        }),
        normalizeAdminVideo,
      ),
      invalidatesTags: invalidateVideo,
    }),
    publishVideo: builder.mutation({
      queryFn: normalizedQuery(
        ({ videoId, concurrencyStamp }) => ({
          url: `/admin/videos/${videoId}/publish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminVideo,
      ),
      invalidatesTags: invalidateVideo,
    }),
    unpublishVideo: builder.mutation({
      queryFn: normalizedQuery(
        ({ videoId, concurrencyStamp }) => ({
          url: `/admin/videos/${videoId}/unpublish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminVideo,
      ),
      invalidatesTags: invalidateVideo,
    }),
    retryVideo: builder.mutation({
      queryFn: normalizedQuery(
        ({ videoId, concurrencyStamp }) => ({
          url: `/admin/videos/${videoId}/retry`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminVideo,
      ),
      invalidatesTags: invalidateVideo,
    }),
    archiveVideo: builder.mutation({
      queryFn: normalizedQuery(
        ({ videoId, concurrencyStamp }) => ({
          url: `/admin/videos/${videoId}/archive`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminVideo,
      ),
      invalidatesTags: invalidateVideo,
    }),
    getVideoPlayback: builder.mutation({
      queryFn: normalizedQuery(
        (videoId) => ({
          url: `/admin/videos/${videoId}/playback`,
          method: "POST",
        }),
        normalizePlayback,
      ),
    }),
  }),
});

export const {
  useArchiveVideoMutation,
  useCreateVideoMutation,
  useGetAdminVideoQuery,
  useGetAdminVideosQuery,
  useGetVideoPlaybackMutation,
  usePublishVideoMutation,
  useRetryVideoMutation,
  useUnpublishVideoMutation,
  useUpdateVideoMutation,
} = videosApi;
