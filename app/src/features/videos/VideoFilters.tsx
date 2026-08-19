import { useState, type FormEvent } from "react";
import { ChevronDown, ChevronUp, RotateCcw, Search, X } from "lucide-react";
import { useGetVideoCategoriesQuery } from "@/features/videos/videoApi";
import { VIDEO_CATEGORY_PAGE_SIZE } from "@/features/videos/videoSearchParams";
import type { VideoCategory } from "@/features/videos/videoTypes";
import { getVideoErrorMessage } from "@/features/videos/videoUtils";
import { cn } from "@/lib/utils";

interface VideoFiltersProps {
  categories: VideoCategory[];
  categoriesLoading: boolean;
  categoriesError: unknown;
  hasMoreCategories: boolean;
  keywordDraft: string;
  keywordError: string | null;
  selectedCategoryId: string | null;
  selectedCategoryName: string | null;
  onKeywordChange: (value: string) => void;
  onClearKeyword: () => void;
  onSearch: () => void;
  onSelectCategory: (id: string | null) => void;
  onRetryCategories: () => void;
}

/** Owns video keyword and category controls, including bounded category browsing. */
export default function VideoFilters(props: VideoFiltersProps) {
  const {
    categories,
    categoriesLoading,
    categoriesError,
    hasMoreCategories,
    keywordDraft,
    keywordError,
    selectedCategoryId,
    selectedCategoryName,
    onKeywordChange,
    // onClearKeyword,
    onSearch,
    onSelectCategory,
    onRetryCategories,
  } = props;
  const [expanded, setExpanded] = useState(false);
  const [categoryKeywordDraft, setCategoryKeywordDraft] = useState("");
  const [categoryKeyword, setCategoryKeyword] = useState("");
  const [categoryPage, setCategoryPage] = useState(1);
  const categoryQuery = useGetVideoCategoriesQuery(
    {
      page: categoryPage,
      pageSize: VIDEO_CATEGORY_PAGE_SIZE,
      keyword: categoryKeyword || undefined,
    },
    { skip: !expanded },
  );
  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSearch();
  };
  const handleCategorySearch = (event: FormEvent) => {
    event.preventDefault();
    setCategoryKeyword(categoryKeywordDraft.trim());
    setCategoryPage(1);
  };
  return (
    <section aria-labelledby="video-filter-heading" className="space-y-5">
      <h2 id="video-filter-heading" className="sr-only">
        筛选视频
      </h2>
      <form className="max-w-2xl" onSubmit={handleSubmit}>
        <label className="label pb-1.5" htmlFor="video-keyword">
          <span className="label-text font-medium">搜索视频</span>
        </label>
        <div className="flex flex-col gap-2 sm:flex-row">
          <div className="relative min-w-0 flex-1">
            <input
              aria-describedby={
                keywordError ? "video-keyword-error" : undefined
              }
              aria-invalid={Boolean(keywordError)}
              className="input input-bordered w-full"
              id="video-keyword"
              maxLength={200}
              placeholder="搜索标题或描述"
              type="search"
              value={keywordDraft}
              onChange={(event) => onKeywordChange(event.target.value)}
            />
          </div>
          <button className="btn btn-primary" type="submit">
            <Search aria-hidden="true" className="size-4" />
            搜索
          </button>
          {/* <button
              className="btn btn-ghost"
              disabled={!keywordDraft}
              type="button"
              onClick={onClearKeyword}
            >
              <X aria-hidden="true" className="size-4" />
              清除
            </button> */}
        </div>

        {keywordError ? (
          <p
            id="video-keyword-error"
            className="mt-2 text-sm text-error"
            role="alert"
          >
            {keywordError}
          </p>
        ) : null}
      </form>
      <div>
        <p className="mb-2 text-sm font-medium">视频分类</p>
        <div
          className="flex flex-wrap gap-2"
          role="group"
          aria-label="视频分类"
        >
          <button
            aria-pressed={!selectedCategoryId}
            className={cn(
              "btn btn-sm",
              !selectedCategoryId ? "btn-neutral" : "btn-ghost",
            )}
            type="button"
            onClick={() => onSelectCategory(null)}
          >
            全部视频
          </button>
          {categories.map((category) => (
            <button
              key={category.id}
              aria-pressed={selectedCategoryId === category.id}
              className={cn(
                "btn btn-sm max-w-full",
                selectedCategoryId === category.id
                  ? "btn-neutral"
                  : "btn-ghost",
              )}
              type="button"
              onClick={() => onSelectCategory(category.id)}
            >
              <span className="truncate">{category.name}</span>
              <span className="text-xs opacity-65">{category.videoCount}</span>
            </button>
          ))}
          {selectedCategoryId && !selectedCategoryName ? (
            <button
              aria-pressed="true"
              className="btn btn-neutral btn-sm"
              type="button"
              onClick={() => onSelectCategory(null)}
            >
              已选择分类
              <X aria-hidden="true" className="size-3.5" />
            </button>
          ) : null}
          {hasMoreCategories ? (
            <button
              aria-controls="more-video-categories"
              aria-expanded={expanded}
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={() => setExpanded((value) => !value)}
            >
              更多分类
              {expanded ? (
                <ChevronUp aria-hidden="true" className="size-4" />
              ) : (
                <ChevronDown aria-hidden="true" className="size-4" />
              )}
            </button>
          ) : null}
        </div>
        {categoriesLoading ? (
          <p className="mt-2 text-sm text-base-content/60" role="status">
            正在加载分类...
          </p>
        ) : null}
        {categoriesError ? (
          <div
            className="mt-2 flex flex-wrap items-center gap-2 text-sm text-error"
            role="alert"
          >
            <span>
              {getVideoErrorMessage(categoriesError, "分类加载失败。")}
            </span>
            <button
              className="btn btn-ghost btn-xs"
              type="button"
              onClick={onRetryCategories}
            >
              <RotateCcw aria-hidden="true" className="size-3.5" />
              重试
            </button>
          </div>
        ) : null}
      </div>
      {expanded ? (
        <div
          id="more-video-categories"
          className="space-y-4 border-y border-base-300 py-4"
        >
          <form
            className="flex flex-col gap-2 sm:flex-row"
            onSubmit={handleCategorySearch}
          >
            <label className="sr-only" htmlFor="video-category-keyword">
              搜索视频分类
            </label>
            <input
              className="input input-bordered min-w-0 flex-1"
              id="video-category-keyword"
              maxLength={200}
              placeholder="搜索分类名称或 slug"
              type="search"
              value={categoryKeywordDraft}
              onChange={(event) => setCategoryKeywordDraft(event.target.value)}
            />
            <button className="btn btn-neutral" type="submit">
              <Search aria-hidden="true" className="size-4" />
              搜索分类
            </button>
          </form>
          {categoryQuery.isLoading ? (
            <div
              className="grid gap-2 sm:grid-cols-2"
              aria-label="分类加载中"
              role="status"
            >
              <div className="skeleton h-10" />
              <div className="skeleton h-10" />
            </div>
          ) : categoryQuery.isError ? (
            <div className="flex flex-wrap items-center gap-3" role="alert">
              <p>
                {getVideoErrorMessage(categoryQuery.error, "分类加载失败。")}
              </p>
              <button
                className="btn btn-outline btn-sm"
                type="button"
                onClick={() => categoryQuery.refetch()}
              >
                <RotateCcw aria-hidden="true" className="size-4" />
                重试
              </button>
            </div>
          ) : categoryQuery.data?.items.length ? (
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {categoryQuery.data.items.map((category) => (
                <button
                  key={category.id}
                  aria-pressed={selectedCategoryId === category.id}
                  className={cn(
                    "btn h-auto min-h-10 justify-between px-3 py-2",
                    selectedCategoryId === category.id
                      ? "btn-neutral"
                      : "btn-ghost",
                  )}
                  type="button"
                  onClick={() => onSelectCategory(category.id)}
                >
                  <span className="min-w-0 truncate">{category.name}</span>
                  <span className="text-xs opacity-65">
                    {category.videoCount}
                  </span>
                </button>
              ))}
            </div>
          ) : (
            <p className="text-sm text-base-content/65">没有找到匹配的分类。</p>
          )}
          {categoryQuery.data && categoryQuery.data.totalPages > 1 ? (
            <div className="flex items-center justify-between gap-3">
              <button
                className="btn btn-outline btn-sm"
                disabled={categoryPage <= 1}
                type="button"
                onClick={() => setCategoryPage((value) => value - 1)}
              >
                上一页
              </button>
              <span className="text-sm text-base-content/65">
                第 {categoryPage} / {categoryQuery.data.totalPages} 页
              </span>
              <button
                className="btn btn-outline btn-sm"
                disabled={categoryPage >= categoryQuery.data.totalPages}
                type="button"
                onClick={() => setCategoryPage((value) => value + 1)}
              >
                下一页
              </button>
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}
