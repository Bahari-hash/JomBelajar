import type { FormEvent } from "react";
import { RotateCcw, Search, X } from "lucide-react";
import type { PaperCategorySummary } from "@/features/papers/paperTypes";
import { getPaperErrorMessage } from "@/features/papers/paperUtils";
import { cn } from "@/lib/utils";

interface Props {
  keywordDraft: string;
  keywordError: string | null;
  categories: PaperCategorySummary[];
  categoriesLoading: boolean;
  categoriesError: unknown;
  selectedCategoryId: string | null;
  onKeywordChange: (value: string) => void;
  onSearch: () => void;
  onClearKeyword: () => void;
  onSelectCategory: (id: string | null) => void;
  onRetryCategories: () => void;
}

export default function PaperFilters(props: Props) {
  const submit = (event: FormEvent) => {
    event.preventDefault();
    props.onSearch();
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
              aria-invalid={Boolean(props.keywordError)}
              className="input input-bordered w-full"
              type="search"
              maxLength={200}
              value={props.keywordDraft}
              onChange={(event) => props.onKeywordChange(event.target.value)}
            />
            {props.keywordDraft ? (
              <button
                aria-label="清除搜索关键词"
                className="btn btn-ghost btn-square btn-sm absolute right-1 top-1/2 -translate-y-1/2"
                type="button"
                onClick={props.onClearKeyword}
              >
                <X className="size-4" />
              </button>
            ) : null}
          </div>
          <button className="btn btn-primary" type="submit">
            <Search className="size-4" />
            搜索
          </button>
        </div>
        {props.keywordError ? (
          <p className="mt-2 text-sm text-error" role="alert">
            {props.keywordError}
          </p>
        ) : null}
      </form>
      <div>
        <p className="mb-2 text-sm font-medium">试卷分类</p>
        <div
          className="flex flex-wrap gap-2"
          role="group"
          aria-label="按试卷分类筛选"
        >
          <button
            aria-pressed={!props.selectedCategoryId}
            className={cn(
              "btn btn-sm",
              props.selectedCategoryId ? "btn-ghost" : "btn-neutral",
            )}
            type="button"
            onClick={() => props.onSelectCategory(null)}
          >
            全部试卷
          </button>
          {props.categories.map((category) => (
            <button
              key={category.id}
              aria-pressed={props.selectedCategoryId === category.id}
              className={cn(
                "btn btn-sm",
                props.selectedCategoryId === category.id
                  ? "btn-neutral"
                  : "btn-ghost",
              )}
              type="button"
              onClick={() => props.onSelectCategory(category.id)}
            >
              {category.name}
            </button>
          ))}
        </div>
        {props.categoriesLoading ? (
          <p className="mt-2 text-sm text-base-content/60">正在加载分类...</p>
        ) : null}
        {props.categoriesError ? (
          <div
            className="mt-2 flex items-center gap-2 text-sm text-error"
            role="alert"
          >
            <span>
              {getPaperErrorMessage(props.categoriesError, "分类加载失败。")}
            </span>
            <button
              className="btn btn-ghost btn-xs"
              type="button"
              onClick={props.onRetryCategories}
            >
              <RotateCcw className="size-3.5" />
              重试
            </button>
          </div>
        ) : null}
      </div>
    </section>
  );
}
