import { baseApi } from "@/services/baseApi.js";
import {
  normalizeClearVideoCategory,
  normalizeVideoCategory,
  normalizeVideoCategoryPage,
} from "@/services/videoContracts.js";

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    includeInactive: String(filters.includeInactive),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  return `/admin/video-categories?${params.toString()}`;
}

function categoryTags(result) {
  return result
    ? [
        { type: "VideoCategory", id: "LIST" },
        ...result.items.map(({ id }) => ({ type: "VideoCategory", id })),
      ]
    : [{ type: "VideoCategory", id: "LIST" }];
}

function invalidateCategory(_result, error, { categoryId }) {
  return error
    ? []
    : [
        { type: "VideoCategory", id: categoryId },
        { type: "VideoCategory", id: "LIST" },
        { type: "VideoCategory", id: "OPTIONS" },
      ];
}

/** Administrator video-category endpoints with explicit clear and delete mutations. */
export const videoCategoriesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getVideoCategories: builder.query({
      query: (filters) => ({ url: buildListUrl(filters) }),
      transformResponse: normalizeVideoCategoryPage,
      providesTags: categoryTags,
    }),
    getAllVideoCategoryOptions: builder.query({
      async queryFn(_argument, _api, _extraOptions, baseQuery) {
        const items = [];
        let page = 1;
        let totalPages = 1;
        do {
          const result = await baseQuery({
            url: buildListUrl({
              page,
              pageSize: 100,
              keyword: "",
              includeInactive: true,
            }),
          });
          if (result.error) return { error: result.error };
          const normalized = normalizeVideoCategoryPage(result.data);
          if (normalized.totalPages > 1000) {
            return {
              error: {
                status: "CUSTOM_ERROR",
                detail: "视频分类数量超出安全加载范围。",
                kind: "contract",
              },
            };
          }
          items.push(...normalized.items);
          totalPages = normalized.totalPages;
          page += 1;
        } while (page <= totalPages && page <= 1000);
        return {
          data: Array.from(
            new Map(items.map((item) => [item.id, item])).values(),
          ),
        };
      },
      providesTags: (result) =>
        result
          ? [
              { type: "VideoCategory", id: "OPTIONS" },
              ...result.map(({ id }) => ({ type: "VideoCategory", id })),
            ]
          : [{ type: "VideoCategory", id: "OPTIONS" }],
    }),
    createVideoCategory: builder.mutation({
      query: (body) => ({
        url: "/admin/video-categories",
        method: "POST",
        body,
      }),
      transformResponse: normalizeVideoCategory,
      invalidatesTags: (_result, error) =>
        error
          ? []
          : [
              { type: "VideoCategory", id: "LIST" },
              { type: "VideoCategory", id: "OPTIONS" },
            ],
    }),
    updateVideoCategory: builder.mutation({
      query: ({ categoryId, ...body }) => ({
        url: `/admin/video-categories/${categoryId}`,
        method: "PUT",
        body,
      }),
      transformResponse: normalizeVideoCategory,
      invalidatesTags: invalidateCategory,
    }),
    clearVideoCategory: builder.mutation({
      query: ({ categoryId }) => ({
        url: `/admin/video-categories/${categoryId}/videos`,
        method: "DELETE",
      }),
      transformResponse: normalizeClearVideoCategory,
      invalidatesTags: (result, error, argument) =>
        error
          ? []
          : [
              ...invalidateCategory(result, error, argument),
              { type: "Video", id: "LIST" },
            ],
    }),
    deleteVideoCategory: builder.mutation({
      query: ({ categoryId }) => ({
        url: `/admin/video-categories/${categoryId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateCategory,
    }),
  }),
});

export const {
  useClearVideoCategoryMutation,
  useCreateVideoCategoryMutation,
  useDeleteVideoCategoryMutation,
  useGetAllVideoCategoryOptionsQuery,
  useGetVideoCategoriesQuery,
  useUpdateVideoCategoryMutation,
} = videoCategoriesApi;
