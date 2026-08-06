import { useEffect, useRef, useState } from "react";
import { BookOpenText, RotateCcw, SearchX } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import ArticleCard from "@/features/articles/ArticleCard";
import ArticleFilters from "@/features/articles/ArticleFilters";
import ArticlePagination from "@/features/articles/ArticlePagination";
import {
  useGetArticleCategoriesQuery,
  useGetArticlesQuery,
} from "@/features/articles/articleApi";
import {
  ARTICLE_PAGE_SIZE,
  createArticleSearchParams,
  hasControlCharacters,
  INITIAL_CATEGORY_PAGE_SIZE,
  MAX_ARTICLE_KEYWORD_LENGTH,
  parseArticleSearchParams,
  type ArticleSearchState,
} from "@/features/articles/articleSearchParams";
import { getArticleErrorMessage } from "@/features/articles/articleUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function ArticlesPage() {
  useDocumentTitle("文章");
  const [searchParams, setSearchParams] = useSearchParams();
  const parsedSearch = parseArticleSearchParams(searchParams);
  const { keyword, categoryId, page } = parsedSearch.state;
  const [keywordDraft, setKeywordDraft] = useState(keyword);
  const [keywordError, setKeywordError] = useState<string | null>(null);
  const resultsHeadingRef = useRef<HTMLHeadingElement>(null);
  const articleQuery = useGetArticlesQuery({
    page,
    pageSize: ARTICLE_PAGE_SIZE,
    keyword: keyword || undefined,
    categoryId: categoryId || undefined,
  });
  const categoryQuery = useGetArticleCategoriesQuery({
    page: 1,
    pageSize: INITIAL_CATEGORY_PAGE_SIZE,
  });

  useEffect(() => {
    if (parsedSearch.needsNormalization) {
      setSearchParams(parsedSearch.normalizedParams, { replace: true });
    }
  }, [parsedSearch.needsNormalization, parsedSearch.normalizedParams, setSearchParams]);

  useEffect(() => {
    setKeywordDraft(keyword);
    setKeywordError(null);
  }, [keyword]);

  useEffect(() => {
    const totalPages = articleQuery.data?.totalPages;
    if (totalPages && page > totalPages) {
      setSearchParams(
        createArticleSearchParams({ keyword, categoryId, page: totalPages }),
        { replace: true },
      );
    } else if (totalPages === 0 && page !== 1) {
      setSearchParams(
        createArticleSearchParams({ keyword, categoryId, page: 1 }),
        { replace: true },
      );
    }
  }, [articleQuery.data?.totalPages, categoryId, keyword, page, setSearchParams]);

  const updateSearch = (nextState: ArticleSearchState, replace = false) => {
    setSearchParams(createArticleSearchParams(nextState), { replace });
  };

  const handleSearch = () => {
    const normalizedKeyword = keywordDraft.trim();
    if (normalizedKeyword.length > MAX_ARTICLE_KEYWORD_LENGTH) {
      setKeywordError("搜索关键词不能超过 200 个字符。");
      return;
    }
    if (hasControlCharacters(normalizedKeyword)) {
      setKeywordError("搜索关键词包含无效字符。");
      return;
    }
    setKeywordError(null);
    updateSearch({ keyword: normalizedKeyword, categoryId, page: 1 });
  };

  const handlePageChange = (nextPage: number) => {
    updateSearch({ keyword, categoryId, page: nextPage });
    requestAnimationFrame(() => {
      resultsHeadingRef.current?.focus({ preventScroll: true });
      resultsHeadingRef.current?.scrollIntoView({ block: "start" });
    });
  };

  const initialCategories = categoryQuery.data?.items ?? [];
  const resultCategory = articleQuery.data?.items
    .flatMap((article) => article.categories)
    .find((category) => category.id === categoryId);
  const selectedCategory = initialCategories.find(
    (category) => category.id === categoryId,
  );
  const selectedCategoryName = selectedCategory?.name ?? resultCategory?.name ?? null;
  const hasFilters = Boolean(keyword || categoryId);
  const currentListPath = `/articles${searchParams.size ? `?${searchParams}` : ""}`;

  return (
    <div className="space-y-8">
      <header className="max-w-3xl">
        <div className="flex items-center gap-3">
          <span className="grid size-10 place-items-center rounded-md bg-primary text-primary-content">
            <BookOpenText aria-hidden="true" className="size-5" />
          </span>
          <h1 className="text-3xl font-bold">文章</h1>
        </div>
        <p className="mt-3 leading-7 text-base-content/70">
          阅读真实语境中的外语内容，积累更自然的词汇和表达。
        </p>
      </header>

      <ArticleFilters
        categories={initialCategories}
        categoriesError={categoryQuery.error}
        categoriesLoading={categoryQuery.isLoading}
        hasMoreCategories={(categoryQuery.data?.totalPages ?? 0) > 1}
        keywordDraft={keywordDraft}
        keywordError={keywordError}
        selectedCategoryId={categoryId}
        selectedCategoryName={selectedCategoryName}
        onClearKeyword={() => {
          setKeywordDraft("");
          setKeywordError(null);
          updateSearch({ keyword: "", categoryId, page: 1 });
        }}
        onKeywordChange={(value) => {
          setKeywordDraft(value);
          if (keywordError) setKeywordError(null);
        }}
        onRetryCategories={() => categoryQuery.refetch()}
        onSearch={handleSearch}
        onSelectCategory={(nextCategoryId) =>
          updateSearch({ keyword, categoryId: nextCategoryId, page: 1 })
        }
      />

      <section aria-labelledby="article-results-heading" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2
              ref={resultsHeadingRef}
              id="article-results-heading"
              className="text-xl font-bold outline-none"
              tabIndex={-1}
            >
              阅读列表
            </h2>
            <p className="mt-1 text-sm text-base-content/65" aria-live="polite">
              {articleQuery.data
                ? `${hasFilters ? "当前筛选找到" : "共"} ${articleQuery.data.totalCount} 篇文章${selectedCategoryName ? ` · ${selectedCategoryName}` : ""}${keyword ? ` · “${keyword}”` : ""}`
                : "正在获取文章数量..."}
            </p>
          </div>
          {articleQuery.isFetching && !articleQuery.isLoading ? (
            <span className="loading loading-spinner loading-sm" aria-label="正在刷新文章" role="status" />
          ) : null}
        </div>

        {articleQuery.isLoading ? (
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3" aria-label="文章加载中" role="status">
            {Array.from({ length: 6 }, (_, index) => (
              <div key={index} className="overflow-hidden rounded-lg border border-base-300">
                <div className="skeleton aspect-video w-full rounded-none" />
                <div className="space-y-3 p-5">
                  <div className="skeleton h-5 w-1/3" />
                  <div className="skeleton h-7 w-4/5" />
                  <div className="skeleton h-16 w-full" />
                </div>
              </div>
            ))}
          </div>
        ) : articleQuery.isError && !articleQuery.data ? (
          <div className="border-y border-base-300 py-12 text-center" role="alert">
            <p className="font-semibold">{getArticleErrorMessage(articleQuery.error)}</p>
            <button className="btn btn-outline btn-sm mt-4" type="button" onClick={() => articleQuery.refetch()}>
              <RotateCcw aria-hidden="true" className="size-4" />
              重新加载
            </button>
          </div>
        ) : articleQuery.data?.items.length ? (
          <>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {articleQuery.data.items.map((article) => (
                <ArticleCard key={article.id} article={article} listPath={currentListPath} />
              ))}
            </div>
            <ArticlePagination
              page={page}
              totalPages={articleQuery.data.totalPages}
              onPageChange={handlePageChange}
            />
          </>
        ) : (
          <div className="border-y border-base-300 py-12 text-center">
            <SearchX aria-hidden="true" className="mx-auto size-10 text-base-content/45" />
            <h3 className="mt-4 text-lg font-semibold">
              {hasFilters ? "没有找到匹配的文章" : "暂时没有公开文章"}
            </h3>
            <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-base-content/65">
              {hasFilters
                ? "可以清除当前筛选，或者换一个关键词重新搜索。"
                : "文章发布后会显示在这里，请稍后再来看看。"}
            </p>
            {hasFilters ? (
              <button
                className="btn btn-outline btn-sm mt-4"
                type="button"
                onClick={() => {
                  setKeywordDraft("");
                  updateSearch({ keyword: "", categoryId: null, page: 1 });
                }}
              >
                <RotateCcw aria-hidden="true" className="size-4" />
                清除筛选
              </button>
            ) : null}
          </div>
        )}
      </section>
    </div>
  );
}
