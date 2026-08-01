import { useEffect, useState } from "react";
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
  FileJson,
  Plus,
  RotateCcw,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { WordActionDialog } from "@/features/words/WordActionDialog.jsx";
import { WordFilters } from "@/features/words/WordFilters.jsx";
import { WordTable } from "@/features/words/WordTable.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { readWordFilters, writeWordFilters } from "@/lib/wordFilters.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAdminWordsQuery } from "@/services/wordsApi.js";

function Words() {
  useAdminPage("单词管理");
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readWordFilters(searchParams);
  const canonicalSearch = writeWordFilters(filters).toString();
  const [pendingAction, setPendingAction] = useState(null);
  const [notice, setNotice] = useState(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetAdminWordsQuery(filters);
  useEffect(() => {
    if (searchParams.toString() !== canonicalSearch)
      setSearchParams(canonicalSearch, { replace: true });
  }, [canonicalSearch, searchParams, setSearchParams]);
  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1))
      setSearchParams(
        writeWordFilters({ ...filters, page: Math.max(data.totalPages, 1) }),
        { replace: true },
      );
  }, [data, filters, setSearchParams]);
  const hasFilters = Boolean(
    filters.keyword ||
    filters.language ||
    filters.status ||
    filters.partOfSpeech ||
    filters.definition,
  );
  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">内容管理</p>
          <h1 className="mt-1 text-2xl font-semibold">单词管理</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={refetch} disabled={isFetching}>
            <RotateCcw
              aria-hidden="true"
              className={isFetching ? "animate-spin" : undefined}
            />
            {isFetching ? "正在刷新" : "刷新"}
          </Button>
          <Button variant="outline" asChild>
            <Link to="/words/batch">
              <FileJson aria-hidden="true" />
              批量录入
            </Link>
          </Button>
          <Button asChild>
            <Link to="/words/new">
              <Plus aria-hidden="true" />
              新建单词
            </Link>
          </Button>
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      <WordFilters
        filters={filters}
        onApply={(next) =>
          setSearchParams(writeWordFilters({ ...filters, ...next, page: 1 }))
        }
        onReset={() => setSearchParams(new URLSearchParams())}
      />
      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载单词列表">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>
            {error.status === 403 ? "无权查看单词" : "单词列表加载失败"}
          </AlertTitle>
          <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有单词管理权限。"
                : getErrorMessage(error)}
            </span>
            <Button variant="outline" size="sm" onClick={refetch}>
              <RotateCcw aria-hidden="true" />
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : data?.items.length === 0 ? (
        <section className="flex min-h-64 flex-col items-center justify-center border-y px-4 text-center">
          <BookOpen
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2 className="mt-4 text-sm font-medium">
            {hasFilters ? "没有符合条件的单词" : "暂无单词"}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? "调整或清除筛选条件后重试。"
              : "新建第一个单词草稿开始录入。"}
          </p>
          {hasFilters ? (
            <Button
              variant="outline"
              className="mt-4"
              onClick={() => setSearchParams(new URLSearchParams())}
            >
              清除筛选
            </Button>
          ) : null}
        </section>
      ) : data ? (
        <>
          <div
            className="flex min-h-6 items-center justify-between text-sm text-muted-foreground"
            aria-live="polite"
          >
            <span>共 {data.totalCount} 个单词</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <WordTable
            words={data.items}
            onAction={(action, word) => {
              setNotice(null);
              setPendingAction({ action, word });
            }}
          />
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="单词列表分页"
          >
            <p className="text-sm text-muted-foreground">
              第 {data.page} / {Math.max(data.totalPages, 1)} 页
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={data.page <= 1 || isFetching}
                onClick={() =>
                  setSearchParams(
                    writeWordFilters({ ...filters, page: data.page - 1 }),
                  )
                }
              >
                <ChevronLeft aria-hidden="true" />
                上一页
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={data.page >= data.totalPages || isFetching}
                onClick={() =>
                  setSearchParams(
                    writeWordFilters({ ...filters, page: data.page + 1 }),
                  )
                }
              >
                下一页
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </nav>
        </>
      ) : null}
      {pendingAction ? (
        <WordActionDialog
          action={pendingAction.action}
          word={pendingAction.word}
          onClose={() => setPendingAction(null)}
          onDone={setNotice}
          onConflict={refetch}
        />
      ) : null}
    </div>
  );
}

export default Words;
