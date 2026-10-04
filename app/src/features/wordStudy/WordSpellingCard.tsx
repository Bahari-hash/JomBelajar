import { CheckCircle2, XCircle } from "lucide-react";
import { useEffect, useRef, useState } from "react";
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
  onExit,
}: {
  item: Extract<WordStudyCurrentItem, { phase: "Spelling" }>;
  submitting: boolean;
  onSubmit: (answer: string) => Promise<WordStudyCommandResponse | null>;
  onAdvance?: (session: WordStudySessionState) => void;
  onExit?: () => Promise<void>;
}) {
  const input = useRef<HTMLInputElement>(null);
  const locked = useRef(false);
  const [pending, setPending] = useState(false);
  const [exitError, setExitError] = useState(false);
  const [answer, setAnswer] = useState("");
  const [submitted, setSubmitted] = useState<WordStudyCommandResponse | null>(
    null,
  );
  useEffect(() => {
    setAnswer("");
    setSubmitted(null);
  }, [item.itemId]);
  useEffect(() => { if (!submitted) input.current?.focus(); }, [submitted, item.itemId]);
  const outcome = submitted?.spellingOutcome;
  const submit = async () => {
    if (locked.current || submitting || submitted) return;
    locked.current = true; setPending(true);
    try { const response = await onSubmit(answer); if (response) setSubmitted(response); }
    finally { locked.current = false; setPending(false); }
  };
  const advance = () => {
    if (!submitted || typeof onAdvance !== "function") return;
    setAnswer("");
    setSubmitted(null);
    onAdvance(submitted.session);
  };
  useEffect(() => {
    const handle = (event: KeyboardEvent) => {
      if (event.code !== "Space" || event.repeat || event.isComposing || event.ctrlKey || event.metaKey || event.altKey) return;
      const target = event.target as HTMLElement;
      if (target.closest("dialog, [role=dialog], [role=alertdialog], textarea, select, [contenteditable=true]") ||
          (target.tagName === "INPUT" && target !== input.current)) return;
      if (target.closest("[data-spelling-exit]") || (target.closest("button") && !target.closest("form[data-spelling]"))) return;
      // Shift+Space still allows a literal space for multiword entries.
      if (event.shiftKey) return;
      event.preventDefault();
      if (pending || submitting || locked.current) return;
      if (submitted) advance(); else void submit();
    };
    window.addEventListener("keydown", handle);
    return () => window.removeEventListener("keydown", handle);
  });
  return (
    <form
      data-spelling
      className="space-y-6"
      onSubmit={(event) => {
        event.preventDefault();
        if (!submitted && !submitting && !pending) void submit();
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
          ref={input}
          aria-label="拼写单词"
          autoComplete="off"
          className="input input-bordered w-full mt-4"
          value={answer}
          disabled={submitting || pending || submitted !== null}
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
            {outcome.result === "Correct" ? "拼写正确" : !answer.trim() ? "已跳过，稍后再练" : "拼写错误"}
          </p>
          <p>正确答案：{outcome.correctAnswer}</p>
        </div>
      ) : null}
      <div className="mt-4 flex flex-wrap items-center gap-3">
      {!submitted ? (
        <button
          key="submit-spelling"
          className="btn btn-primary"
          disabled={submitting || pending}
          type="submit"
        >
          {answer.trim() ? "提交拼写" : "不知道，跳过"}
        </button>
      ) : (
        <button
          key="advance-spelling"
          className="btn btn-primary"
          disabled={submitting}
          type="button"
          onClick={(event) => {
            // Advancing replaces this control; never submit the next empty answer.
            event.preventDefault();
            advance();
          }}
        >
          {submitted.session.status === "Active" &&
          submitted.session.currentItem
            ? "下一词"
            : "查看结果"}
        </button>
      )}
      {onExit ? <button data-spelling-exit type="button" className="btn btn-outline" disabled={submitting || pending}
        onClick={() => {
          if (locked.current) return;
          locked.current = true; setPending(true); setExitError(false);
          void onExit().catch(() => setExitError(true)).finally(() => { locked.current = false; setPending(false); });
        }}>退出本次拼写</button> : null}
      </div>
      {exitError ? <p role="alert" className="text-error">退出未完成，请重试。</p> : null}
    </form>
  );
}
