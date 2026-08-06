import { useState, type FormEvent } from "react";
import { ChevronDown, ChevronUp, RotateCcw, Search, X } from "lucide-react";
import { useGetArticleCategoriesQuery } from "@/features/articles/articleApi";
import { CATEGORY_DIALOG_PAGE_SIZE } from "@/features/articles/articleSearchParams";
import type { ArticleCategory } from "@/features/articles/articleTypes";
import { getArticleErrorMessage } from "@/features/articles/articleUtils";
import { cn } from "@/lib/utils";

interface ArticleFiltersProps {
  keywordDraft: string;
  keywordError: string | null;
  categories: ArticleCategory[];
  categoriesLoading: boolean;
  categoriesError: unknown;
  selectedCategoryId: string | null;
  selectedCategoryName: string | null;
  hasMoreCategories: boolean;
  onKeywordChange: (value: string) => void;
  onSearch: () => void;
  onClearKeyword: () => void;
  onSelectCategory: (categoryId: string | null) => void;
  onRetryCategories: () => void;
}

/** Owns article search controls and bounded server-side category browsing. */
export default function ArticleFilters({
  keywordDraft,
  keywordError,
  categories,
  categoriesLoading,
  categoriesError,
  selectedCategoryId,
  selectedCategoryName,
  hasMoreCategories,
  onKeywordChange,
  onSearch,
  // onClearKeyword,
  onSelectCategory,
  onRetryCategories,
}: ArticleFiltersProps) {
  const [expanded, setExpanded] = useState(false);
  const [categoryKeywordDraft, setCategoryKeywordDraft] = useState("");
  const [categoryKeyword, setCategoryKeyword] = useState("");
  const [categoryPage, setCategoryPage] = useState(1);
  const categoryQuery = useGetArticleCategoriesQuery(
    {
      page: categoryPage,
      pageSize: CATEGORY_DIALOG_PAGE_SIZE,
      keyword: categoryKeyword || undefined,
    },
    { skip: !expanded },
  );

  const handleSearchSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSearch();
  };
  const handleCategorySearch = (event: FormEvent) => {
    event.preventDefault();
    setCategoryKeyword(categoryKeywordDraft.trim());
    setCategoryPage(1);
  };

  return (
    <section aria-labelledby="article-filter-heading" className="space-y-5">
      <h2 id="article-filter-heading" className="sr-only">
        筛选文章
      </h2>
      <form className="max-w-2xl" onSubmit={handleSearchSubmit}>
        <label className="label pb-1.5" htmlFor="article-keyword">
          <span className="label-text font-medium">搜索文章</span>
        </label>
        <div className="flex flex-col gap-2 sm:flex-row">
          <div className="relative min-w-0 flex-1">
            {/* <Search
              aria-hidden="true"
              className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-base-content/50"
            /> */}
            <input
              aria-describedby={keywordError ? "article-keyword-error" : undefined}
              aria-invalid={Boolean(keywordError)}
              className="input input-bordered w-full"
              id="article-keyword"
              maxLength={200}
              placeholder="搜索标题或摘要"
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
            id="article-keyword-error"
            className="mt-2 text-sm text-error"
            role="alert"
          >
            {keywordError}
          </p>
        ) : null}
      </form>

      <div>
        <p className="mb-2 text-sm font-medium">文章分类</p>
        <div className="flex flex-wrap gap-2" role="group" aria-label="文章分类">
          <button
            aria-pressed={!selectedCategoryId}
            className={cn(
              "btn btn-sm",
              !selectedCategoryId ? "btn-neutral" : "btn-ghost",
            )}
            type="button"
            onClick={() => onSelectCategory(null)}
          >
            全部文章
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
              <span className="text-xs opacity-65">{category.articleCount}</span>
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
              aria-controls="more-article-categories"
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
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-error" role="alert">
            <span>{getArticleErrorMessage(categoriesError, "分类加载失败。")}</span>
            <button className="btn btn-ghost btn-xs" type="button" onClick={onRetryCategories}>
              <RotateCcw aria-hidden="true" className="size-3.5" />
              重试
            </button>
          </div>
        ) : null}
      </div>

      {expanded ? (
        <div
          id="more-article-categories"
          className="space-y-4 border-y border-base-300 py-4"
        >
          <form className="flex flex-col gap-2 sm:flex-row" onSubmit={handleCategorySearch}>
            <label className="sr-only" htmlFor="category-keyword">
              搜索文章分类
            </label>
            <input
              className="input input-bordered min-w-0 flex-1"
              id="category-keyword"
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
            <div className="grid gap-2 sm:grid-cols-2" aria-label="分类加载中" role="status">
              <div className="skeleton h-10" />
              <div className="skeleton h-10" />
            </div>
          ) : categoryQuery.isError ? (
            <div className="flex flex-wrap items-center gap-3" role="alert">
              <p>{getArticleErrorMessage(categoryQuery.error, "分类加载失败。")}</p>
              <button className="btn btn-outline btn-sm" type="button" onClick={() => categoryQuery.refetch()}>
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
                  <span className="text-xs opacity-65">{category.articleCount}</span>
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
