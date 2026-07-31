import { useEffect, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  FileText,
  Plus,
  RotateCcw,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { ArticleActionDialog } from "@/features/articles/ArticleActionDialog.jsx";
import { ArticleFilters } from "@/features/articles/ArticleFilters.jsx";
import { ArticleTable } from "@/features/articles/ArticleTable.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import {
  readArticleFilters,
  writeArticleFilters,
} from "@/lib/articleFilters.js";
import { useGetAllArticleCategoryOptionsQuery } from "@/services/articleCategoriesApi.js";
import { useGetAdminArticlesQuery } from "@/services/articlesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

function Articles() {
  useAdminPage("文章管理");
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readArticleFilters(searchParams);
  const canonicalSearch = writeArticleFilters(filters).toString();
  const [pendingAction, setPendingAction] = useState(null);
  const [notice, setNotice] = useState(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetAdminArticlesQuery(filters);
  const { data: categories = [] } = useGetAllArticleCategoryOptionsQuery();

  useEffect(() => {
    if (searchParams.toString() !== canonicalSearch)
      setSearchParams(canonicalSearch, { replace: true });
  }, [canonicalSearch, searchParams, setSearchParams]);
  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1))
      setSearchParams(
        writeArticleFilters({ ...filters, page: Math.max(data.totalPages, 1) }),
        { replace: true },
      );
  }, [data, filters, setSearchParams]);

  const hasFilters = Boolean(
    filters.keyword || filters.categoryId || filters.status,
  );
  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">内容管理</p>
          <h1 className="mt-1 text-2xl font-semibold">文章管理</h1>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={refetch} disabled={isFetching}>
            <RotateCcw
              aria-hidden="true"
              className={isFetching ? "animate-spin" : undefined}
            />
            {isFetching ? "正在刷新" : "刷新"}
          </Button>
          <Button asChild>
            <Link to="/articles/new">
              <Plus aria-hidden="true" />
              新建文章
            </Link>
          </Button>
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      <ArticleFilters
        filters={filters}
        categories={categories}
        onApply={(next) =>
          setSearchParams(writeArticleFilters({ ...filters, ...next, page: 1 }))
        }
        onReset={() => setSearchParams(new URLSearchParams())}
      />
      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载文章列表">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>
            {error.status === 403 ? "无权查看文章" : "文章列表加载失败"}
          </AlertTitle>
          <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有文章编辑权限。"
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
          <FileText
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2 className="mt-4 text-sm font-medium">
            {hasFilters ? "没有符合条件的文章" : "暂无文章"}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? "调整或清除筛选条件后重试。"
              : "新建第一篇文章草稿开始编辑。"}
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
            <span>共 {data.totalCount} 篇文章</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <ArticleTable
            articles={data.items}
            onAction={(action, article) => {
              setNotice(null);
              setPendingAction({ action, article });
            }}
          />
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="文章列表分页"
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
                    writeArticleFilters({ ...filters, page: data.page - 1 }),
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
                    writeArticleFilters({ ...filters, page: data.page + 1 }),
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
        <ArticleActionDialog
          action={pendingAction.action}
          article={pendingAction.article}
          onClose={() => setPendingAction(null)}
          onDone={setNotice}
          onConflict={refetch}
        />
      ) : null}
    </div>
  );
}

export default Articles;
