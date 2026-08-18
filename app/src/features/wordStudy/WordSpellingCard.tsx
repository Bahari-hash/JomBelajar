import { useEffect, useState } from "react";
import type {
  WordSpellingResult,
  WordStudyCurrentItem,
} from "@/features/wordStudy/wordStudyTypes";

export default function WordSpellingCard({
  item,
  submitting,
  onSubmit,
}: {
  item: Extract<WordStudyCurrentItem, { phase: "Spelling" }>;
  submitting: boolean;
  onSubmit: (answer: string) => Promise<WordSpellingResult | null>;
}) {
  const [answer, setAnswer] = useState("");
  const [feedback, setFeedback] = useState<string | null>(null);
  useEffect(() => {
    setAnswer("");
    setFeedback(null);
  }, [item.itemId]);
  return (
    <form
      className="space-y-6"
      onSubmit={(event) => {
        event.preventDefault();
        void onSubmit(answer).then((result) => {
          if (result === "Incorrect") setFeedback("拼写不正确，稍后再试一次");
          if (result === "Correct") {
            setFeedback("拼写正确");
            setAnswer("");
          }
        });
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
          className="input input-bordered w-full"
          value={answer}
          onChange={(event) => setAnswer(event.target.value)}
        />
      </label>
      {feedback ? (
        <p className="text-sm" role="status">
          {feedback}
        </p>
      ) : null}
      <button
        className="btn btn-primary"
        disabled={submitting || !answer.trim()}
        type="submit"
      >
        提交拼写
      </button>
    </form>
  );
}
