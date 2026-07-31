import { useEffect, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Plus,
  RotateCcw,
  Video,
} from "lucide-react";
import { Link, useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { VideoActionDialog } from "@/features/videos/VideoActionDialog.jsx";
import { VideoFilters } from "@/features/videos/VideoFilters.jsx";
import { VideoTable } from "@/features/videos/VideoTable.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { readVideoFilters, writeVideoFilters } from "@/lib/videoFilters.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAllVideoCategoryOptionsQuery } from "@/services/videoCategoriesApi.js";
import { useGetAdminVideosQuery } from "@/services/videosApi.js";
import { useGetAdminUsersQuery } from "@/services/usersApi.js";

function Videos() {
  useAdminPage("视频管理");
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readVideoFilters(searchParams);
  const canonical = writeVideoFilters(filters).toString();
  const [action, setAction] = useState(null);
  const [notice, setNotice] = useState(null);
  const { data, error, isLoading, isFetching, refetch } =
    useGetAdminVideosQuery(filters);
  const { data: categories = [] } = useGetAllVideoCategoryOptionsQuery();
  const { data: creatorPage } = useGetAdminUsersQuery({
    page: 1,
    pageSize: 100,
    keyword: "",
    role: "Admin",
    status: "",
  });
  useEffect(() => {
    if (searchParams.toString() !== canonical)
      setSearchParams(canonical, { replace: true });
  }, [canonical, searchParams, setSearchParams]);
  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1))
      setSearchParams(
        writeVideoFilters({ ...filters, page: Math.max(data.totalPages, 1) }),
        { replace: true },
      );
  }, [data, filters, setSearchParams]);
  const hasFilters = Boolean(
    filters.keyword ||
    filters.processingStatus ||
    filters.publicationStatus ||
    filters.categoryId ||
    filters.createdById,
  );
  return (
    <div className="min-w-0 max-w-full space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">内容管理</p>
          <h1 className="mt-1 text-2xl font-semibold">视频管理</h1>
        </div>
        <div className="flex w-full flex-wrap gap-2 sm:w-auto sm:justify-end">
          <Button variant="outline" onClick={refetch} disabled={isFetching}>
            <RotateCcw
              aria-hidden="true"
              className={isFetching ? "animate-spin" : undefined}
            />
            {isFetching ? "正在刷新" : "刷新"}
          </Button>
          <Button asChild>
            <Link to="/videos/new">
              <Plus aria-hidden="true" />
              上传视频
            </Link>
          </Button>
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      <VideoFilters
        filters={filters}
        categories={categories}
        creators={creatorPage?.items ?? []}
        onApply={(next) =>
          setSearchParams(writeVideoFilters({ ...filters, ...next, page: 1 }))
        }
        onReset={() => setSearchParams(new URLSearchParams())}
      />
      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载视频列表">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>
            {error.status === 403 ? "无权查看视频" : "视频列表加载失败"}
          </AlertTitle>
          <AlertDescription className="mt-2 flex items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有视频管理权限。"
                : getErrorMessage(error)}
            </span>
            <Button variant="outline" size="sm" onClick={refetch}>
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : data?.items.length === 0 ? (
        <section className="flex min-h-64 flex-col items-center justify-center border-y text-center">
          <Video aria-hidden="true" className="size-8 text-muted-foreground" />
          <h2 className="mt-4 text-sm font-medium">
            {hasFilters ? "没有符合条件的视频" : "暂无视频"}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters
              ? "调整或清除筛选条件后重试。"
              : "上传源文件并创建第一个视频。"}
          </p>
          {hasFilters ? (
            <Button
              className="mt-4"
              variant="outline"
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
            <span>共 {data.totalCount} 个视频</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <VideoTable
            videos={data.items}
            onAction={(nextAction, video) =>
              setAction({ action: nextAction, video })
            }
          />
          <nav
            className="flex items-center justify-between gap-3"
            aria-label="视频列表分页"
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
                    writeVideoFilters({ ...filters, page: data.page - 1 }),
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
                    writeVideoFilters({ ...filters, page: data.page + 1 }),
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
      {action ? (
        <VideoActionDialog
          action={action.action}
          video={action.video}
          onClose={() => setAction(null)}
          onDone={setNotice}
          onConflict={refetch}
        />
      ) : null}
    </div>
  );
}

export default Videos;
