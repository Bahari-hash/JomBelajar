import { ChevronLeft, ChevronRight, X } from "lucide-react";
import { useEffect, useState } from "react";
import {
  useGetReviewExclusionsQuery,
  useRestoreReviewMutation,
} from "./wordStudyApi";

type WordReviewExclusionsPanelProps = {
  modal?: boolean;
  onClose?: () => void;
};

export default function WordReviewExclusionsPanel({
  modal = false,
  onClose,
}: WordReviewExclusionsPanelProps) {
  const [page, setPage] = useState(1);
  const query = useGetReviewExclusionsQuery({ page, pageSize: 20 });
  const [restore, restoreState] = useRestoreReviewMutation();
  const [selected, setSelected] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (query.data && page > query.data.totalPages) {
      setPage(Math.max(1, query.data.totalPages));
    }
  }, [page, query.data]);
  const confirmRestore = async () => {
    if (!selected) return;
    setError(null);
    try {
      await restore(selected).unwrap();
      setSelected(null);
    } catch {
      setError("恢复复习失败，请重试。");
    }
  };
  const content = (
    <section className={modal ? undefined : "border-t border-base-300 py-8"}>
      <h2
        id={modal ? "review-exclusions-panel-title" : undefined}
        className="text-xl font-semibold"
      >
        已停止复习
      </h2>
      {query.isError || error ? (
        <p className="alert alert-error mt-4 text-sm" role="alert">
          {error ?? "停止复习列表暂时无法加载。"}
        </p>
      ) : null}
      {query.isError ? (
        <button
          className="btn btn-ghost btn-sm mt-3"
          type="button"
          onClick={() => void query.refetch()}
        >
          重新加载
        </button>
      ) : null}
      <div className="mt-5 divide-y divide-base-300">
        {query.data?.items.map((word) => (
          <div
            className="flex flex-col items-stretch gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"
            key={word.wordId}
          >
            <div className="min-w-0 flex-1">
              <p className="wrap-break-word font-semibold">{word.headword}</p>
              <p className="wrap-break-word text-sm text-base-content/60">
                {word.senses[0]?.definition}
              </p>
            </div>
            <button
              className="btn btn-outline btn-sm"
              aria-label={`恢复复习 ${word.headword}`}
              type="button"
              onClick={() => setSelected(word.wordId)}
            >
              恢复复习
            </button>
          </div>
        ))}
      </div>
      {query.data?.totalCount === 0 ? (
        <p className="mt-4 text-base-content/60">没有停止复习的单词</p>
      ) : null}
      {query.data && query.data.totalPages > 1 ? (
        <nav
          className="mt-5 flex items-center justify-between"
          aria-label="停止复习分页"
        >
          <button
            className="btn btn-ghost btn-square"
            aria-label="上一页"
            disabled={page <= 1}
            type="button"
            onClick={() => setPage((value) => value - 1)}
          >
            <ChevronLeft className="size-4" />
          </button>
          <span className="text-sm">
            第 {query.data.page} / {query.data.totalPages} 页
          </span>
          <button
            className="btn btn-ghost btn-square"
            aria-label="下一页"
            disabled={page >= query.data.totalPages}
            type="button"
            onClick={() => setPage((value) => value + 1)}
          >
            <ChevronRight className="size-4" />
          </button>
        </nav>
      ) : null}
      {selected ? (
        <div
          className="modal modal-open"
          role="dialog"
          aria-modal="true"
          aria-labelledby="restore-title"
        >
          <div className="modal-box">
            <h3 id="restore-title" className="text-xl font-semibold">
              恢复复习？
            </h3>
            <p className="mt-3 text-base-content/70">
              恢复后将在 1 天后重新到期。
            </p>
            {error ? (
              <p className="alert alert-error mt-4 text-sm" role="alert">
                {error}
              </p>
            ) : null}
            <div className="modal-action">
              <button
                className="btn btn-ghost"
                disabled={restoreState.isLoading}
                type="button"
                onClick={() => setSelected(null)}
              >
                取消
              </button>
              <button
                className="btn btn-primary"
                disabled={restoreState.isLoading}
                type="button"
                onClick={() => void confirmRestore()}
              >
                确认恢复复习
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );

  if (!modal) return content;

  return (
    <div
      className="modal modal-open"
      role="dialog"
      aria-modal="true"
      aria-labelledby="review-exclusions-panel-title"
    >
      <div className="modal-box relative w-11/12 max-w-3xl">
        <button
          className="btn btn-ghost btn-sm btn-circle absolute right-2 top-2"
          aria-label="关闭已停止复习"
          type="button"
          onClick={onClose}
        >
          <X aria-hidden="true" className="size-4" />
        </button>
        {content}
      </div>
      <button
        className="modal-backdrop"
        aria-label="关闭已停止复习弹窗"
        type="button"
        onClick={onClose}
      />
    </div>
  );
}
