import { baseApi } from "@/services/baseApi.js";
import {
  normalizeArticleListItem,
  normalizeAdminArticle,
  normalizePage,
  normalizePreview,
} from "@/services/articleContracts.js";

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.categoryId) params.set("categoryId", filters.categoryId);
  if (filters.status) params.set("status", filters.status);
  return `/admin/articles?${params.toString()}`;
}

function articleTags(article) {
  return [
    { type: "AdminArticle", id: article.id },
    ...article.categories.map((category) => ({
      type: "ArticleCategory",
      id: category.id,
    })),
  ];
}

function invalidateArticle(_result, error, { articleId }) {
  return error
    ? []
    : [
        { type: "AdminArticle", id: articleId },
        { type: "AdminArticle", id: "LIST" },
      ];
}

/** RTK Query endpoints for the final editor article and canonical preview contracts. */
export const articlesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminArticles: builder.query({
      query: (filters) => ({ url: buildListUrl(filters) }),
      transformResponse: (value) =>
        normalizePage(value, normalizeArticleListItem),
      providesTags: (result) =>
        result
          ? [
              { type: "AdminArticle", id: "LIST" },
              ...result.items.flatMap(articleTags),
            ]
          : [{ type: "AdminArticle", id: "LIST" }],
    }),
    getAdminArticle: builder.query({
      query: (articleId) => ({ url: `/admin/articles/${articleId}` }),
      transformResponse: normalizeAdminArticle,
      providesTags: (result, _error, articleId) =>
        result
          ? articleTags(result)
          : [{ type: "AdminArticle", id: articleId }],
    }),
    createArticle: builder.mutation({
      query: (body) => ({ url: "/admin/articles", method: "POST", body }),
      transformResponse: normalizeAdminArticle,
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "AdminArticle", id: "LIST" }],
    }),
    updateArticle: builder.mutation({
      query: ({ articleId, ...body }) => ({
        url: `/admin/articles/${articleId}`,
        method: "PUT",
        body,
      }),
      transformResponse: normalizeAdminArticle,
      invalidatesTags: invalidateArticle,
    }),
    previewArticle: builder.mutation({
      query: (contentMarkdown) => ({
        url: "/admin/articles/preview",
        method: "POST",
        body: { contentMarkdown },
      }),
      transformResponse: normalizePreview,
    }),
    publishArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/admin/articles/${articleId}/publish`,
        method: "POST",
      }),
      transformResponse: normalizeAdminArticle,
      invalidatesTags: invalidateArticle,
    }),
    unpublishArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/admin/articles/${articleId}/unpublish`,
        method: "POST",
      }),
      transformResponse: normalizeAdminArticle,
      invalidatesTags: invalidateArticle,
    }),
    archiveArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/admin/articles/${articleId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateArticle,
    }),
  }),
});

export const {
  useArchiveArticleMutation,
  useCreateArticleMutation,
  useGetAdminArticleQuery,
  useGetAdminArticlesQuery,
  usePreviewArticleMutation,
  usePublishArticleMutation,
  useUnpublishArticleMutation,
  useUpdateArticleMutation,
} = articlesApi;
