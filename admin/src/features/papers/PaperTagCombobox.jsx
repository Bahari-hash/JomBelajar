import { useRef, useState } from "react";
import {
  Check,
  ChevronLeft,
  ChevronRight,
  ChevronsUpDown,
  RotateCcw,
  Search,
} from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover.jsx";
import { cn } from "@/lib/utils";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAdminPaperTagsQuery } from "@/services/papersApi.js";

const PAGE_SIZE = 20;

/** Searchable single-select control backed by the administrator tag directory. */
export function PaperTagCombobox({ value, onChange }) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState("");
  const [keyword, setKeyword] = useState("");
  const [page, setPage] = useState(1);
  const searchRef = useRef(null);
  const query = useGetAdminPaperTagsQuery(
    { page, pageSize: PAGE_SIZE, keyword: keyword || undefined },
    { skip: !open },
  );

  const submitSearch = (event) => {
    event.preventDefault();
    setKeyword(draft.trim());
    setPage(1);
  };
  const select = (nextValue) => {
    onChange(nextValue);
    setOpen(false);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-label="标签"
          aria-expanded={open}
          className="w-full justify-between font-normal"
        >
          <span className="min-w-0 truncate">{value || "全部标签"}</span>
          <ChevronsUpDown aria-hidden="true" className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="w-[var(--radix-popover-trigger-width)] min-w-72 max-w-[calc(100vw-2rem)] space-y-2"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          searchRef.current?.focus();
        }}
      >
        <form className="flex gap-1.5" onSubmit={submitSearch}>
          <Input
            ref={searchRef}
            aria-label="搜索试卷标签"
            type="search"
            maxLength={200}
            placeholder="搜索标签"
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
          />
          <Button
            type="submit"
            size="icon"
            variant="outline"
            aria-label="搜索标签"
          >
            <Search aria-hidden="true" />
          </Button>
        </form>

        <div
          role="listbox"
          aria-label="试卷标签"
          className="max-h-64 space-y-1 overflow-y-auto"
        >
          <button
            role="option"
            aria-selected={!value}
            className={cn(
              "flex min-h-8 w-full items-center justify-between rounded-md px-2 text-left text-sm outline-none hover:bg-accent focus-visible:bg-accent",
              !value && "bg-accent",
            )}
            type="button"
            onClick={() => select("")}
          >
            <span>全部标签</span>
            {!value ? <Check aria-hidden="true" className="size-4" /> : null}
          </button>
          {query.isLoading ? (
            <div className="space-y-1" role="status" aria-label="正在加载标签">
              <div className="h-8 animate-pulse rounded-md bg-muted" />
              <div className="h-8 animate-pulse rounded-md bg-muted" />
              <div className="h-8 animate-pulse rounded-md bg-muted" />
            </div>
          ) : query.isError ? (
            <div className="space-y-2 p-2 text-sm" role="alert">
              <p>{getErrorMessage(query.error)}</p>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => query.refetch()}
              >
                <RotateCcw aria-hidden="true" />
                重试
              </Button>
            </div>
          ) : query.data?.items.length ? (
            query.data.items.map((tag) => (
              <button
                key={tag.name}
                role="option"
                aria-label={`${tag.name} ${tag.paperCount}`}
                aria-selected={value === tag.name}
                className={cn(
                  "flex min-h-8 w-full items-center justify-between gap-3 rounded-md px-2 text-left text-sm outline-none hover:bg-accent focus-visible:bg-accent",
                  value === tag.name && "bg-accent",
                )}
                type="button"
                onClick={() => select(tag.name)}
              >
                <span className="min-w-0 truncate">{tag.name}</span>
                <span className="flex shrink-0 items-center gap-1.5 text-xs text-muted-foreground">
                  {tag.paperCount}
                  {value === tag.name ? (
                    <Check aria-hidden="true" className="size-4" />
                  ) : null}
                </span>
              </button>
            ))
          ) : (
            <p className="p-2 text-sm text-muted-foreground">
              没有找到匹配的标签。
            </p>
          )}
        </div>

        {query.data && query.data.totalPages > 1 ? (
          <div className="flex items-center justify-between gap-2 border-t pt-2">
            <Button
              type="button"
              size="icon-sm"
              variant="outline"
              aria-label="上一页标签"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
            >
              <ChevronLeft aria-hidden="true" />
            </Button>
            <span className="text-xs text-muted-foreground">
              {page} / {query.data.totalPages}
            </span>
            <Button
              type="button"
              size="icon-sm"
              variant="outline"
              aria-label="下一页标签"
              disabled={page >= query.data.totalPages}
              onClick={() => setPage((current) => current + 1)}
            >
              <ChevronRight aria-hidden="true" />
            </Button>
          </div>
        ) : null}
      </PopoverContent>
    </Popover>
  );
}
