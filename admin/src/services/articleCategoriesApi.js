import { baseApi } from "@/services/baseApi.js";
import {
  normalizeArticleCategory,
  normalizePage,
} from "@/services/articleContracts.js";

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    includeInactive: String(filters.includeInactive),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  return `/admin/article-categories?${params.toString()}`;
}

function categoryTags(result) {
  return result
    ? [
        { type: "ArticleCategory", id: "LIST" },
        ...result.items.map(({ id }) => ({ type: "ArticleCategory", id })),
      ]
    : [{ type: "ArticleCategory", id: "LIST" }];
}

function invalidateCategory(_result, error, { categoryId }) {
  return error
    ? []
    : [
        { type: "ArticleCategory", id: categoryId },
        { type: "ArticleCategory", id: "LIST" },
        { type: "ArticleCategory", id: "OPTIONS" },
      ];
}

/** Administrator category endpoints, including paged management and complete editor options. */
export const articleCategoriesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getArticleCategories: builder.query({
      query: (filters) => ({ url: buildListUrl(filters) }),
      transformResponse: (value) =>
        normalizePage(value, normalizeArticleCategory),
      providesTags: categoryTags,
    }),
    getAllArticleCategoryOptions: builder.query({
      async queryFn(_argument, _api, _extraOptions, baseQuery) {
        const items = [];
        let page = 1;
        let totalPages = 1;
        do {
          const result = await baseQuery({
            url: buildListUrl({
              page,
              pageSize: 100,
              includeInactive: true,
              keyword: "",
            }),
          });
          if (result.error) return { error: result.error };
          const normalized = normalizePage(
            result.data,
            normalizeArticleCategory,
          );
          if (normalized.totalPages > 1000) {
            return {
              error: {
                status: "CUSTOM_ERROR",
                detail: "分类数量超出安全加载范围。",
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
              { type: "ArticleCategory", id: "OPTIONS" },
              ...result.map(({ id }) => ({ type: "ArticleCategory", id })),
            ]
          : [{ type: "ArticleCategory", id: "OPTIONS" }],
    }),
    createArticleCategory: builder.mutation({
      query: (body) => ({
        url: "/admin/article-categories",
        method: "POST",
        body,
      }),
      transformResponse: normalizeArticleCategory,
      invalidatesTags: (_result, error) =>
        error
          ? []
          : [
              { type: "ArticleCategory", id: "LIST" },
              { type: "ArticleCategory", id: "OPTIONS" },
            ],
    }),
    updateArticleCategory: builder.mutation({
      query: ({ categoryId, ...body }) => ({
        url: `/admin/article-categories/${categoryId}`,
        method: "PUT",
        body,
      }),
      transformResponse: normalizeArticleCategory,
      invalidatesTags: invalidateCategory,
    }),
    deleteArticleCategory: builder.mutation({
      query: ({ categoryId }) => ({
        url: `/admin/article-categories/${categoryId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateCategory,
    }),
    clearArticleCategory: builder.mutation({
      query: ({ categoryId }) => ({
        url: `/admin/article-categories/${categoryId}/articles`,
        method: "DELETE",
      }),
      transformResponse: (value) => {
        if (
          typeof value?.categoryId !== "string" ||
          !Number.isInteger(value?.removedArticleCount) ||
          value.removedArticleCount < 0
        ) {
          throw new Error("API returned invalid category clear data.");
        }
        return {
          categoryId: value.categoryId,
          removedArticleCount: value.removedArticleCount,
        };
      },
      invalidatesTags: (result, error, argument) =>
        error
          ? []
          : [
              ...invalidateCategory(result, error, argument),
              { type: "EditorArticle", id: "LIST" },
            ],
    }),
  }),
});

export const {
  useClearArticleCategoryMutation,
  useCreateArticleCategoryMutation,
  useDeleteArticleCategoryMutation,
  useGetAllArticleCategoryOptionsQuery,
  useGetArticleCategoriesQuery,
  useUpdateArticleCategoryMutation,
} = articleCategoriesApi;
