import { useEffect, useRef, useState } from "react";
import { Clapperboard, RotateCcw, SearchX } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import VideoCard from "@/features/videos/VideoCard";
import VideoFilters from "@/features/videos/VideoFilters";
import VideoPagination from "@/features/videos/VideoPagination";
import {
  useGetVideoCategoriesQuery,
  useGetVideosQuery,
} from "@/features/videos/videoApi";
import {
  INITIAL_VIDEO_CATEGORY_PAGE_SIZE,
  VIDEO_PAGE_SIZE,
  createVideoSearchParams,
  hasVideoControlCharacters,
  MAX_VIDEO_KEYWORD_LENGTH,
  parseVideoSearchParams,
  type VideoSearchState,
} from "@/features/videos/videoSearchParams";
import { getVideoErrorMessage } from "@/features/videos/videoUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function VideosPage() {
  useDocumentTitle("视频");
  const [searchParams, setSearchParams] = useSearchParams();
  const parsed = parseVideoSearchParams(searchParams);
  const { keyword, categoryId, page } = parsed.state;
  const [keywordDraft, setKeywordDraft] = useState(keyword);
  const [keywordError, setKeywordError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const videosQuery = useGetVideosQuery({
    page,
    pageSize: VIDEO_PAGE_SIZE,
    keyword: keyword || undefined,
    categoryId: categoryId || undefined,
  });
  const categoriesQuery = useGetVideoCategoriesQuery({
    page: 1,
    pageSize: INITIAL_VIDEO_CATEGORY_PAGE_SIZE,
  });
  useEffect(() => {
    if (parsed.needsNormalization)
      setSearchParams(parsed.normalizedParams, { replace: true });
  }, [parsed.needsNormalization, parsed.normalizedParams, setSearchParams]);
  useEffect(() => {
    setKeywordDraft(keyword);
    setKeywordError(null);
  }, [keyword]);
  useEffect(() => {
    const totalPages = videosQuery.data?.totalPages;
    if (totalPages && page > totalPages)
      setSearchParams(
        createVideoSearchParams({ keyword, categoryId, page: totalPages }),
        { replace: true },
      );
    else if (totalPages === 0 && page !== 1)
      setSearchParams(
        createVideoSearchParams({ keyword, categoryId, page: 1 }),
        { replace: true },
      );
  }, [
    videosQuery.data?.totalPages,
    keyword,
    categoryId,
    page,
    setSearchParams,
  ]);
  const updateSearch = (state: VideoSearchState, replace = false) =>
    setSearchParams(createVideoSearchParams(state), { replace });
  const handleSearch = () => {
    const next = keywordDraft.trim();
    if (next.length > MAX_VIDEO_KEYWORD_LENGTH)
      return setKeywordError("搜索关键词不能超过 200 个字符。");
    if (hasVideoControlCharacters(next))
      return setKeywordError("搜索关键词包含无效字符。");
    setKeywordError(null);
    updateSearch({ keyword: next, categoryId, page: 1 });
  };
  const selectedCategory = categoriesQuery.data?.items.find(
    (item) => item.id === categoryId,
  );
  const resultCategory = videosQuery.data?.items
    .flatMap((item) => item.categories)
    .find((item) => item.id === categoryId);
  const selectedCategoryName =
    selectedCategory?.name ?? resultCategory?.name ?? null;
  const hasFilters = Boolean(keyword || categoryId);
  const listPath = `/videos${searchParams.size ? `?${searchParams}` : ""}`;
  return (
    <div className="space-y-8">
      <header className="max-w-3xl">
        <div className="flex items-center gap-3">
          <span className="grid size-10 place-items-center rounded-md bg-primary text-primary-content">
            <Clapperboard aria-hidden="true" className="size-5" />
          </span>
          <h1 className="text-3xl font-bold">视频</h1>
        </div>
        <p className="mt-3 leading-7 text-base-content/70">
          观看真实语境中的外语视频，在听力与理解之间建立连接。
        </p>
      </header>
      <VideoFilters
        categories={categoriesQuery.data?.items ?? []}
        categoriesError={categoriesQuery.error}
        categoriesLoading={categoriesQuery.isLoading}
        hasMoreCategories={(categoriesQuery.data?.totalPages ?? 0) > 1}
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
        onRetryCategories={() => categoriesQuery.refetch()}
        onSearch={handleSearch}
        onSelectCategory={(next) =>
          updateSearch({ keyword, categoryId: next, page: 1 })
        }
      />
      <section aria-labelledby="video-results-heading" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2
              ref={headingRef}
              id="video-results-heading"
              className="text-xl font-bold outline-none"
              tabIndex={-1}
            >
              观看列表
            </h2>
            <p className="mt-1 text-sm text-base-content/65" aria-live="polite">
              {videosQuery.data
                ? `${hasFilters ? "当前筛选找到" : "共"} ${videosQuery.data.totalCount} 个视频${selectedCategoryName ? ` · ${selectedCategoryName}` : ""}${keyword ? ` · “${keyword}”` : ""}`
                : "正在获取视频数量..."}
            </p>
          </div>
          {videosQuery.isFetching && !videosQuery.isLoading ? (
            <span
              className="loading loading-spinner loading-sm"
              aria-label="正在刷新视频"
              role="status"
            />
          ) : null}
        </div>
        {videosQuery.isLoading ? (
          <div
            className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
            aria-label="视频加载中"
            role="status"
          >
            {Array.from({ length: 6 }, (_, index) => (
              <div
                key={index}
                className="overflow-hidden rounded-lg border border-base-300"
              >
                <div className="skeleton aspect-video w-full rounded-none" />
                <div className="space-y-3 p-5">
                  <div className="skeleton h-5 w-1/3" />
                  <div className="skeleton h-7 w-4/5" />
                  <div className="skeleton h-16 w-full" />
                </div>
              </div>
            ))}
          </div>
        ) : videosQuery.isError && !videosQuery.data ? (
          <div
            className="border-y border-base-300 py-12 text-center"
            role="alert"
          >
            <p className="font-semibold">
              {getVideoErrorMessage(videosQuery.error)}
            </p>
            <button
              className="btn btn-outline btn-sm mt-4"
              type="button"
              onClick={() => videosQuery.refetch()}
            >
              <RotateCcw aria-hidden="true" className="size-4" />
              重新加载
            </button>
          </div>
        ) : videosQuery.data?.items.length ? (
          <>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {videosQuery.data.items.map((video) => (
                <VideoCard key={video.id} listPath={listPath} video={video} />
              ))}
            </div>
            <VideoPagination
              page={page}
              totalPages={videosQuery.data.totalPages}
              onPageChange={(next) => {
                updateSearch({ keyword, categoryId, page: next });
                requestAnimationFrame(() => {
                  headingRef.current?.focus({ preventScroll: true });
                  headingRef.current?.scrollIntoView({ block: "start" });
                });
              }}
            />
          </>
        ) : (
          <div className="border-y border-base-300 py-12 text-center">
            <SearchX
              aria-hidden="true"
              className="mx-auto size-10 text-base-content/45"
            />
            <h3 className="mt-4 text-lg font-semibold">
              {hasFilters ? "没有找到匹配的视频" : "暂时没有公开视频"}
            </h3>
            <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-base-content/65">
              {hasFilters
                ? "可以清除当前筛选，或者换一个关键词重新搜索。"
                : "视频发布后会显示在这里，请稍后再来看看。"}
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
