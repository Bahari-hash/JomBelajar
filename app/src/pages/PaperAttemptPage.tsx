import { ArrowLeft, RefreshCw } from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import PaperAttemptResult from "@/features/papers/PaperAttemptResult";
import PaperAttemptWorkspace from "@/features/papers/PaperAttemptWorkspace";
import {
  useClearAnswerMutation,
  useGetAttemptQuery,
  useGetAttemptResultQuery,
  useLazyGetAttemptResultQuery,
  useSaveAnswerMutation,
  useStartAttemptMutation,
  useSubmitAttemptMutation,
} from "@/features/papers/paperApi";
import { isPaperGuid } from "@/features/papers/paperSearchParams";
import type {
  PaperAttempt,
  PaperAttemptResult as Result,
  SavePaperAnswerRequest,
} from "@/features/papers/paperTypes";
import {
  getPaperErrorMessage,
  isPaperSubmitRecoveryError,
} from "@/features/papers/paperUtils";
import { usePaperAttemptAnswers } from "@/features/papers/usePaperAttemptAnswers";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

function Loading() {
  return (
    <div
      aria-label="测试加载中"
      className="mx-auto max-w-4xl space-y-5"
      role="status"
    >
      <div className="skeleton h-8 w-2/3" />
      <div className="skeleton h-64 w-full" />
    </div>
  );
}

function ResultView({ result }: { result: Result }) {
  const navigate = useNavigate();
  const [restart, restartState] = useStartAttemptMutation();
  const [error, setError] = useState<string | null>(null);
  const restartAttempt = async () => {
    setError(null);
    try {
      const attempt = await restart(result.paperId).unwrap();
      navigate(`/paper-attempts/${attempt.id}`);
    } catch (reason) {
      setError(getPaperErrorMessage(reason, "开始新的测试失败，请重试。"));
    }
  };
  return (
    <PaperAttemptResult
      result={result}
      restarting={restartState.isLoading}
      restartError={error}
      onRestart={restartAttempt}
    />
  );
}

function SubmittedAttempt({ attempt }: { attempt: PaperAttempt }) {
  const resultQuery = useGetAttemptResultQuery(attempt.id);
  useDocumentTitle(`${attempt.title} · 测试结果`);
  if (resultQuery.isLoading) return <Loading />;
  if (resultQuery.isError || !resultQuery.data)
    return (
      <section className="mx-auto max-w-xl py-14 text-center" role="alert">
        <h1 className="text-2xl font-bold">结果加载失败</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          {getPaperErrorMessage(resultQuery.error)}
        </p>
        <button
          className="btn btn-primary mt-6"
          type="button"
          onClick={() => resultQuery.refetch()}
        >
          <RefreshCw aria-hidden="true" className="size-4" />
          重新加载
        </button>
      </section>
    );
  return <ResultView result={resultQuery.data} />;
}

function ActiveAttempt({ attempt }: { attempt: PaperAttempt }) {
  const [saveAnswer] = useSaveAnswerMutation();
  const [clearAnswer] = useClearAnswerMutation();
  const [submitAttempt, submitState] = useSubmitAttemptMutation();
  const [loadResult] = useLazyGetAttemptResultQuery();
  const [submittedResult, setSubmittedResult] = useState<Result | null>(null);
  const [submitRecoveryError, setSubmitRecoveryError] = useState<string | null>(
    null,
  );
  const controller = usePaperAttemptAnswers({
    attemptId: attempt.id,
    questions: attempt.questions,
    saveAnswer: (questionId: string, answer: SavePaperAnswerRequest) =>
      saveAnswer({ attemptId: attempt.id, questionId, answer }).unwrap(),
    clearAnswer: (questionId: string) =>
      clearAnswer({ attemptId: attempt.id, questionId }).unwrap(),
  });
  useDocumentTitle(`${attempt.title} · 在线测试`);
  if (submittedResult) return <ResultView result={submittedResult} />;
  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <Link
        className="btn btn-ghost btn-sm -ml-3"
        to={`/papers/${attempt.paperId}`}
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        返回试卷详情
      </Link>
      <header>
        <h1 className="wrap-break-word text-2xl font-bold sm:text-3xl">
          {attempt.title}
        </h1>
        {attempt.instructions?.trim() ? (
          <p className="mt-2 whitespace-pre-wrap wrap-break-word text-sm leading-7 text-base-content/65">
            {attempt.instructions}
          </p>
        ) : null}
      </header>
      <PaperAttemptWorkspace
        attempt={attempt}
        answers={controller.answers}
        isFlushing={controller.isFlushing}
        changeAnswer={controller.changeAnswer}
        flushQuestion={controller.flushQuestion}
        retryQuestion={controller.retryQuestion}
        flushAll={controller.flushAll}
        submitting={submitState.isLoading}
        submitError={
          submitRecoveryError ??
          (submitState.isError
            ? getPaperErrorMessage(submitState.error, "提交测试失败，请重试。")
            : null)
        }
        onSubmit={async () => {
          setSubmitRecoveryError(null);
          try {
            const result = await submitAttempt(attempt.id).unwrap();
            setSubmittedResult(result);
          } catch (reason) {
            if (!isPaperSubmitRecoveryError(reason)) throw reason;
            try {
              const result = await loadResult(attempt.id).unwrap();
              setSubmittedResult(result);
            } catch (recoveryReason) {
              setSubmitRecoveryError(
                getPaperErrorMessage(recoveryReason, "提交测试失败，请重试。"),
              );
            }
          }
        }}
      />
    </div>
  );
}

export default function PaperAttemptPage() {
  const { attemptId } = useParams();
  const validId = isPaperGuid(attemptId) ? attemptId : null;
  const query = useGetAttemptQuery(validId ?? "", { skip: !validId });
  useDocumentTitle(query.data?.title ?? "在线测试");
  if (!validId)
    return (
      <section className="mx-auto max-w-xl py-14 text-center">
        <h1 className="text-2xl font-bold">测试不存在</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          请从试卷详情进入测试。
        </p>
        <Link className="btn btn-primary mt-6" to="/papers">
          返回试卷目录
        </Link>
      </section>
    );
  if (query.isLoading) return <Loading />;
  if (query.isError || !query.data)
    return (
      <section className="mx-auto max-w-xl py-14 text-center" role="alert">
        <h1 className="text-2xl font-bold">测试加载失败</h1>
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
          <Link className="btn btn-ghost" to="/papers">
            返回试卷目录
          </Link>
        </div>
      </section>
    );
  return query.data.status === "Submitted" ? (
    <SubmittedAttempt attempt={query.data} />
  ) : (
    <ActiveAttempt attempt={query.data} />
  );
}
