import { useEffect, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  FolderTree,
  Plus,
  RotateCcw,
  Search,
} from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Switch } from "@/components/ui/switch.jsx";
import { CategoryDeleteDialog } from "@/features/articleCategories/CategoryDeleteDialog.jsx";
import { CategoryFormDialog } from "@/features/articleCategories/CategoryFormDialog.jsx";
import { CategoryTable } from "@/features/articleCategories/CategoryTable.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import {
  readArticleCategoryFilters,
  writeArticleCategoryFilters,
} from "@/lib/articleFilters.js";
import { useGetArticleCategoriesQuery } from "@/services/articleCategoriesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

function ArticleCategories() {
  useAdminPage("文章分类");
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readArticleCategoryFilters(searchParams);
  const canonicalSearch = writeArticleCategoryFilters(filters).toString();
  const [keyword, setKeyword] = useState(filters.keyword);
  const [dialog, setDialog] = useState(null);
  const [notice, setNotice] = useState(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetArticleCategoriesQuery(filters);
  useEffect(() => {
    setKeyword(filters.keyword);
  }, [filters.keyword]);
  useEffect(() => {
    if (searchParams.toString() !== canonicalSearch)
      setSearchParams(canonicalSearch, { replace: true });
  }, [canonicalSearch, searchParams, setSearchParams]);
  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1))
      setSearchParams(
        writeArticleCategoryFilters({
          ...filters,
          page: Math.max(data.totalPages, 1),
        }),
        { replace: true },
      );
  }, [data, filters, setSearchParams]);

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">内容管理</p>
          <h1 className="mt-1 text-2xl font-semibold">文章分类</h1>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={refetch} disabled={isFetching}>
            <RotateCcw
              aria-hidden="true"
              className={isFetching ? "animate-spin" : undefined}
            />
            {isFetching ? "正在刷新" : "刷新"}
          </Button>
          <Button onClick={() => setDialog({ type: "form", category: null })}>
            <Plus aria-hidden="true" />
            新建分类
          </Button>
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      <form
        className="flex flex-wrap items-end gap-3 border-y py-4"
        onSubmit={(event) => {
          event.preventDefault();
          setSearchParams(
            writeArticleCategoryFilters({
              ...filters,
              keyword: keyword.trim(),
              page: 1,
            }),
          );
        }}
      >
        <div className="grid gap-y-3 gap-x-4 md:grid-cols-[16rem_auto_auto] items-start">
          <div className="space-y-1.5">
            <Label htmlFor="category-keyword">关键词</Label>
            <Input
              id="category-keyword"
              value={keyword}
              maxLength={200}
              onChange={(event) => setKeyword(event.target.value)}
              placeholder="分类名称或 Slug"
            />
          </div>

          <div className="space-y-1.5">
            <Label className="invisible hidden md:block" aria-hidden="true">
              占位
            </Label>
            <div className="flex h-8 items-center gap-2">
              <Switch
                id="include-inactive"
                checked={filters.includeInactive}
                onCheckedChange={(checked) =>
                  setSearchParams(
                    writeArticleCategoryFilters({
                      ...filters,
                      includeInactive: checked,
                      page: 1,
                    }),
                  )
                }
              />
              <Label
                htmlFor="include-inactive"
                className="cursor-pointer font-normal leading-none"
              >
                包含停用分类
              </Label>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label className="invisible hidden md:block" aria-hidden="true">
              操作
            </Label>
            <div className="flex gap-2">
              <Button type="submit">
                <Search aria-hidden="true" className="mr-2 h-4 w-4" /> 应用
              </Button>
              <Button
                type="button"
                variant="outline"
                aria-label="重置筛选"
                onClick={() => setSearchParams(new URLSearchParams())}
              >
                <RotateCcw aria-hidden="true" className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </form>
      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载文章分类">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>
            {error.status === 403 ? "无权管理分类" : "分类加载失败"}
          </AlertTitle>
          <AlertDescription className="mt-2 flex items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有管理员分类权限。"
                : getErrorMessage(error)}
            </span>
            <Button variant="outline" size="sm" onClick={refetch}>
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : data?.items.length === 0 ? (
        <section className="flex min-h-64 flex-col items-center justify-center border-y text-center">
          <FolderTree
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2 className="mt-4 text-sm font-medium">
            {filters.keyword ? "没有符合条件的分类" : "暂无文章分类"}
          </h2>
        </section>
      ) : data ? (
        <>
          <div
            className="flex min-h-6 items-center justify-between text-sm text-muted-foreground"
            aria-live="polite"
          >
            <span>共 {data.totalCount} 个分类</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <CategoryTable
            categories={data.items}
            onEdit={(category) => setDialog({ type: "form", category })}
            onDelete={(category) => setDialog({ type: "delete", category })}
          />
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="分类列表分页"
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
                    writeArticleCategoryFilters({
                      ...filters,
                      page: data.page - 1,
                    }),
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
                    writeArticleCategoryFilters({
                      ...filters,
                      page: data.page + 1,
                    }),
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
      {dialog?.type === "form" ? (
        <CategoryFormDialog
          category={dialog.category}
          onClose={() => setDialog(null)}
          onDone={setNotice}
        />
      ) : null}
      {dialog?.type === "delete" ? (
        <CategoryDeleteDialog
          category={dialog.category}
          onClose={() => setDialog(null)}
          onDone={setNotice}
        />
      ) : null}
    </div>
  );
}

export default ArticleCategories;
