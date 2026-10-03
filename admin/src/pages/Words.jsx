import { useEffect, useState } from "react";
import {
  BookOpen,
  ChevronLeft,
  ChevronRight,
  FileJson,
  Plus,
  RotateCcw,
} from "lucide-react";
import {
  Link,
  useLocation,
  useNavigate,
  useSearchParams,
} from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { WordBulkDeleteDialog } from "@/features/words/WordBulkDeleteDialog.jsx";
import { WordDeleteDialog } from "@/features/words/WordDeleteDialog.jsx";
import { WordFilters } from "@/features/words/WordFilters.jsx";
import { WordTable } from "@/features/words/WordTable.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { readWordFilters, writeWordFilters } from "@/lib/wordFilters.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { BulkResourceActions } from "@/features/shared/BulkResourceActions.jsx";
import { useLazyGetAdminWordsQuery, useDeleteWordMutation, useGetAdminWordsQuery } from "@/services/wordsApi.js";

function Words() {
  useAdminPage("单词管理");
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readWordFilters(searchParams);
  const canonicalSearch = writeWordFilters(filters).toString();
  const [loadAllPage] = useLazyGetAdminWordsQuery();
  const [deleteAllItem] = useDeleteWordMutation();
  const [selected, setSelected] = useState({});
  const [bulkWords, setBulkWords] = useState(null);
  const [bulkErrors, setBulkErrors] = useState([]);
  const [pendingWord, setPendingWord] = useState(null);
  const [notice, setNotice] = useState(() => location.state?.notice ?? null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetAdminWordsQuery(filters);
  useEffect(() => {
    if (location.state?.notice)
      navigate(`${location.pathname}${location.search}`, {
        replace: true,
        state: null,
      });
  }, [location.pathname, location.search, location.state, navigate]);
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
    filters.keyword || filters.partOfSpeech || filters.definition,
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
              批量导入
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
      <BulkResourceActions label="单词" allOnly
        loadPage={page => loadAllPage({ page, pageSize: 100 }, false).unwrap()}
        remove={word => deleteAllItem({ wordId: word.id, concurrencyStamp: word.concurrencyStamp }).unwrap()}
        onClear={() => setSelected({})} onDone={() => { void refetch(); }}
        description="单词的释义、例句及相关学习记录也会删除；音频库文件不会一并删除。" />
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
              : "新建第一个单词开始录入。"}
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
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm">已选 {Object.keys(selected).length} 个（可跨页勾选）</span>
            <Button variant="outline" disabled={!Object.keys(selected).length || bulkWords !== null} onClick={() => setSelected({})}>清空选择</Button>
            <Button variant="destructive" disabled={!Object.keys(selected).length || bulkWords !== null} onClick={() => setBulkWords(Object.values(selected))}>批量删除</Button>
          </div>
          {bulkErrors.length ? <Alert variant="destructive"><AlertTitle>以下单词未删除，请核对后重新勾选重试</AlertTitle><AlertDescription>
            <ul>{bulkErrors.map(({ word, message }) => <li key={word.id}>{word.headword}：{message}</li>)}</ul>
          </AlertDescription></Alert> : null}
          <WordTable
            selected={selected}
            selectionDisabled={bulkWords !== null}
            onSelect={(word, checked) => setSelected(previous => { const next = { ...previous }; if (checked) next[word.id] = word; else delete next[word.id]; return next; })}
            onSelectPage={checked => setSelected(previous => { const next = { ...previous }; for (const word of data.items) { if (checked) next[word.id] = word; else delete next[word.id]; } return next; })}
            words={data.items}
            onDelete={(word) => {
              setNotice(null);
              setPendingWord(word);
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
      {bulkWords ? <WordBulkDeleteDialog words={bulkWords} onClose={() => setBulkWords(null)}
        onDone={(removed, failed) => { setSelected({}); setBulkErrors(failed); setNotice(`已删除 ${removed} 个单词，${failed.length} 个未删除。`); void refetch(); }} /> : null}
      {pendingWord ? (
        <WordDeleteDialog
          word={pendingWord}
          onClose={() => setPendingWord(null)}
          onDone={setNotice}
          onConflict={refetch}
        />
      ) : null}
    </div>
  );
}

export default Words;
