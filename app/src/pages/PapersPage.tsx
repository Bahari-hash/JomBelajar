import { ListCheck, RotateCcw, SearchX } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import PaperCard from "@/features/papers/PaperCard";
import PaperFilters from "@/features/papers/PaperFilters";
import PaperPagination from "@/features/papers/PaperPagination";
import {
  useGetPaperTagsQuery,
  useGetPapersQuery,
} from "@/features/papers/paperApi";
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

const INITIAL_PAPER_TAG_PAGE_SIZE = 6;

export default function PapersPage() {
  useDocumentTitle("在线测试");
  const [searchParams, setSearchParams] = useSearchParams();
  const parsed = parsePaperSearchParams(searchParams);
  const { keyword, tag, page } = parsed.state;
  const [keywordDraft, setKeywordDraft] = useState(keyword);
  const [keywordError, setKeywordError] = useState<string | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const papersQuery = useGetPapersQuery({
    page,
    pageSize: PAPER_PAGE_SIZE,
    keyword: keyword || undefined,
    tag: tag || undefined,
  });
  const tagsQuery = useGetPaperTagsQuery({
    page: 1,
    pageSize: INITIAL_PAPER_TAG_PAGE_SIZE,
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
    const totalPages = papersQuery.data?.totalPages;
    if (totalPages && page > totalPages)
      setSearchParams(
        createPaperSearchParams({ keyword, tag, page: totalPages }),
        { replace: true },
      );
    else if (totalPages === 0 && page !== 1)
      setSearchParams(createPaperSearchParams({ keyword, tag, page: 1 }), {
        replace: true,
      });
  }, [keyword, page, papersQuery.data?.totalPages, setSearchParams, tag]);

  const updateSearch = (state: PaperSearchState, replace = false) =>
    setSearchParams(createPaperSearchParams(state), { replace });
  const submitSearch = () => {
    const next = keywordDraft.trim();
    if (next.length > MAX_PAPER_KEYWORD_LENGTH)
      return setKeywordError("搜索关键词不能超过 200 个字符。");
    if (hasPaperControlCharacters(next))
      return setKeywordError("搜索关键词包含无效字符。");
    setKeywordError(null);
    updateSearch({ keyword: next, tag, page: 1 });
  };
  const hasFilters = Boolean(keyword || tag);
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
        hasMoreTags={(tagsQuery.data?.totalPages ?? 0) > 1}
        keywordDraft={keywordDraft}
        keywordError={keywordError}
        selectedTag={tag}
        tags={tagsQuery.data?.items ?? []}
        tagsError={tagsQuery.error}
        tagsLoading={tagsQuery.isLoading}
        onClearKeyword={() => {
          setKeywordDraft("");
          setKeywordError(null);
          updateSearch({ keyword: "", tag, page: 1 });
        }}
        onKeywordChange={(value) => {
          setKeywordDraft(value);
          if (keywordError) setKeywordError(null);
        }}
        onRetryTags={() => tagsQuery.refetch()}
        onSearch={submitSearch}
        onSelectTag={(nextTag) =>
          updateSearch({ keyword, tag: nextTag, page: 1 })
        }
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
              {papersQuery.data
                ? `${hasFilters ? "当前筛选找到" : "共"} ${papersQuery.data.totalCount} 份试卷${tag ? ` · #${tag}` : ""}${keyword ? ` · “${keyword}”` : ""}`
                : "正在获取试卷数量..."}
            </p>
          </div>
          {papersQuery.isFetching && !papersQuery.isLoading ? (
            <span
              className="loading loading-spinner loading-sm"
              aria-label="正在刷新试卷"
              role="status"
            />
          ) : null}
        </div>
        {papersQuery.isLoading ? (
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
        ) : papersQuery.isError && !papersQuery.data ? (
          <div
            className="border-y border-base-300 py-12 text-center"
            role="alert"
          >
            <p className="font-semibold">
              {getPaperErrorMessage(papersQuery.error)}
            </p>
            <button
              className="btn btn-outline btn-sm mt-4"
              type="button"
              onClick={() => papersQuery.refetch()}
            >
              <RotateCcw aria-hidden="true" className="size-4" />
              重新加载
            </button>
          </div>
        ) : papersQuery.data?.items.length ? (
          <>
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {papersQuery.data.items.map((paper) => (
                <PaperCard key={paper.id} paper={paper} listPath={listPath} />
              ))}
            </div>
            <PaperPagination
              page={page}
              totalPages={papersQuery.data.totalPages}
              onPageChange={(next) => {
                updateSearch({ keyword, tag, page: next });
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
                ? "可以清除当前筛选，或者更换关键词或标签重新搜索。"
                : "试卷发布后会显示在这里，请稍后再来看看。"}
            </p>
            {hasFilters ? (
              <button
                className="btn btn-outline btn-sm mt-4"
                type="button"
                onClick={() => {
                  setKeywordDraft("");
                  setKeywordError(null);
                  updateSearch({ keyword: "", tag: null, page: 1 });
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
