import { createApi } from "@reduxjs/toolkit/query/react";
import type {
  ArticleCategory,
  ArticleCategoryListQuery,
  ArticleListItem,
  ArticleListQuery,
  PagedResponse,
  PublicArticle,
} from "@/features/articles/articleTypes";
import { axiosBaseQuery } from "@/services/axiosBaseQuery";

/** Caches public article discovery, category, and detail queries through Axios. */
export const articleApi = createApi({
  reducerPath: "articleApi",
  baseQuery: axiosBaseQuery(),
  endpoints: (builder) => ({
    getArticles: builder.query<
      PagedResponse<ArticleListItem>,
      ArticleListQuery
    >({
      query: ({ page, pageSize, keyword, categoryId }) => ({
        url: "/articles",
        params: { page, pageSize, keyword, categoryId },
      }),
    }),
    getArticle: builder.query<PublicArticle, string>({
      query: (articleId) => ({
        url: `/articles/${articleId}`,
      }),
    }),
    getArticleCategories: builder.query<
      PagedResponse<ArticleCategory>,
      ArticleCategoryListQuery
    >({
      query: ({ page, pageSize, keyword }) => ({
        url: "/article-categories",
        params: { page, pageSize, keyword },
      }),
    }),
  }),
});

export const {
  useGetArticleQuery,
  useGetArticlesQuery,
  useGetArticleCategoriesQuery,
} = articleApi;
