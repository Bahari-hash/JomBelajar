interface Props {
  open: boolean;
  submitting: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => Promise<void>;
}

export default function WordReviewExcludeDialog({
  open,
  submitting,
  error,
  onCancel,
  onConfirm,
}: Props) {
  if (!open) return null;
  return (
    <div
      className="modal modal-open"
      role="dialog"
      aria-modal="true"
      aria-labelledby="exclude-title"
    >
      <div className="modal-box">
        <h2 id="exclude-title" className="text-xl font-semibold">
          停止复习这个单词？
        </h2>
        <p className="mt-3 text-base-content/70">
          确认后，这个单词将从复习队列移除，可以在个人资料中恢复。
        </p>
        {error ? (
          <p className="alert alert-error mt-4 text-sm">{error}</p>
        ) : null}
        <div className="modal-action">
          <button
            className="btn btn-ghost"
            disabled={submitting}
            type="button"
            onClick={onCancel}
          >
            取消
          </button>
          <button
            className="btn btn-error"
            disabled={submitting}
            type="button"
            onClick={() => void onConfirm()}
          >
            {submitting ? (
              <span className="loading loading-spinner loading-sm" />
            ) : null}
            确认停止复习
          </button>
        </div>
      </div>
    </div>
  );
}
