import { baseApi } from "@/services/baseApi.js";
import {
  normalizeArticleListItem,
  normalizeEditorArticle,
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
  return `/editor/articles?${params.toString()}`;
}

function articleTags(article) {
  return [
    { type: "EditorArticle", id: article.id },
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
        { type: "EditorArticle", id: articleId },
        { type: "EditorArticle", id: "LIST" },
      ];
}

/** RTK Query endpoints for the final editor article and canonical preview contracts. */
export const articlesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getEditorArticles: builder.query({
      query: (filters) => ({ url: buildListUrl(filters) }),
      transformResponse: (value) =>
        normalizePage(value, normalizeArticleListItem),
      providesTags: (result) =>
        result
          ? [
              { type: "EditorArticle", id: "LIST" },
              ...result.items.flatMap(articleTags),
            ]
          : [{ type: "EditorArticle", id: "LIST" }],
    }),
    getEditorArticle: builder.query({
      query: (articleId) => ({ url: `/editor/articles/${articleId}` }),
      transformResponse: normalizeEditorArticle,
      providesTags: (result, _error, articleId) =>
        result
          ? articleTags(result)
          : [{ type: "EditorArticle", id: articleId }],
    }),
    createArticle: builder.mutation({
      query: (body) => ({ url: "/editor/articles", method: "POST", body }),
      transformResponse: normalizeEditorArticle,
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "EditorArticle", id: "LIST" }],
    }),
    updateArticle: builder.mutation({
      query: ({ articleId, ...body }) => ({
        url: `/editor/articles/${articleId}`,
        method: "PUT",
        body,
      }),
      transformResponse: normalizeEditorArticle,
      invalidatesTags: invalidateArticle,
    }),
    previewArticle: builder.mutation({
      query: (contentMarkdown) => ({
        url: "/editor/articles/preview",
        method: "POST",
        body: { contentMarkdown },
      }),
      transformResponse: normalizePreview,
    }),
    publishArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/editor/articles/${articleId}/publish`,
        method: "POST",
      }),
      transformResponse: normalizeEditorArticle,
      invalidatesTags: invalidateArticle,
    }),
    unpublishArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/editor/articles/${articleId}/unpublish`,
        method: "POST",
      }),
      transformResponse: normalizeEditorArticle,
      invalidatesTags: invalidateArticle,
    }),
    archiveArticle: builder.mutation({
      query: ({ articleId }) => ({
        url: `/editor/articles/${articleId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidateArticle,
    }),
  }),
});

export const {
  useArchiveArticleMutation,
  useCreateArticleMutation,
  useGetEditorArticleQuery,
  useGetEditorArticlesQuery,
  usePreviewArticleMutation,
  usePublishArticleMutation,
  useUnpublishArticleMutation,
  useUpdateArticleMutation,
} = articlesApi;
