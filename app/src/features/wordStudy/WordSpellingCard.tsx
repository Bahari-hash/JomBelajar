import { CheckCircle2, XCircle } from "lucide-react";
import { useEffect, useState } from "react";
import type {
  WordStudyCurrentItem,
  WordStudyCommandResponse,
  WordStudySessionState,
} from "@/features/wordStudy/wordStudyTypes";

export default function WordSpellingCard({
  item,
  submitting,
  onSubmit,
  onAdvance,
}: {
  item: Extract<WordStudyCurrentItem, { phase: "Spelling" }>;
  submitting: boolean;
  onSubmit: (answer: string) => Promise<WordStudyCommandResponse | null>;
  onAdvance?: (session: WordStudySessionState) => void;
}) {
  const [answer, setAnswer] = useState("");
  const [submitted, setSubmitted] = useState<WordStudyCommandResponse | null>(
    null,
  );
  useEffect(() => {
    setAnswer("");
    setSubmitted(null);
  }, [item.itemId]);
  const outcome = submitted?.spellingOutcome;
  const submit = async () => {
    const response = await onSubmit(answer);
    if (response) setSubmitted(response);
  };
  const advance = () => {
    if (!submitted || typeof onAdvance !== "function") return;
    setAnswer("");
    setSubmitted(null);
    onAdvance(submitted.session);
  };
  return (
    <form
      className="space-y-6"
      onSubmit={(event) => {
        event.preventDefault();
        if (!submitted && answer.trim() && !submitting) void submit();
      }}
    >
      <div className="space-y-3">
        {item.spelling.senses.map((sense, index) => (
          <div
            key={`${index}-${sense.definition}`}
            className="border-b border-base-300 pb-3"
          >
            <span className="text-sm text-primary">{sense.partOfSpeech}</span>
            <p>{sense.definition}</p>
          </div>
        ))}
      </div>
      <label className="form-control">
        <span className="label-text mb-2">拼写单词</span>
        <input
          aria-label="拼写单词"
          autoComplete="off"
          className="input input-bordered w-full mt-4"
          value={answer}
          disabled={submitting || submitted !== null}
          onChange={(event) => setAnswer(event.target.value)}
        />
      </label>
      {outcome ? (
        <div className="space-y-2 mt-4" role="status">
          <p
            className={`flex items-center gap-2 text-sm ${
              outcome.result === "Correct" ? "text-success" : "text-error"
            }`}
          >
            {outcome.result === "Correct" ? (
              <CheckCircle2 className="size-5" aria-hidden="true" />
            ) : (
              <XCircle className="size-5" aria-hidden="true" />
            )}
            {outcome.result === "Correct" ? "拼写正确" : "拼写错误"}
          </p>
          <p>正确答案：{outcome.correctAnswer}</p>
        </div>
      ) : null}
      {!submitted ? (
        <button
          className="btn btn-primary mt-4"
          disabled={submitting || !answer.trim()}
          type="submit"
        >
          提交拼写
        </button>
      ) : (
        <button
          className="btn btn-primary mt-4"
          disabled={submitting}
          type="button"
          onClick={advance}
        >
          {submitted.session.status === "Active" &&
          submitted.session.currentItem
            ? "下一词"
            : "查看结果"}
        </button>
      )}
    </form>
  );
}
