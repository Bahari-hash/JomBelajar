import { ArrowLeft, CheckCircle2, RotateCcw, XCircle } from "lucide-react";
import { Link } from "react-router-dom";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import type { PaperAttemptResult as Result } from "@/features/papers/paperTypes";
import {
  formatPaperDate,
  getQuestionTypeLabel,
} from "@/features/papers/paperUtils";

interface PaperAttemptResultProps {
  result: Result;
  restarting: boolean;
  restartError: string | null;
  onRestart: () => void;
}

function answerText(question: Result["questions"][number], correct: boolean) {
  if (!question.isAnswered && !correct) return "未作答";
  if (question.type === "SingleChoice") {
    const id = correct ? question.correctOptionId : question.selectedOptionId;
    return (
      question.options.find((option) => option.id === id)?.text ?? "未作答"
    );
  }
  if (question.type === "TrueFalse") {
    const value = correct ? question.correctBoolean : question.booleanAnswer;
    return value === null ? "未作答" : value ? "正确" : "错误";
  }
  if (question.type === "Dictation") {
    const values = correct ? question.dictationAnswers : question.textAnswers;
    return (
      values?.map((value) => value.trim() || "未作答").join(" / ") || "未作答"
    );
  }
  if (correct)
    return question.acceptedAnswers.length
      ? question.acceptedAnswers.join("、")
      : "暂无标准答案";
  return question.textAnswer?.trim() || "未作答";
}

export default function PaperAttemptResult({
  result,
  restarting,
  restartError,
  onRestart,
}: PaperAttemptResultProps) {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div
        className={`rounded-lg border p-5 sm:p-7 ${result.isPassed ? "border-success/40 bg-success/10" : "border-warning/40 bg-warning/10"}`}
      >
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              {result.isPassed ? (
                <CheckCircle2
                  aria-hidden="true"
                  className="size-6 text-success"
                />
              ) : (
                <XCircle aria-hidden="true" className="size-6 text-warning" />
              )}
              <h1 className="text-2xl font-bold">测试结果</h1>
            </div>
            <p className="mt-3 text-3xl font-bold">
              {result.score} / {result.paperTotalScore}
            </p>
            <p className="mt-2 font-semibold">
              {result.isPassed ? "已通过" : "未通过"}
            </p>
          </div>
          <div className="text-right text-sm text-base-content/65">
            <p>{result.paperTitle}</p>
            <p className="mt-1">第 {result.attemptNumber} 次测试</p>
            <time className="mt-1 block" dateTime={result.submittedAt}>
              提交于 {formatPaperDate(result.submittedAt)}
            </time>
            <p className="mt-1">及格分 {result.paperPassingScore}</p>
          </div>
        </div>
      </div>
      <section aria-labelledby="paper-result-questions">
        <h2 id="paper-result-questions" className="text-xl font-bold">
          逐题结果
        </h2>
        <ol className="mt-4 space-y-4">
          {[...result.questions]
            .sort(
              (a, b) =>
                a.sortOrder - b.sortOrder ||
                a.questionId.localeCompare(b.questionId),
            )
            .map((question, index) => (
              <li
                key={question.questionId}
                className="rounded-lg border border-base-300 bg-base-100 p-5"
              >
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-sm font-semibold">
                    第 {index + 1} 题 · {getQuestionTypeLabel(question.type)} ·{" "}
                    {question.points} 分
                  </p>
                  <span
                    className={
                      question.isCorrect
                        ? "font-semibold text-success"
                        : "font-semibold text-error"
                    }
                  >
                    {question.isCorrect ? "正确" : "错误"} ·{" "}
                    {question.awardedPoints} 分
                  </span>
                </div>
                <h3 className="mt-3 break-words text-lg font-bold leading-7">
                  {question.prompt}
                </h3>
                {question.audioResourceId ? (
                  <div className="mt-3">
                    <AudioPlaybackButton
                      audioResourceId={question.audioResourceId}
                      label="题目音频"
                    />
                  </div>
                ) : null}
                <div className="mt-4 grid min-w-0 gap-3 text-sm sm:grid-cols-2">
                  <p className="min-w-0 break-words">
                    <span className="font-semibold">用户答案：</span>
                    {answerText(question, false)}
                  </p>
                  <p className="min-w-0 break-words">
                    <span className="font-semibold">正确答案：</span>
                    {answerText(question, true)}
                  </p>
                </div>
                {question.explanation?.trim() ? (
                  <div className="mt-4 border-t border-base-300 pt-4 text-sm leading-7 text-base-content/70">
                    <span className="font-semibold text-base-content">
                      解析：
                    </span>
                    {question.explanation}
                  </div>
                ) : null}
              </li>
            ))}
        </ol>
      </section>
      <div className="flex flex-wrap gap-3">
        <Link className="btn btn-ghost" to={`/papers/${result.paperId}`}>
          <ArrowLeft aria-hidden="true" className="size-4" />
          返回试卷详情
        </Link>
        <button
          className="btn btn-primary"
          disabled={restarting}
          type="button"
          onClick={onRestart}
        >
          {restarting ? (
            <span className="loading loading-spinner loading-sm" />
          ) : (
            <RotateCcw aria-hidden="true" className="size-4" />
          )}
          再做一次
        </button>
      </div>
      {restartError ? (
        <p className="text-sm text-error" role="alert">
          {restartError}
        </p>
      ) : null}
    </div>
  );
}
