import { Link } from "react-router-dom";
import { useState } from "react";
import type { WordStudySessionState } from "@/features/wordStudy/wordStudyTypes";

export default function WordStudyCompletion({
  session,
  continueLabel,
  canContinue,
  onContinue,
}: {
  session: WordStudySessionState;
  continueLabel: string;
  canContinue: boolean;
  onContinue?: () => Promise<void>;
}) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return (
    <section className="py-12 text-center">
      <h2 className="text-2xl font-bold">本组已完成</h2>
      <p className="mt-3 text-base-content/65">
        完成 {session.completedCount} 个，排除 {session.excludedCount} 个，跳过{" "}
        {session.skippedCount} 个
      </p>
      {error ? (
        <p
          className="alert alert-error mx-auto mt-5 max-w-md text-sm"
          role="alert"
        >
          {error}
        </p>
      ) : null}
      <div className="mt-7 flex flex-wrap justify-center gap-3">
        {canContinue && onContinue ? (
          <button
            className="btn btn-primary"
            disabled={loading}
            type="button"
            onClick={() => {
              setLoading(true);
              setError(null);
              void onContinue()
                .catch(() => setError("下一组暂时无法创建，请重试。"))
                .finally(() => setLoading(false));
            }}
          >
            {loading ? (
              <span className="loading loading-spinner loading-sm" />
            ) : null}
            {continueLabel}
          </button>
        ) : null}
        <Link className="btn btn-outline" to="/words">
          返回单词首页
        </Link>
      </div>
    </section>
  );
}
