import { useState, type FormEvent } from "react";
import { ChevronDown, ChevronUp, RotateCcw, Search, X } from "lucide-react";
import { useGetPaperTagsQuery } from "@/features/papers/paperApi";
import type { PaperTagSummary } from "@/features/papers/paperTypes";
import { getPaperErrorMessage } from "@/features/papers/paperUtils";
import { cn } from "@/lib/utils";

const PAPER_TAG_DIRECTORY_PAGE_SIZE = 20;

interface PaperFiltersProps {
  keywordDraft: string;
  keywordError: string | null;
  tags: PaperTagSummary[];
  tagsLoading: boolean;
  tagsError: unknown;
  selectedTag: string | null;
  hasMoreTags: boolean;
  onKeywordChange: (value: string) => void;
  onSearch: () => void;
  onClearKeyword: () => void;
  onSelectTag: (tag: string | null) => void;
  onRetryTags: () => void;
}

/** Owns Paper title search and bounded server-side tag browsing. */
export default function PaperFilters({
  keywordDraft,
  keywordError,
  tags,
  tagsLoading,
  tagsError,
  selectedTag,
  hasMoreTags,
  onKeywordChange,
  onSearch,
  onClearKeyword,
  onSelectTag,
  onRetryTags,
}: PaperFiltersProps) {
  const [expanded, setExpanded] = useState(false);
  const [tagKeywordDraft, setTagKeywordDraft] = useState("");
  const [tagKeyword, setTagKeyword] = useState("");
  const [tagPage, setTagPage] = useState(1);
  const tagQuery = useGetPaperTagsQuery(
    {
      page: tagPage,
      pageSize: PAPER_TAG_DIRECTORY_PAGE_SIZE,
      keyword: tagKeyword || undefined,
    },
    { skip: !expanded },
  );
  const selectedTagIsMissing =
    selectedTag !== null && !tags.some((tag) => tag.name === selectedTag);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSearch();
  };
  const submitTagSearch = (event: FormEvent) => {
    event.preventDefault();
    setTagKeyword(tagKeywordDraft.trim());
    setTagPage(1);
  };

  return (
    <section aria-labelledby="paper-filter-heading" className="space-y-5">
      <h2 id="paper-filter-heading" className="sr-only">
        筛选试卷
      </h2>
      <form role="search" className="max-w-2xl" onSubmit={submit}>
        <label className="label pb-1.5" htmlFor="paper-keyword">
          <span className="label-text font-medium">搜索试卷标题</span>
        </label>
        <div className="flex flex-col gap-2 sm:flex-row">
          <div className="relative min-w-0 flex-1">
            <input
              id="paper-keyword"
              aria-label="搜索试卷标题"
              aria-invalid={Boolean(keywordError)}
              aria-describedby={
                keywordError ? "paper-keyword-error" : undefined
              }
              className="input input-bordered w-full"
              type="search"
              maxLength={200}
              placeholder="搜索试卷标题"
              value={keywordDraft}
              onChange={(event) => onKeywordChange(event.target.value)}
            />
            {keywordDraft ? (
              <button
                aria-label="清除搜索关键词"
                className="btn btn-ghost btn-square btn-sm absolute right-1 top-1/2 -translate-y-1/2"
                type="button"
                onClick={onClearKeyword}
              >
                <X aria-hidden="true" className="size-4" />
              </button>
            ) : null}
          </div>
          <button className="btn btn-primary" type="submit">
            <Search aria-hidden="true" className="size-4" />
            搜索
          </button>
        </div>
        {keywordError ? (
          <p
            id="paper-keyword-error"
            role="alert"
            className="mt-2 text-sm text-error"
          >
            {keywordError}
          </p>
        ) : null}
      </form>

      <div>
        <p className="mb-2 text-sm font-medium">试卷标签</p>
        <div
          className="flex flex-wrap gap-2"
          role="group"
          aria-label="按试卷标签筛选"
        >
          <button
            aria-pressed={!selectedTag}
            className={cn(
              "btn btn-sm",
              selectedTag ? "btn-ghost" : "btn-neutral",
            )}
            type="button"
            onClick={() => onSelectTag(null)}
          >
            全部试卷
          </button>
          {tags.map((tag) => (
            <button
              key={tag.name}
              aria-label={`${tag.name} ${tag.paperCount}`}
              aria-pressed={selectedTag === tag.name}
              className={cn(
                "btn btn-sm max-w-full",
                selectedTag === tag.name ? "btn-neutral" : "btn-ghost",
              )}
              type="button"
              onClick={() => onSelectTag(tag.name)}
            >
              <span className="truncate">{tag.name}</span>
              <span className="text-xs opacity-65">{tag.paperCount}</span>
            </button>
          ))}
          {selectedTagIsMissing ? (
            <button
              aria-pressed="true"
              className="btn btn-neutral btn-sm max-w-full"
              type="button"
              onClick={() => onSelectTag(null)}
            >
              <span className="truncate">{selectedTag}</span>
              <X aria-hidden="true" className="size-3.5" />
            </button>
          ) : null}
          {hasMoreTags ? (
            <button
              aria-controls="more-paper-tags"
              aria-expanded={expanded}
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={() => setExpanded((value) => !value)}
            >
              更多标签
              {expanded ? (
                <ChevronUp aria-hidden="true" className="size-4" />
              ) : (
                <ChevronDown aria-hidden="true" className="size-4" />
              )}
            </button>
          ) : null}
        </div>
        {tagsLoading ? (
          <p className="mt-2 text-sm text-base-content/60" role="status">
            正在加载标签...
          </p>
        ) : null}
        {tagsError ? (
          <div
            className="mt-2 flex flex-wrap items-center gap-2 text-sm text-error"
            role="alert"
          >
            <span>{getPaperErrorMessage(tagsError, "标签加载失败。")}</span>
            <button
              className="btn btn-ghost btn-xs"
              type="button"
              onClick={onRetryTags}
            >
              <RotateCcw aria-hidden="true" className="size-3.5" />
              重试
            </button>
          </div>
        ) : null}
      </div>

      {expanded ? (
        <div
          id="more-paper-tags"
          className="space-y-4 border-y border-base-300 py-4"
        >
          <form
            className="flex flex-col gap-2 sm:flex-row"
            onSubmit={submitTagSearch}
          >
            <label className="sr-only" htmlFor="paper-tag-keyword">
              搜索试卷标签
            </label>
            <input
              id="paper-tag-keyword"
              aria-label="搜索试卷标签"
              className="input input-bordered min-w-0 flex-1"
              maxLength={200}
              placeholder="搜索标签名称"
              type="search"
              value={tagKeywordDraft}
              onChange={(event) => setTagKeywordDraft(event.target.value)}
            />
            <button className="btn btn-neutral" type="submit">
              <Search aria-hidden="true" className="size-4" />
              搜索标签
            </button>
          </form>
          {tagQuery.isLoading ? (
            <div
              className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3"
              aria-label="标签加载中"
              role="status"
            >
              {Array.from({ length: 3 }, (_, index) => (
                <div key={index} className="skeleton h-10" />
              ))}
            </div>
          ) : tagQuery.isError ? (
            <div className="flex flex-wrap items-center gap-3" role="alert">
              <p>{getPaperErrorMessage(tagQuery.error, "标签加载失败。")}</p>
              <button
                className="btn btn-outline btn-sm"
                type="button"
                onClick={() => tagQuery.refetch()}
              >
                <RotateCcw aria-hidden="true" className="size-4" />
                重试
              </button>
            </div>
          ) : tagQuery.data?.items.length ? (
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {tagQuery.data.items.map((tag) => (
                <button
                  key={tag.name}
                  aria-label={`${tag.name} ${tag.paperCount}`}
                  aria-pressed={selectedTag === tag.name}
                  className={cn(
                    "btn h-auto min-h-10 justify-between px-3 py-2",
                    selectedTag === tag.name ? "btn-neutral" : "btn-ghost",
                  )}
                  type="button"
                  onClick={() => onSelectTag(tag.name)}
                >
                  <span className="min-w-0 truncate">{tag.name}</span>
                  <span className="text-xs opacity-65">{tag.paperCount}</span>
                </button>
              ))}
            </div>
          ) : (
            <p className="text-sm text-base-content/65">没有找到匹配的标签。</p>
          )}
          {tagQuery.data && tagQuery.data.totalPages > 1 ? (
            <div className="flex items-center justify-between gap-3">
              <button
                className="btn btn-outline btn-sm"
                disabled={tagPage <= 1}
                type="button"
                onClick={() => setTagPage((value) => value - 1)}
              >
                上一页
              </button>
              <span className="text-sm text-base-content/65">
                第 {tagPage} / {tagQuery.data.totalPages} 页
              </span>
              <button
                className="btn btn-outline btn-sm"
                disabled={tagPage >= tagQuery.data.totalPages}
                type="button"
                onClick={() => setTagPage((value) => value + 1)}
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
