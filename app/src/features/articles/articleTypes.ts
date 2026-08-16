export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ArticleCategorySummary {
  id: string;
  name: string;
  slug: string;
}

export interface ArticleUserSummary {
  id: string;
  nickname: string | null;
  avatarUrl: string | null;
}

export interface ArticleListItem {
  id: string;
  title: string;
  summary: string | null;
  status: "Published";
  categories: ArticleCategorySummary[];
  coverUrl: string | null;
  author: ArticleUserSummary;
  publishedAt: string | null;
  updatedAt: string;
}

export interface PublicArticle {
  id: string;
  title: string;
  summary: string | null;
  contentHtml: string;
  categories: ArticleCategorySummary[];
  author: ArticleUserSummary;
  publishedAt: string | null;
  coverUrl: string | null;
  createdAt: string;
  updatedAt: string;
  readingAudioResourceId: string | null;
}

export interface ArticleCategory {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  isActive: boolean;
  articleCount: number;
  createdAt: string;
}

export interface ArticleListQuery {
  page: number;
  pageSize: number;
  keyword?: string;
  categoryId?: string;
}

export interface ArticleCategoryListQuery {
  page: number;
  pageSize: number;
  keyword?: string;
}
