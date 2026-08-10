import { ListCheck, RotateCcw, SearchX } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import PaperCard from "@/features/papers/PaperCard";
import PaperFilters from "@/features/papers/PaperFilters";
import PaperPagination from "@/features/papers/PaperPagination";
import { useGetPapersQuery } from "@/features/papers/paperApi";
import {
  createPaperSearchParams,
  hasPaperControlCharacters,
  MAX_PAPER_KEYWORD_LENGTH,
  PAPER_PAGE_SIZE,
  parsePaperSearchParams,
  type PaperSearchState,
} from "@/features/papers/paperSearchParams";
import { getPaperErrorMessage } from "@/features/papers/paperUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function PapersPage() {
  useDocumentTitle("在线测试");
  const [searchParams, setSearchParams] = useSearchParams();
  const parsed = parsePaperSearchParams(searchParams);
  const { keyword, page } = parsed.state;
  const [keywordDraft, setKeywordDraft] = useState(keyword);
  const [keywordError, setKeywordError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const query = useGetPapersQuery({
    page,
    pageSize: PAPER_PAGE_SIZE,
    keyword: keyword || undefined,
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
    const totalPages = query.data?.totalPages;
    if (totalPages && page > totalPages)
      setSearchParams(createPaperSearchParams({ keyword, page: totalPages }), {
        replace: true,
      });
    else if (totalPages === 0 && page !== 1)
      setSearchParams(createPaperSearchParams({ keyword, page: 1 }), {
        replace: true,
      });
  }, [keyword, page, query.data?.totalPages, setSearchParams]);

  const updateSearch = (state: PaperSearchState, replace = false) =>
    setSearchParams(createPaperSearchParams(state), { replace });
  const submitSearch = () => {
    const next = keywordDraft.trim();
    if (next.length > MAX_PAPER_KEYWORD_LENGTH)
      return setKeywordError("搜索关键词不能超过 200 个字符。");
    if (hasPaperControlCharacters(next))
      return setKeywordError("搜索关键词包含无效字符。");
    setKeywordError(null);
    updateSearch({ keyword: next, page: 1 });
  };
  const hasFilters = Boolean(keyword);
  const listPath = `/papers${searchParams.size ? `?${searchParams}` : ""}`;

  return (
    <div className="space-y-8">
      <header className="max-w-3xl">
        <div className="flex items-center gap-3">
          <span className="grid size-10 place-items-center rounded-md bg-primary text-primary-content">
            <ListCheck aria-hidden="true" className="size-5" />
          </span>
          <h1 className="text-3xl font-bold">在线测试</h1>
        </div>
        <p className="mt-3 leading-7 text-base-content/70">
          通过选择、判断和填空练习检验外语学习成果。
        </p>
      </header>
      <PaperFilters
        keywordDraft={keywordDraft}
        keywordError={keywordError}
        onKeywordChange={(value) => {
          setKeywordDraft(value);
          if (keywordError) setKeywordError(null);
        }}
        onSearch={submitSearch}
        onClear={() => {
          setKeywordDraft("");
          setKeywordError(null);
          updateSearch({ keyword: "", page: 1 });
        }}
      />
      <section aria-labelledby="paper-results-heading" className="space-y-5">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2
              ref={headingRef}
              id="paper-results-heading"
              className="text-xl font-bold outline-none"
              tabIndex={-1}
            >
              试卷列表
            </h2>
            <p className="mt-1 text-sm text-base-content/65" aria-live="polite">
              {query.data
                ? `${hasFilters ? "当前筛选找到" : "共"} ${query.data.totalCount} 份试卷${keyword ? ` · “${keyword}”` : ""}`
                : "正在获取试卷数量..."}
            </p>
          </div>
          {query.isFetching && !query.isLoading ? (
            <span
              className="loading loading-spinner loading-sm"
              aria-label="正在刷新试卷"
              role="status"
            />
          ) : null}
        </div>
        {query.isLoading ? (
          <div
            className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3"
            aria-label="试卷加载中"
            role="status"
          >
            {Array.from({ length: 6 }, (_, index) => (
              <div
                key={index}
                className="overflow-hidden rounded-lg border border-base-300"
              >
                <div className="skeleton aspect-16/7 w-full rounded-none" />
                <div className="space-y-3 p-5">
                  <div className="skeleton h-5 w-1/3" />
                  <div className="skeleton h-7 w-4/5" />
                  <div className="skeleton h-16 w-full" />
                </div>
              </div>
            ))}
          </div>
        ) : query.isError && !query.data ? (
          <div
            className="border-y border-base-300 py-12 text-center"
            role="alert"
          >
            <p className="font-semibold">{getPaperErrorMessage(query.error)}</p>
            <button
              className="btn btn-outline btn-sm mt-4"
              type="button"
              onClick={() => query.refetch()}
            >
              <RotateCcw aria-hidden="true" className="size-4" />
              重新加载
            </button>
          </div>
        ) : query.data?.items.length ? (
          <>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {query.data.items.map((paper) => (
                <PaperCard key={paper.id} paper={paper} listPath={listPath} />
              ))}
            </div>
            <PaperPagination
              page={page}
              totalPages={query.data.totalPages}
              onPageChange={(next) => {
                updateSearch({ keyword, page: next });
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
              {hasFilters ? "没有找到匹配的试卷" : "暂时没有公开试卷"}
            </h3>
            <p className="mx-auto mt-2 max-w-lg text-sm leading-6 text-base-content/65">
              {hasFilters
                ? "可以清除当前筛选，或者换一个关键词重新搜索。"
                : "试卷发布后会显示在这里，请稍后再来看看。"}
            </p>
            {hasFilters ? (
              <button
                className="btn btn-outline btn-sm mt-4"
                type="button"
                onClick={() => {
                  setKeywordDraft("");
                  updateSearch({ keyword: "", page: 1 });
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
