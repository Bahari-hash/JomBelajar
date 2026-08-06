import { createApi } from "@reduxjs/toolkit/query/react";
import { httpClient } from "@/services/httpClient";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";
import type {
  PagedResponse,
  VideoCatalogItem,
  VideoCatalogQuery,
  VideoCategory,
  VideoCategoryListQuery,
  VideoDetails,
  VideoPlayback,
} from "@/features/videos/videoTypes";

/** Caches non-sensitive user video catalog, category, and detail responses. */
export const videoApi = createApi({
  reducerPath: "videoApi",
  baseQuery: axiosBaseQuery(),
  endpoints: (builder) => ({
    getVideos: builder.query<
      PagedResponse<VideoCatalogItem>,
      VideoCatalogQuery
    >({
      query: ({ page, pageSize, keyword, categoryId }) => ({
        url: "/videos",
        params: { page, pageSize, keyword, categoryId },
      }),
    }),
    getVideo: builder.query<VideoDetails, string>({
      query: (videoId) => ({ url: `/videos/${videoId}` }),
    }),
    getVideoCategories: builder.query<
      PagedResponse<VideoCategory>,
      VideoCategoryListQuery
    >({
      query: ({ page, pageSize, keyword }) => ({
        url: "/video-categories",
        params: { page, pageSize, keyword },
      }),
    }),
  }),
});

export const {
  useGetVideoQuery,
  useGetVideoCategoriesQuery,
  useGetVideosQuery,
} = videoApi;

/** Requests a short-lived playback grant without retaining it in RTK Query. */
export async function requestVideoPlayback(
  videoId: string,
  signal?: AbortSignal,
) {
  const response = await httpClient.post<VideoPlayback>(
    `/videos/${videoId}/playback`,
    undefined,
    { signal },
  );
  return response.data;
}

/** Persists the latest locally observed playback position for the signed-in user. */
export async function updateVideoProgress(
  videoId: string,
  positionSeconds: number,
  signal?: AbortSignal,
) {
  await httpClient.put<void>(
    `/videos/${videoId}/progress`,
    { positionSeconds },
    { signal },
  );
}
