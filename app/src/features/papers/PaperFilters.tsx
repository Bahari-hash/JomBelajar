import { Search, X } from "lucide-react";
import type { FormEvent } from "react";

interface PaperFiltersProps {
  keywordDraft: string;
  keywordError: string | null;
  onKeywordChange: (value: string) => void;
  onSearch: () => void;
  onClear: () => void;
}

export default function PaperFilters({
  keywordDraft,
  keywordError,
  onKeywordChange,
  onSearch,
  onClear,
}: PaperFiltersProps) {
  const submit = (event: FormEvent) => {
    event.preventDefault();
    onSearch();
  };
  return (
    <section aria-labelledby="paper-filter-heading">
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
                onClick={onClear}
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
    </section>
  );
}
