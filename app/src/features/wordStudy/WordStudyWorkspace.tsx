import { Heart, HeartOff } from "lucide-react";
import WordMemorizationCard from "./WordMemorizationCard";
import WordSpellingCard from "./WordSpellingCard";
import WordStudyCompletion from "./WordStudyCompletion";
import type {
  WordMemorizationResult,
  WordSpellingResult,
  WordStudyCurrentItem,
  WordStudySessionState,
} from "./wordStudyTypes";

interface Props {
  mode: "learning" | "review";
  session: WordStudySessionState;
  submitting: boolean;
  onMemorization: (
    item: WordStudyCurrentItem,
    result: WordMemorizationResult,
  ) => Promise<void>;
  onSpelling: (
    item: WordStudyCurrentItem,
    answer: string,
  ) => Promise<WordSpellingResult | null>;
  onToggleFavorite: (item: WordStudyCurrentItem) => Promise<void>;
  onExclude?: (item: WordStudyCurrentItem) => void;
}

export default function WordStudyWorkspace(props: Props) {
  if (props.session.status === "Completed" || !props.session.currentItem)
    return <WordStudyCompletion session={props.session} />;
  const item = props.session.currentItem;
  const validTotal =
    props.session.actualCount -
    props.session.excludedCount -
    props.session.skippedCount;
  const passed =
    item.phase === "Memorization"
      ? props.session.memorizationPassedCount
      : props.session.spellingPassedCount;
  return (
    <main className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center justify-between border-b border-base-300 pb-4">
        <div>
          <p className="text-sm text-base-content/60">
            {item.phase === "Memorization" ? "记忆阶段" : "拼写阶段"}
          </p>
          <p className="font-medium">
            {passed} / {validTotal}
          </p>
        </div>
        <button
          className="btn btn-ghost btn-square"
          title={item.isFavorite ? "取消收藏" : "收藏"}
          type="button"
          onClick={() => void props.onToggleFavorite(item)}
        >
          {item.isFavorite ? (
            <HeartOff className="size-5" />
          ) : (
            <Heart className="size-5" />
          )}
        </button>
      </div>
      <section className="min-h-[28rem]">
        {item.phase === "Memorization" ? (
          <>
            <WordMemorizationCard item={item} />
            <div className="mt-8 flex flex-wrap justify-between gap-3">
              <button
                className="btn btn-outline"
                disabled={props.submitting}
                onClick={() => void props.onMemorization(item, "Forgotten")}
              >
                没记住
              </button>
              <div className="flex gap-3">
                {props.mode === "review" && props.onExclude ? (
                  <button
                    className="btn btn-ghost text-error"
                    disabled={props.submitting}
                    onClick={() => props.onExclude?.(item)}
                  >
                    不再复习
                  </button>
                ) : null}
                <button
                  className="btn btn-primary"
                  disabled={props.submitting}
                  onClick={() => void props.onMemorization(item, "Remembered")}
                >
                  记住了
                </button>
              </div>
            </div>
          </>
        ) : (
          <WordSpellingCard
            item={item}
            submitting={props.submitting}
            onSubmit={(answer) => props.onSpelling(item, answer)}
          />
        )}
      </section>
    </main>
  );
}
