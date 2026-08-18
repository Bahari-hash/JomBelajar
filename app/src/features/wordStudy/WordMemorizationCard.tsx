import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import type { WordStudyCurrentItem } from "@/features/wordStudy/wordStudyTypes";

export default function WordMemorizationCard({
  item,
}: {
  item: Extract<WordStudyCurrentItem, { phase: "Memorization" }>;
}) {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <h2 className="text-3xl font-bold">{item.memorization.headword}</h2>
        {item.memorization.audioResourceId ? (
          <AudioPlaybackButton
            audioResourceId={item.memorization.audioResourceId}
            label="单词音频"
          />
        ) : null}
      </div>
      <div className="space-y-5">
        {item.memorization.senses.map((sense) => (
          <details
            key={`${sense.sortOrder}-${sense.definition}`}
            className="min-w-0 border-t border-base-300 pt-4"
          >
            <summary className="cursor-pointer wrap-break-word text-sm text-primary">
              {sense.partOfSpeech} 释义
            </summary>
            <div className="mt-3">
              <p className="text-lg">{sense.definition}</p>
              {sense.usageNote ? (
                <p className="mt-1 text-base-content/60">{sense.usageNote}</p>
              ) : null}
              {sense.examples.length > 0 ? (
                <details className="mt-3">
                  <summary className="cursor-pointer">
                    例句（{sense.examples.length}）
                  </summary>
                  <div className="mt-2 space-y-2">
                    {sense.examples.map((example) => (
                      <div
                        key={`${example.sortOrder}-${example.sentence}`}
                        className="flex min-w-0 items-start justify-between gap-3"
                      >
                        <div className="min-w-0 flex-1 wrap-break-word">
                          <p>{example.sentence}</p>
                          <p className="text-sm text-base-content/60">
                            {example.translation}
                          </p>
                        </div>
                        {example.audioResourceId ? (
                          <AudioPlaybackButton
                            audioResourceId={example.audioResourceId}
                            label="例句音频"
                            variant="icon"
                          />
                        ) : null}
                      </div>
                    ))}
                  </div>
                </details>
              ) : null}
            </div>
          </details>
        ))}
      </div>
    </div>
  );
}
