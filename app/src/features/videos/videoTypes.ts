export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface VideoCategorySummary {
  id: string;
  name: string;
  slug: string;
}

export interface VideoUserSummary {
  id: string;
  nickname: string | null;
  avatarUrl: string | null;
}

export interface VideoCatalogItem {
  id: string;
  title: string;
  description: string | null;
  originalLanguage: string;
  durationSeconds: number;
  author: VideoUserSummary;
  publishedAt: string;
  categories: VideoCategorySummary[];
}

export interface VideoDetails {
  id: string;
  title: string;
  description: string | null;
  originalLanguage: string;
  durationSeconds: number;
  displayWidth: number;
  displayHeight: number;
  author: VideoUserSummary;
  publishedAt: string;
  categories: VideoCategorySummary[];
}

export interface VideoCategory {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  isActive: boolean;
  videoCount: number;
  createdAt: string;
}

export interface VideoPlayback {
  masterPlaylistUrl: string;
  posterUrl: string | null;
  expiresAt: string | null;
  durationSeconds: number;
  positionSeconds: number;
  isCompleted: boolean;
}

export interface VideoCatalogQuery {
  page: number;
  pageSize: number;
  keyword?: string;
  categoryId?: string;
}

export interface VideoCategoryListQuery {
  page: number;
  pageSize: number;
  keyword?: string;
}
