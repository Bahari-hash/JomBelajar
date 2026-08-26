import { X } from "lucide-react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import type { WordFavorite } from "./wordStudyTypes";

interface WordFavoriteDetailsProps {
  word: WordFavorite;
  onClose: () => void;
  onRemove: () => void;
}

export default function WordFavoriteDetails({
  word,
  onClose,
  onRemove,
}: WordFavoriteDetailsProps) {
  return (
    <div
      className="modal modal-open"
      role="dialog"
      aria-modal="true"
      aria-labelledby="favorite-word-details-title"
    >
      <section className="modal-box relative w-11/12 max-w-3xl">
        <button
          className="btn btn-ghost btn-sm btn-circle absolute right-2 top-2"
          aria-label="关闭收藏详情"
          type="button"
          onClick={onClose}
        >
          <X aria-hidden="true" className="size-4" />
        </button>

        <h2 id="favorite-word-details-title" className="text-xl font-semibold">
          {word.headword}
        </h2>
        {word.audioResourceId ? (
          <div className="mt-3">
            <AudioPlaybackButton
              audioResourceId={word.audioResourceId}
              label={`朗读 ${word.headword}`}
            />
          </div>
        ) : null}

        <div className="mt-5 space-y-6">
          {word.senses.map((sense) => (
            <section key={`${sense.sortOrder}-${sense.partOfSpeech}`}>
              <h3 className="font-semibold">{sense.partOfSpeech}</h3>
              <p className="mt-1">{sense.definition}</p>
              {sense.usageNote ? (
                <p className="mt-1 text-sm text-base-content/70">
                  {sense.usageNote}
                </p>
              ) : null}
              {sense.examples.length > 0 ? (
                <ul className="mt-3 space-y-3">
                  {sense.examples.map((example) => (
                    <li key={`${example.sortOrder}-${example.sentence}`}>
                      <p>{example.sentence}</p>
                      <p className="text-sm text-base-content/70">
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

        <div className="modal-action">
          <button className="btn btn-error" type="button" onClick={onRemove}>
            取消收藏
          </button>
        </div>
      </section>
    </div>
  );
}
