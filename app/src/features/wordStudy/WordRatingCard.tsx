import { useEffect, useRef, useState } from "react";
import WordMemorizationCard from "./WordMemorizationCard";
import type { WordMemorizationResult, WordStudyCurrentItem } from "./wordStudyTypes";

const choices = [
  { rating: "Again", label: "重来", className: "btn-error" },
  { rating: "Hard", label: "困难", className: "btn-warning" },
  { rating: "Good", label: "良好", className: "btn-success" },
  { rating: "Easy", label: "简单", className: "btn-info" },
] as const;

/** Formats server-computed intervals without inventing client-side schedules. */
function formatReviewInterval(seconds: number) {
  if (seconds < 60) return `${Math.max(1, Math.round(seconds))} 秒`;
  if (seconds < 3600) return `${Math.round(seconds / 60 * 10) / 10} 分钟`;
  if (seconds < 86400) return `${Math.round(seconds / 3600 * 10) / 10} 小时`;
  return `${Math.round(seconds / 86400)} 天`;
}

/** A recall-first card. Keyed by item revision by the workspace to reset on every rating. */
export default function WordRatingCard({ item, submitting, onRate }: {
  item: Extract<WordStudyCurrentItem, { phase: "Memorization" }>;
  submitting: boolean;
  onRate: (rating: WordMemorizationResult) => Promise<void>;
}) {
  const [revealed, setRevealed] = useState(false);
  const [pending, setPending] = useState(false);
  const locked = useRef(false);
  const goodButton = useRef<HTMLButtonElement>(null);
  const revealButton = useRef<HTMLButtonElement>(null);
  const disabled = pending || submitting;
  const rate = async (rating: WordMemorizationResult) => {
    if (!revealed || disabled || locked.current ||
        !item.memorization.ratingPreviews?.some(p => p.rating === rating)) return;
    locked.current = true;
    setPending(true);
    try { await onRate(rating); }
    catch { /* Workspace displays the synchronized error and permits a retry. */ }
    finally { locked.current = false; setPending(false); }
  };
  useEffect(() => {
    if (revealed) goodButton.current?.focus();
    else revealButton.current?.focus();
  }, [revealed]);
  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      if (event.repeat || event.isComposing || event.altKey || event.ctrlKey || event.metaKey ||
          target.closest("input, textarea, select, [contenteditable=true], [role=dialog], dialog")) return;
      if (disabled) return;
      if (!revealed && event.code === "Space") {
        // Let other focused controls (audio/favorite) keep native activation.
        if (target.closest("button") && target !== revealButton.current) return;
        event.preventDefault(); setRevealed(true);
      } else if (revealed && /^[1-4]$/.test(event.key)) {
        event.preventDefault(); void rate(choices[Number(event.key) - 1].rating);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  });
  return <div>
    <WordMemorizationCard item={item} revealed={revealed} />
    <div className="mt-10 border-t border-base-300 pt-6">
      {!revealed ? <div className="text-center">
        <button ref={revealButton} type="button" className="btn btn-primary min-w-48" disabled={disabled}
          onClick={() => setRevealed(true)}>显示答案</button>
        <p className="mt-3 text-xs text-base-content/50">空格显示答案</p>
      </div> : <>
        <p className="mb-4 text-center text-sm text-base-content/60">回忆得怎么样？选择后安排下次复习。</p>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {choices.map((choice, index) => {
            const preview = item.memorization.ratingPreviews?.find(p => p.rating === choice.rating);
            return <div key={choice.rating} className="text-center">
              <p className="mb-2 min-h-5 text-sm tabular-nums text-base-content/65">
                {preview ? formatReviewInterval(preview.intervalSeconds) : "待计算"}
              </p>
              <button ref={choice.rating === "Good" ? goodButton : undefined} type="button"
                className={`btn w-full ${choice.className}`} disabled={disabled || !preview}
                onClick={() => void rate(choice.rating)}>{choice.label}<kbd className="ml-1 text-xs opacity-65">{index + 1}</kbd></button>
            </div>;
          })}
        </div>
        <p className="mt-4 text-center text-xs text-base-content/50">本组记忆完成后查看小结，可选择是否练习拼写</p>
      </>}
    </div>
  </div>;
}
