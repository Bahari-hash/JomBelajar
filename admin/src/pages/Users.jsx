import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight, RotateCcw, UsersRound } from "lucide-react";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { UserActionDialog } from "@/features/users/UserActionDialog.jsx";
import { UserDetailSheet } from "@/features/users/UserDetailSheet.jsx";
import { UserFilters } from "@/features/users/UserFilters.jsx";
import { UserTable } from "@/features/users/UserTable.jsx";
import { readUserFilters, writeUserFilters } from "@/lib/userFilters.js";
import { clearApiSession } from "@/services/baseApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useGetAdminUsersQuery } from "@/services/usersApi.js";

function Users() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const currentUserId = useSelector((state) => state.auth.user?.id);
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = readUserFilters(searchParams);
  const canonicalSearch = writeUserFilters(filters).toString();
  const [detailUserId, setDetailUserId] = useState(null);
  const [pendingAction, setPendingAction] = useState(null);
  const { data, error, isLoading, isFetching, refetch } = useGetAdminUsersQuery(filters);

  useEffect(() => {
    document.title = "用户管理 | TinyLang 管理后台";
    return () => {
      document.title = "TinyLang 管理后台";
    };
  }, []);

  useEffect(() => {
    if (searchParams.toString() !== canonicalSearch) {
      setSearchParams(canonicalSearch, { replace: true });
    }
  }, [canonicalSearch, searchParams, setSearchParams]);

  useEffect(() => {
    if (data && filters.page > Math.max(data.totalPages, 1)) {
      setSearchParams(
        writeUserFilters({ ...filters, page: Math.max(data.totalPages, 1) }),
        { replace: true },
      );
    }
  }, [data, filters, setSearchParams]);

  const handleApplyFilters = (nextFilters) => {
    setSearchParams(writeUserFilters({ ...filters, ...nextFilters, page: 1 }));
  };

  const handleResetFilters = () => setSearchParams(new URLSearchParams());
  const handlePageChange = (page) =>
    setSearchParams(writeUserFilters({ ...filters, page }));

  const handleAction = (action, user) => {
    if (action === "detail") {
      setDetailUserId(user.id);
    } else {
      setPendingAction({ action, user });
    }
  };

  const handleSelfRevoked = () => {
    clearApiSession(dispatch, "全部会话已撤销，请重新登录。");
    navigate("/login", { replace: true, state: { message: "全部会话已撤销，请重新登录。" } });
  };

  const hasFilters = Boolean(filters.keyword || filters.role || filters.status);

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">账户与权限</p>
          <h1 className="mt-1 text-2xl font-semibold">用户管理</h1>
        </div>
        <Button variant="outline" onClick={refetch} disabled={isFetching}>
          <RotateCcw aria-hidden="true" className={isFetching ? "animate-spin" : undefined} />
          {isFetching ? "正在刷新" : "刷新"}
        </Button>
      </header>

      <UserFilters filters={filters} onApply={handleApplyFilters} onReset={handleResetFilters} />

      {isLoading ? (
        <div className="space-y-2" role="status" aria-label="正在加载用户列表">
          <Skeleton className="h-10 w-full" />
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-14 w-full" />
          ))}
        </div>
      ) : error ? (
        <Alert variant="destructive">
          <AlertTitle>{error.status === 403 ? "无权查看用户" : "用户列表加载失败"}</AlertTitle>
          <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
            <span>
              {error.status === 403
                ? "当前账户没有管理员用户查询权限。"
                : getErrorMessage(error, "请检查网络后重试。")}
            </span>
            <Button variant="outline" size="sm" onClick={refetch}>
              <RotateCcw aria-hidden="true" />
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : data?.items.length === 0 ? (
        <section className="flex min-h-64 flex-col items-center justify-center border-y px-4 text-center">
          <UsersRound aria-hidden="true" className="size-8 text-muted-foreground" />
          <h2 className="mt-4 text-sm font-medium">
            {hasFilters ? "没有符合条件的用户" : "暂无用户"}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {hasFilters ? "调整或清除筛选条件后重试。" : "后端当前没有返回用户记录。"}
          </p>
          {hasFilters ? (
            <Button variant="outline" className="mt-4" onClick={handleResetFilters}>
              清除筛选
            </Button>
          ) : null}
        </section>
      ) : data ? (
        <>
          <div className="flex min-h-6 flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground" aria-live="polite">
            <span>共 {data.totalCount} 位用户</span>
            {isFetching ? <span>正在更新列表</span> : null}
          </div>
          <UserTable users={data.items} currentUserId={currentUserId} onAction={handleAction} />
          <nav className="flex items-center justify-between gap-3" aria-label="用户列表分页">
            <p className="text-sm text-muted-foreground">
              第 {data.page} / {Math.max(data.totalPages, 1)} 页
            </p>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={data.page <= 1 || isFetching}
                onClick={() => handlePageChange(data.page - 1)}
              >
                <ChevronLeft aria-hidden="true" />
                上一页
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={data.page >= data.totalPages || isFetching}
                onClick={() => handlePageChange(data.page + 1)}
              >
                下一页
                <ChevronRight aria-hidden="true" />
              </Button>
            </div>
          </nav>
        </>
      ) : null}

      <UserDetailSheet
        userId={detailUserId}
        open={Boolean(detailUserId)}
        onOpenChange={(open) => !open && setDetailUserId(null)}
      />

      {pendingAction ? (
        <UserActionDialog
          key={`${pendingAction.action}:${pendingAction.user.id}`}
          action={pendingAction.action}
          user={pendingAction.user}
          currentUserId={currentUserId}
          onClose={() => setPendingAction(null)}
          onSelfRevoked={handleSelfRevoked}
        />
      ) : null}
    </div>
  );
}

export default Users;
