import { Heart, HeartOff } from "lucide-react";
import { useState } from "react";
import WordMemorizationCard from "./WordMemorizationCard";
import WordSpellingCard from "./WordSpellingCard";
import WordStudyCompletion from "./WordStudyCompletion";
import WordReviewExcludeDialog from "./WordReviewExcludeDialog";
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
  onExclude?: (item: WordStudyCurrentItem) => Promise<void>;
  onContinue?: () => Promise<void>;
  canContinue?: boolean;
  showTodayReview?: boolean;
}

export default function WordStudyWorkspace(props: Props) {
  const [favoriteError, setFavoriteError] = useState<string | null>(null);
  const [commandError, setCommandError] = useState<string | null>(null);
  const [excludeItem, setExcludeItem] = useState<WordStudyCurrentItem | null>(
    null,
  );
  const [excludeSubmitting, setExcludeSubmitting] = useState(false);
  const [excludeError, setExcludeError] = useState<string | null>(null);
  if (props.session.status === "Completed" || !props.session.currentItem)
    return (
      <WordStudyCompletion
        session={props.session}
        continueLabel={props.mode === "learning" ? "再学一组" : "再复习一组"}
        canContinue={props.canContinue ?? true}
        onContinue={props.onContinue}
        showTodayReview={props.showTodayReview}
      />
    );
  const item = props.session.currentItem;
  const validTotal =
    props.session.actualCount -
    props.session.excludedCount -
    props.session.skippedCount;
  const passed =
    item.phase === "Memorization"
      ? props.session.memorizationPassedCount
      : props.session.spellingPassedCount;
  const toggleFavorite = async () => {
    setFavoriteError(null);
    try {
      await props.onToggleFavorite(item);
    } catch {
      setFavoriteError("收藏状态更新失败，请重试。");
    }
  };
  const confirmExclude = async () => {
    if (!excludeItem || !props.onExclude) return;
    setExcludeSubmitting(true);
    setExcludeError(null);
    try {
      await props.onExclude(excludeItem);
      setExcludeItem(null);
    } catch {
      setExcludeError("停止复习失败，请重试。");
    } finally {
      setExcludeSubmitting(false);
    }
  };
  const submitMemorization = async (result: WordMemorizationResult) => {
    setCommandError(null);
    try {
      await props.onMemorization(item, result);
    } catch {
      setCommandError("学习进度保存失败，已重新同步当前状态。");
    }
  };
  const submitSpelling = async (answer: string) => {
    setCommandError(null);
    try {
      return await props.onSpelling(item, answer);
    } catch {
      setCommandError("拼写结果保存失败，已重新同步当前状态。");
      return null;
    }
  };
  return (
    <>
      <main className="mx-auto max-w-3xl space-y-6">
        {favoriteError ? (
          <p className="alert alert-error" role="alert">
            {favoriteError}
          </p>
        ) : null}
        {commandError ? (
          <p className="alert alert-error" role="alert">
            {commandError}
          </p>
        ) : null}
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
            aria-label={item.isFavorite ? "取消收藏" : "收藏"}
            type="button"
            onClick={() => void toggleFavorite()}
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
                  onClick={() => void submitMemorization("Forgotten")}
                >
                  没记住
                </button>
                <div className="flex gap-3">
                  {props.mode === "review" && props.onExclude ? (
                    <button
                      className="btn btn-ghost text-error"
                      disabled={props.submitting}
                      onClick={() => {
                        setExcludeError(null);
                        setExcludeItem(item);
                      }}
                    >
                      不再复习此词
                    </button>
                  ) : null}
                  <button
                    className="btn btn-primary"
                    disabled={props.submitting}
                    onClick={() => void submitMemorization("Remembered")}
                  >
                    记住了
                  </button>
                </div>
              </div>
            </>
          ) : (
            <>
              <WordSpellingCard
                item={item}
                submitting={props.submitting}
                onSubmit={submitSpelling}
              />
              {props.mode === "review" && props.onExclude ? (
                <button
                  className="btn btn-ghost mt-6 text-error"
                  disabled={props.submitting}
                  type="button"
                  onClick={() => {
                    setExcludeError(null);
                    setExcludeItem(item);
                  }}
                >
                  不再复习此词
                </button>
              ) : null}
            </>
          )}
        </section>
      </main>
      <WordReviewExcludeDialog
        open={excludeItem !== null}
        submitting={excludeSubmitting}
        error={excludeError}
        onCancel={() => setExcludeItem(null)}
        onConfirm={confirmExclude}
      />
    </>
  );
}
