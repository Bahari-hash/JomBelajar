import type { ChangeEvent } from "react";
import { RotateCcw } from "lucide-react";
import type {
  LocalPaperAnswer,
  PaperAnswerValue,
  PaperAttemptQuestion,
} from "@/features/papers/paperTypes";
import { getQuestionTypeLabel } from "@/features/papers/paperUtils";

interface PaperQuestionProps {
  question: PaperAttemptQuestion;
  index: number;
  answer: LocalPaperAnswer;
  disabled: boolean;
  onChange: (value: PaperAnswerValue) => void;
  onBlur: () => void;
  onRetry: () => void;
}

function SaveState({
  answer,
  onRetry,
}: {
  answer: LocalPaperAnswer;
  onRetry: () => void;
}) {
  if (answer.status === "saving")
    return (
      <span aria-live="polite" className="text-sm text-base-content/60">
        正在保存
      </span>
    );
  if (answer.status === "saved")
    return (
      <span aria-live="polite" className="text-sm text-success">
        已保存
      </span>
    );
  if (answer.status === "error")
    return (
      <span className="flex items-center gap-2 text-sm text-error" role="alert">
        <span>保存失败</span>
        <button
          className="btn btn-ghost btn-xs"
          type="button"
          onClick={onRetry}
        >
          <RotateCcw aria-hidden="true" className="size-3.5" />
          重试保存
        </button>
      </span>
    );
  return (
    <span aria-live="polite" className="text-sm text-base-content/55">
      未保存
    </span>
  );
}

export default function PaperQuestion({
  question,
  index,
  answer,
  disabled,
  onChange,
  onBlur,
  onRetry,
}: PaperQuestionProps) {
  const value = answer.value;
  const handleFill = (event: ChangeEvent<HTMLInputElement>) =>
    onChange(event.target.value);
  return (
    <section
      aria-labelledby={`paper-question-${question.id}`}
      className="min-w-0 space-y-5"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm font-semibold text-primary">
          第 {index + 1} 题 · {getQuestionTypeLabel(question.type)} ·{" "}
          {question.points} 分
        </p>
        <SaveState answer={answer} onRetry={onRetry} />
      </div>
      <h2
        id={`paper-question-${question.id}`}
        className="wrap-break-word text-xl font-bold leading-8 sm:text-2xl"
      >
        {question.prompt}
      </h2>
      {question.type === "SingleChoice" ? (
        <fieldset className="space-y-3">
          <legend className="sr-only">选择答案</legend>
          {[...question.options]
            .sort(
              (a, b) => a.sortOrder - b.sortOrder || a.id.localeCompare(b.id),
            )
            .map((option) => (
              <label
                key={option.id}
                className="flex min-w-0 cursor-pointer items-start gap-3 rounded-lg border border-base-300 p-4 transition-colors has-checked:border-primary has-checked:bg-primary/5"
              >
                <input
                  className="radio radio-primary mt-1 shrink-0"
                  disabled={disabled}
                  type="radio"
                  name={`question-${question.id}`}
                  value={option.id}
                  checked={value === option.id}
                  onChange={() => onChange(option.id)}
                />
                <span className="min-w-0 wrap-break-word leading-7">
                  {option.text}
                </span>
              </label>
            ))}
        </fieldset>
      ) : question.type === "TrueFalse" ? (
        <fieldset className="grid gap-3 sm:grid-cols-2">
          <legend className="sr-only">判断答案</legend>
          {[
            [true, "正确"],
            [false, "错误"],
          ].map(([booleanValue, label]) => (
            <label
              key={String(booleanValue)}
              className="flex cursor-pointer items-center gap-3 rounded-lg border border-base-300 p-4 has-checked:border-primary has-checked:bg-primary/5"
            >
              <input
                className="radio radio-primary"
                disabled={disabled}
                type="radio"
                name={`question-${question.id}`}
                checked={value === booleanValue}
                onChange={() => onChange(booleanValue as boolean)}
              />
              <span>{label}</span>
            </label>
          ))}
        </fieldset>
      ) : (
        <div>
          <label className="label" htmlFor={`fill-${question.id}`}>
            <span className="label-text font-medium">填空答案</span>
          </label>
          <input
            id={`fill-${question.id}`}
            aria-label="填空答案"
            className="input input-bordered w-full"
            disabled={disabled}
            type="text"
            value={typeof value === "string" ? value : ""}
            onChange={handleFill}
            onBlur={onBlur}
          />
        </div>
      )}
    </section>
  );
}
