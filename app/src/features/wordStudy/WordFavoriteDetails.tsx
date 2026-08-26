import { ChevronLeft } from "lucide-react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import type { WordFavorite } from "./wordStudyTypes";

interface WordFavoriteDetailsProps {
  word: WordFavorite;
  onClose: () => void;
  onRemove: () => void;
  removing?: boolean;
}

export default function WordFavoriteDetails({
  word,
  onClose,
  onRemove,
  removing = false,
}: WordFavoriteDetailsProps) {
  return (
    <section aria-labelledby="favorite-word-details-title">
      <div className="flex items-center gap-2 pr-10">
        <button
          className="btn btn-ghost btn-sm btn-square"
          aria-label="返回收藏本"
          title="返回收藏本"
          autoFocus
          type="button"
          onClick={onClose}
        >
          <ChevronLeft aria-hidden="true" className="size-4" />
        </button>
        <h2
          id="favorite-word-details-title"
          className="min-w-0 wrap-break-word text-xl font-semibold"
        >
          {word.headword}
        </h2>
      </div>
      {word.audioResourceId ? (
        <div className="mt-3">
          <AudioPlaybackButton
            audioResourceId={word.audioResourceId}
            label={`朗读 ${word.headword}`}
          />
        </div>
      ) : null}

      <div className="mt-5 min-w-0 space-y-6">
        {word.senses.map((sense) => (
          <section
            className="min-w-0"
            key={`${sense.sortOrder}-${sense.partOfSpeech}`}
          >
            <h3 className="font-semibold">{sense.partOfSpeech}</h3>
            <p className="mt-1 wrap-break-word">{sense.definition}</p>
            {sense.usageNote ? (
              <p className="mt-1 wrap-break-word text-sm text-base-content/70">
                {sense.usageNote}
              </p>
            ) : null}
            {sense.examples.length > 0 ? (
              <ul className="mt-3 space-y-3">
                {sense.examples.map((example) => (
                  <li
                    className="min-w-0"
                    key={`${example.sortOrder}-${example.sentence}`}
                  >
                    <p className="wrap-break-word">{example.sentence}</p>
                    <p className="wrap-break-word text-sm text-base-content/70">
                      {example.translation}
                    </p>
                    {example.audioResourceId ? (
                      <AudioPlaybackButton
                        audioResourceId={example.audioResourceId}
                        label={`朗读例句 ${example.sentence}`}
                      />
                    ) : null}
                  </li>
                ))}
              </ul>
            ) : null}
          </section>
        ))}
      </div>

      <div className="mt-6 flex justify-end">
        <button
          className="btn btn-error"
          disabled={removing}
          type="button"
          onClick={onRemove}
        >
          取消收藏
        </button>
      </div>
    </section>
  );
}
