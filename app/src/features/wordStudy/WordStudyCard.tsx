import { Check, RotateCcw } from "lucide-react";
import WordStudyAudioButton from "@/features/wordStudy/WordStudyAudioButton";
import WordStudyProgress from "@/features/wordStudy/WordStudyProgress";
import type {
  WordStudyNextItem,
  WordStudyResult,
} from "@/features/wordStudy/wordStudyTypes";

interface WordStudyCardProps {
  item: WordStudyNextItem;
  submitting: boolean;
  onResult: (result: WordStudyResult) => void;
}

/** Renders one daily word with senses, optional examples, audio, and result actions. */
export default function WordStudyCard({
  item,
  submitting,
  onResult,
}: WordStudyCardProps) {
  const pronunciations = [...item.pronunciations].sort(
    (left, right) => Number(right.isDefault) - Number(left.isDefault) || left.sortOrder - right.sortOrder,
  );

  return (
    <article className="mx-auto w-full max-w-3xl rounded-lg border border-base-300 bg-base-100 p-5 shadow-sm sm:p-8">
      <WordStudyProgress current={item.position + 1} total={item.actualCount} />

      <header className="mt-7 flex flex-wrap items-center gap-3">
        <h2 className="min-w-0 wrap-break-word text-4xl font-bold sm:text-5xl">
          {item.headword}
        </h2>
        {pronunciations.map((pronunciation) => (
          <span className="inline-flex items-center gap-1" key={pronunciation.audioClipId}>
            <WordStudyAudioButton
              audioClipId={pronunciation.audioClipId}
              label="发音"
            />
            {pronunciation.ipa ? (
              <span className="text-sm text-base-content/60">
                {pronunciation.ipa}
              </span>
            ) : null}
          </span>
        ))}
      </header>

      <ol className="mt-8 space-y-7">
        {item.senses.map((sense) => (
          <li className="border-t border-base-300 pt-5" key={`${sense.sortOrder}-${sense.definition}`}>
            <div className="flex flex-wrap items-start gap-3">
              <span className="badge badge-outline">{sense.partOfSpeech}</span>
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
                {sense.examples.map((example) => (
                  <blockquote
                    className="border-l-2 border-secondary pl-4 text-sm leading-6"
                    key={`${example.sortOrder}-${example.sentence}`}
                  >
                    <div className="flex items-start gap-1">
                      <p className="min-w-0 flex-1 wrap-break-word">
                        {example.sentence}
                      </p>
                      {example.audioClipId ? (
                        <WordStudyAudioButton
                          audioClipId={example.audioClipId}
                          label="例句音频"
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
    </article>
  );
}
