import { ArrowLeft, CalendarDays, ListCheck, RefreshCw } from "lucide-react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  useGetPaperQuery,
  useStartAttemptMutation,
} from "@/features/papers/paperApi";
import { isPaperGuid } from "@/features/papers/paperSearchParams";
import {
  formatPaperDate,
  getPaperErrorMessage,
  isPaperNotFoundError,
} from "@/features/papers/paperUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

function getSafeReturnPath(state: unknown) {
  if (typeof state !== "object" || state === null || !("from" in state))
    return "/papers";
  const from = state.from;
  return typeof from === "string" && /^\/papers(?:\?[^#]*)?$/.test(from)
    ? from
    : "/papers";
}

function Skeleton() {
  return (
    <div
      aria-label="试卷详情加载中"
      className="mx-auto max-w-4xl space-y-6"
      role="status"
    >
      <div className="skeleton h-5 w-28" />
      <div className="skeleton h-10 w-4/5" />
      <div className="skeleton h-6 w-full max-w-2xl" />
      <div className="skeleton h-32 w-full" />
    </div>
  );
}

export default function PaperDetailPage() {
  const { paperId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const validId = isPaperGuid(paperId) ? paperId : null;
  const query = useGetPaperQuery(validId ?? "", { skip: !validId });
  const [startAttempt, startState] = useStartAttemptMutation();
  const notFound = !validId || isPaperNotFoundError(query.error);
  const returnPath = getSafeReturnPath(location.state);
  useDocumentTitle(query.data?.title ?? (notFound ? "试卷不存在" : "试卷详情"));

  if (notFound)
    return (
      <section className="mx-auto max-w-xl py-14 text-center">
        <p className="text-sm font-semibold text-error">404</p>
        <h1 className="mt-2 text-2xl font-bold">试卷不存在或已下架</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          该试卷当前无法访问，你可以返回试卷目录继续浏览。
        </p>
        <Link className="btn btn-primary mt-6" to={returnPath}>
          <ArrowLeft aria-hidden="true" className="size-4" />
          返回试卷目录
        </Link>
      </section>
    );
  if (query.isLoading) return <Skeleton />;
  if (query.isError || !query.data)
    return (
      <section className="mx-auto max-w-xl py-14 text-center" role="alert">
        <h1 className="text-2xl font-bold">试卷加载失败</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          {getPaperErrorMessage(query.error)}
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => query.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重新加载
          </button>
          <Link className="btn btn-ghost" to={returnPath}>
            返回试卷目录
          </Link>
        </div>
      </section>
    );

  const paper = query.data;
  const handleStart = async () => {
    const attempt = await startAttempt(paper.id).unwrap();
    navigate(`/paper-attempts/${attempt.id}`);
  };
  return (
    <div className="mx-auto max-w-4xl">
      <Link className="btn btn-ghost btn-sm -ml-3" to={returnPath}>
        <ArrowLeft aria-hidden="true" className="size-4" />
        返回试卷目录
      </Link>
      <article className="mt-3 overflow-hidden rounded-lg border border-base-300 bg-base-100 p-5 sm:p-8">
        <header className="flex flex-col gap-4">
          <div className="flex items-center gap-3">
            <span className="grid size-10 place-items-center rounded-md bg-primary text-primary-content">
              <ListCheck aria-hidden="true" className="size-5" />
            </span>
            <span className="badge badge-ghost">客观题</span>
          </div>
          <h1 className="wrap-break-word text-3xl font-bold leading-tight sm:text-4xl">
            {paper.title}
          </h1>
          {paper.description?.trim() ? (
            <p className="max-w-3xl text-lg leading-8 text-base-content/70">
              {paper.description}
            </p>
          ) : null}
          {(paper.categories ?? []).length > 0 ? (
            <div className="flex flex-wrap gap-2" aria-label="试卷分类">
              {(paper.categories ?? []).map((category) => (
                <span
                  key={category.id}
                  className="badge badge-outline hover:bg-base-200"
                >
                  {category.name}
                </span>
              ))}
            </div>
          ) : null}
          <div className="flex flex-wrap items-center gap-x-5 gap-y-3 border-y border-base-300 py-4 text-sm">
            <span className="inline-flex items-center gap-1.5">
              <ListCheck aria-hidden="true" className="size-4" />
              {paper.questionCount} 道题
            </span>
            <span>总分 {paper.totalScore}</span>
            <span>及格分 {paper.passingScore}</span>
            <span className="inline-flex items-center gap-1.5 text-base-content/65">
              <CalendarDays aria-hidden="true" className="size-4" />
              发布于 {formatPaperDate(paper.publishedAt)}
            </span>
          </div>
        </header>
        <div className="mt-8 grid gap-6 lg:grid-cols-[minmax(0,1fr)_16rem]">
          <section className="min-w-0">
            <h2 className="text-xl font-bold">考试说明</h2>
            {paper.instructions?.trim() ? (
              <p className="mt-3 whitespace-pre-wrap wrap-break-word leading-7 text-base-content/75">
                {paper.instructions}
              </p>
            ) : (
              <p className="mt-3 leading-7 text-base-content/65">
                本试卷包含选择题、判断题和填空题，提交后由系统自动判分。
              </p>
            )}
          </section>
          <aside className="rounded-lg border border-base-300 bg-base-200/40 p-4">
            <p className="text-sm text-base-content/65">
              准备好后开始本次练习。
            </p>
            <button
              className="btn btn-primary mt-4 w-full"
              disabled={startState.isLoading}
              type="button"
              onClick={handleStart}
            >
              {startState.isLoading ? (
                <>
                  <span className="loading loading-spinner loading-sm" />
                  正在准备测试
                </>
              ) : (
                <>开始测试</>
              )}
            </button>
            {startState.isError ? (
              <p className="mt-3 text-sm text-error" role="alert">
                {getPaperErrorMessage(
                  startState.error,
                  "开始测试失败，请重试。",
                )}{" "}
              </p>
            ) : null}
          </aside>
        </div>
      </article>
    </div>
  );
}
