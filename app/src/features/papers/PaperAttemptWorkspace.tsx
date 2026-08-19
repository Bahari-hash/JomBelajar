import { ArrowLeft, ArrowRight, Send, X } from "lucide-react";
import { useMemo, useState } from "react";
import PaperQuestion from "@/features/papers/PaperQuestion";
import PaperQuestionDirectory from "@/features/papers/PaperQuestionDirectory";
import type {
  PaperAttempt,
  PaperAnswerValue,
  LocalPaperAnswer,
} from "@/features/papers/paperTypes";
import type { FlushResult } from "@/features/papers/usePaperAttemptAnswers";

interface PaperAttemptWorkspaceProps {
  attempt: PaperAttempt;
  answers: Record<string, LocalPaperAnswer>;
  isFlushing: boolean;
  changeAnswer: (questionId: string, value: PaperAnswerValue) => void;
  flushQuestion: (questionId: string) => Promise<boolean>;
  retryQuestion: (questionId: string) => Promise<boolean>;
  flushAll: () => Promise<FlushResult>;
  submitting: boolean;
  submitError: string | null;
  onSubmit: () => Promise<void>;
}

export default function PaperAttemptWorkspace({
  attempt,
  answers,
  isFlushing,
  changeAnswer,
  flushQuestion,
  retryQuestion,
  flushAll,
  submitting,
  submitError,
  onSubmit,
}: PaperAttemptWorkspaceProps) {
  const questions = useMemo(
    () =>
      [...attempt.questions].sort(
        (left, right) =>
          left.sortOrder - right.sortOrder || left.id.localeCompare(right.id),
      ),
    [attempt.questions],
  );
  const [currentId, setCurrentId] = useState(questions[0]?.id ?? "");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [localSubmitting, setLocalSubmitting] = useState(false);
  const busy = isFlushing || submitting || localSubmitting;
  const currentIndex = Math.max(
    0,
    questions.findIndex((question) => question.id === currentId),
  );
  const current = questions[currentIndex];
  const answeredCount = questions.filter((question) => {
    const value = answers[question.id]?.value;
    return (
      value !== null &&
      value !== undefined &&
      !(typeof value === "string" && value.trim() === "") &&
      !(Array.isArray(value) && value.every((item) => item.trim() === ""))
    );
  }).length;

  const confirmSubmit = async () => {
    setLocalSubmitting(true);
    const result = await flushAll();
    if (!result.ok) {
      setCurrentId(result.failedQuestionIds[0] ?? currentId);
      setLocalSubmitting(false);
      setConfirmOpen(false);
      return;
    }
    try {
      await onSubmit();
    } finally {
      setLocalSubmitting(false);
      setConfirmOpen(false);
    }
  };
  if (!current)
    return (
      <p className="border-y border-base-300 py-12 text-center">
        这份试卷暂无题目。
      </p>
    );
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm text-base-content/65">
            第 {currentIndex + 1} / {questions.length} 题
          </p>
          <p className="mt-1 text-sm text-base-content/65">
            已作答 {answeredCount} / {questions.length} 题
          </p>
        </div>
        <button
          className="btn btn-primary btn-sm"
          disabled={busy}
          type="button"
          onClick={() => setConfirmOpen(true)}
        >
          <Send aria-hidden="true" className="size-4" />
          提交试卷
          {questions.length - answeredCount
            ? `（${questions.length - answeredCount} 题未作答）`
            : ""}
        </button>
      </div>
      <div className="grid min-w-0 gap-6 lg:grid-cols-[16rem_minmax(0,1fr)]">
        <PaperQuestionDirectory
          questions={questions}
          answers={answers}
          currentQuestionId={current.id}
          disabled={busy}
          onSelect={setCurrentId}
        />
        <main className="min-w-0 rounded-lg border border-base-300 bg-base-100 p-5 sm:p-8">
          <PaperQuestion
            question={current}
            index={currentIndex}
            answer={answers[current.id]!}
            disabled={busy}
            onChange={(value) => changeAnswer(current.id, value)}
            onBlur={() => void flushQuestion(current.id)}
            onRetry={() => void retryQuestion(current.id)}
          />
          <div className="mt-8 grid gap-3 border-t border-base-300 pt-5 sm:grid-cols-2">
            <button
              className="btn btn-outline"
              disabled={busy || currentIndex === 0}
              type="button"
              onClick={() => setCurrentId(questions[currentIndex - 1]!.id)}
            >
              <ArrowLeft aria-hidden="true" className="size-4" />
              上一题
            </button>
            <button
              className="btn btn-outline"
              disabled={busy || currentIndex === questions.length - 1}
              type="button"
              onClick={() => setCurrentId(questions[currentIndex + 1]!.id)}
            >
              下一题
              <ArrowRight aria-hidden="true" className="size-4" />
            </button>
          </div>
          {submitError ? (
            <p className="mt-4 text-sm text-error" role="alert">
              {submitError}
            </p>
          ) : null}
        </main>
      </div>
      {confirmOpen ? (
        <div
          className="fixed inset-0 z-50 grid place-items-center p-4"
          role="dialog"
          aria-modal="true"
          aria-label="确认提交试卷"
        >
          <button
            aria-label="关闭提交确认"
            className="absolute inset-0 bg-neutral/45"
            type="button"
            onClick={() => setConfirmOpen(false)}
          />
          <div className="relative w-full max-w-md rounded-lg border border-base-300 bg-base-100 p-6 shadow-xl">
            <div className="flex items-start justify-between gap-4">
              <h2 className="text-lg font-bold">确认提交试卷</h2>
              <button
                aria-label="关闭提交确认"
                className="btn btn-square btn-ghost btn-sm"
                type="button"
                onClick={() => setConfirmOpen(false)}
              >
                <X aria-hidden="true" className="size-4" />
              </button>
            </div>
            <p className="mt-4 leading-7 text-base-content/70">
              还有 {questions.length - answeredCount}{" "}
              题未作答，未作答题会按错误计分。确认现在提交吗？
            </p>
            <div className="mt-6 flex justify-end gap-3">
              <button
                className="btn btn-ghost"
                type="button"
                disabled={busy}
                onClick={() => setConfirmOpen(false)}
              >
                继续答题
              </button>
              <button
                className="btn btn-primary"
                type="button"
                disabled={busy}
                onClick={() => void confirmSubmit()}
              >
                {busy ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : null}
                确认提交
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
