import {
  ArrowLeft,
  ArrowRight,
  Check,
  ChevronDown,
  ChevronUp,
  RotateCcw,
} from "lucide-react";
import { useEffect, useState } from "react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import WordStudyProgress from "@/features/wordStudy/WordStudyProgress";
import type {
  WordStudyResult,
  WordStudySessionItem,
} from "@/features/wordStudy/wordStudyTypes";

interface WordStudyCardProps {
  item: WordStudySessionItem;
  total: number;
  submitting: boolean;
  hasPrevious: boolean;
  hasNext: boolean;
  onPrevious: () => void;
  onNext: () => void;
  onResult: (result: WordStudyResult) => void;
}

const RESULT_LABELS = {
  Remembered: "已记住",
  Forgotten: "没记住",
  Skipped: "已跳过",
} as const;

/** Renders one daily word with content, result state, audio, and navigation. */
export default function WordStudyCard({
  item,
  total,
  submitting,
  hasPrevious,
  hasNext,
  onPrevious,
  onNext,
  onResult,
}: WordStudyCardProps) {
  const content = item.content;
  const answered = item.status !== "Pending";
  const unavailable = !item.contentAvailable || !content;
  const [expandedItemId, setExpandedItemId] = useState<string | null>(null);
  const contentExpanded = expandedItemId === item.itemId;

  useEffect(() => {
    setExpandedItemId(null);
  }, [item.itemId]);

  return (
    <article className="w-full rounded-lg border border-base-300 bg-base-100 p-5 shadow-sm sm:p-8">
      <div className="flex items-center justify-between gap-3">
        <button
          aria-label="上一词"
          className="btn btn-ghost btn-sm"
          disabled={submitting || !hasPrevious}
          type="button"
          onClick={onPrevious}
        >
          <ArrowLeft aria-hidden="true" className="size-4" />
          上一词
        </button>
        <WordStudyProgress current={item.position + 1} total={total} />
        <button
          aria-label="下一词"
          className="btn btn-ghost btn-sm"
          disabled={submitting || !hasNext}
          type="button"
          onClick={onNext}
        >
          下一词
          <ArrowRight aria-hidden="true" className="size-4" />
        </button>
      </div>

      {unavailable ? (
        <div className="flex min-h-80 flex-col items-center justify-center text-center">
          <h2 className="text-2xl font-bold">该单词当前不可查看</h2>
          <p className="mt-3 text-sm text-base-content/65">
            词条内容已经删除或暂时无法使用。
          </p>
          {answered ? (
            <p className="mt-4 text-sm font-medium">
              结果：
              {RESULT_LABELS[item.status as keyof typeof RESULT_LABELS] ??
                "已跳过"}
            </p>
          ) : null}
        </div>
      ) : (
        <>
          <header className="mt-7 flex flex-wrap items-center gap-3">
            <h2 className="min-w-0 wrap-break-word text-4xl font-bold sm:text-5xl">
              {content.headword}
            </h2>
            {content.audioResourceId ? (
              <AudioPlaybackButton
                audioResourceId={content.audioResourceId}
                label="单词发音"
                variant="icon"
              />
            ) : null}
          </header>

          <div className="mt-8 border-t border-base-300 pt-5">
            <button
              aria-controls={`word-study-content-${item.itemId}`}
              aria-expanded={contentExpanded}
              className="btn btn-outline btn-sm"
              type="button"
              onClick={() =>
                setExpandedItemId((current) =>
                  current === item.itemId ? null : item.itemId,
                )
              }
            >
              {contentExpanded ? (
                <ChevronUp aria-hidden="true" className="size-4" />
              ) : (
                <ChevronDown aria-hidden="true" className="size-4" />
              )}
              {contentExpanded ? "隐藏释义和例句" : "显示释义和例句"}
            </button>
          </div>

          {contentExpanded ? (
            <ol
              className="mt-6 space-y-7"
              id={`word-study-content-${item.itemId}`}
            >
              {content.senses.map((sense) => (
                <li
                  className="border-t border-base-300 pt-5"
                  key={`${sense.sortOrder}-${sense.definition}`}
                >
                  <div className="flex flex-wrap items-start gap-3">
                    <span className="badge badge-outline">
                      {sense.partOfSpeech}
                    </span>
                    <p className="min-w-0 flex-1 wrap-break-word text-lg leading-8">
                      {sense.definition}
                    </p>
                  </div>
                  {sense.usageNote ? (
                    <p className="mt-2 pl-0 text-sm leading-6 text-base-content/65 sm:pl-16">
                      {sense.usageNote}
                    </p>
                  ) : null}
                  {sense.examples.length > 0 ? (
                    <div className="mt-4 space-y-3 sm:pl-16">
                      {sense.examples.map((example, exampleIndex) => (
                        <blockquote
                          className="border-l-2 border-secondary pl-4 text-sm leading-6"
                          key={`${example.sortOrder}-${example.sentence}`}
                        >
                          <div className="flex items-start gap-2">
                            <p className="min-w-0 flex-1 wrap-break-word">
                              {example.sentence}
                            </p>
                            {example.audioResourceId ? (
                              <AudioPlaybackButton
                                audioResourceId={example.audioResourceId}
                                label={`例句 ${exampleIndex + 1} 音频`}
                                variant="icon"
                              />
                            ) : null}
                          </div>
                          <p className="mt-1 wrap-break-word text-base-content/60">
                            {example.translation}
                          </p>
                        </blockquote>
                      ))}
                    </div>
                  ) : null}
                </li>
              ))}
            </ol>
          ) : null}

          {answered ? (
            <div className="alert mt-9" role="status">
              结果：
              {RESULT_LABELS[item.status as keyof typeof RESULT_LABELS] ??
                "已跳过"}
            </div>
          ) : (
            <div className="mt-9 grid grid-cols-1 gap-3 border-t border-base-300 pt-6 sm:grid-cols-2">
              <button
                className="btn btn-outline btn-error h-12"
                disabled={submitting}
                type="button"
                onClick={() => onResult("Forgotten")}
              >
                <RotateCcw aria-hidden="true" className="size-5" />
                没记住
              </button>
              <button
                className="btn btn-primary h-12"
                disabled={submitting}
                type="button"
                onClick={() => onResult("Remembered")}
              >
                {submitting ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : (
                  <Check aria-hidden="true" className="size-5" />
                )}
                记住了
              </button>
            </div>
          )}
        </>
      )}
    </article>
  );
}
