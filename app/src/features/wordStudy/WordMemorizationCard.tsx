import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import type { WordStudyCurrentItem } from "@/features/wordStudy/wordStudyTypes";

export default function WordMemorizationCard({
  item,
  revealed = false,
}: {
  revealed?: boolean;
  item: Extract<WordStudyCurrentItem, { phase: "Memorization" }>;
}) {
  return (
    <div className="space-y-6">
      <div className="flex min-h-48 flex-col items-center justify-center gap-5 text-center">
        <h2 className="max-w-full wrap-break-word text-4xl font-semibold sm:text-5xl">{item.memorization.headword}</h2>
        {item.memorization.audioResourceId ? (
          <AudioPlaybackButton
            audioResourceId={item.memorization.audioResourceId}
            label="单词音频"
          />
        ) : null}
      </div>
      {revealed ? <div className="space-y-5" id="word-answer">
        {item.memorization.senses.map((sense) => (
          <section
            key={`${sense.sortOrder}-${sense.definition}`}
            className="min-w-0 border-t border-base-300 pt-4"
          >
            <h3 className="wrap-break-word text-sm text-primary">
              {sense.partOfSpeech} 释义
            </h3>
            <div className="mt-3">
              <p className="text-lg">{sense.definition}</p>
              {sense.usageNote ? (
                <p className="mt-1 text-base-content/60">{sense.usageNote}</p>
              ) : null}
              {sense.examples.length > 0 ? (
                <section className="mt-3">
                  <h4 className="font-medium">
                    例句（{sense.examples.length}）
                  </h4>
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
                </section>
              ) : null}
            </div>
          </section>
        ))}
      </div> : <p className="text-center text-base-content/60">先回忆它的含义，再显示答案。</p>}
    </div>
  );
}
