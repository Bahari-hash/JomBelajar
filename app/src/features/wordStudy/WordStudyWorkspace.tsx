import { Heart, HeartOff } from "lucide-react";
import { useState } from "react";
import WordStudySummary from "./WordStudySummary";
import WordRatingCard from "./WordRatingCard";
import WordSpellingCard from "./WordSpellingCard";
import WordStudyCompletion from "./WordStudyCompletion";
import WordReviewExcludeDialog from "./WordReviewExcludeDialog";
import type {
  WordMemorizationResult,
  WordStudyCommandResponse,
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
  ) => Promise<WordStudyCommandResponse | null>;
  onSpellingAdvance?: (session: WordStudySessionState) => void;
  onToggleFavorite: (item: WordStudyCurrentItem) => Promise<void>;
  onExclude?: (item: WordStudyCurrentItem) => Promise<void>;
  onContinue?: () => Promise<void>;
  canContinue?: boolean;
  showTodayReview?: boolean;
  onSummaryChoice?: (skipSpelling: boolean) => Promise<void>;
  onReload?: () => Promise<void>;
  onWaitingAction?: (action: "more" | "spelling") => Promise<void>;
}

export default function WordStudyWorkspace(props: Props) {
  const [favoriteError, setFavoriteError] = useState<string | null>(null);
  const [commandError, setCommandError] = useState<string | null>(null);
  const [excludeItem, setExcludeItem] = useState<WordStudyCurrentItem | null>(
    null,
  );
  const [excludeSubmitting, setExcludeSubmitting] = useState(false);
  const [excludeError, setExcludeError] = useState<string | null>(null);
  if (props.session.status === "Completed")
    return (
      <WordStudyCompletion
        session={props.session}
        continueLabel="继续学习"
        canContinue={props.canContinue ?? true}
        onContinue={props.onContinue}
        showTodayReview={props.showTodayReview}
      />
    );
  if (props.session.phase === "Summary") return <WordStudySummary session={props.session} onChoose={props.onSummaryChoice} />;
  if (!props.session.currentItem) return <div className="py-16 text-center" role="status">
    <p>暂时没有可显示的卡片，请刷新学习进度。</p>
    <button className="btn btn-primary mt-4" onClick={() => void props.onReload?.()}>刷新进度</button>
  </div>;
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
      throw new Error("rating failed");
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
        {item.phase === "Memorization" && props.session.newCount !== undefined ? (
          <div className="flex justify-center gap-5 text-sm" aria-label="本组待学卡片">
            <span className="text-info">新词 {props.session.newCount}</span>
            <span className="text-warning">学习中 {props.session.learningCount}</span>
            <span className="text-success">复习 {props.session.reviewCount}</span>
          </div>
        ) : null}
        <section className="min-h-[28rem]">
          {item.phase === "Memorization" ? (
            <>
              <WordRatingCard key={`${item.itemId}-${item.itemConcurrencyStamp}`}
                item={item} submitting={props.submitting || excludeItem !== null}
                onRate={submitMemorization} />
              {props.mode === "review" && props.onExclude ? (
                <div className="mt-6 text-center"><button className="btn btn-ghost btn-sm text-base-content/60"
                  disabled={props.submitting} onClick={() => { setExcludeError(null); setExcludeItem(item); }}>
                  不再复习此词
                </button></div>
              ) : null}
            </>
          ) : (
            <>
              <WordSpellingCard
                item={item}
                submitting={props.submitting}
                onSubmit={submitSpelling}
                onExit={props.onSummaryChoice ? () => props.onSummaryChoice!(true) : undefined}
                onAdvance={props.onSpellingAdvance ?? (() => undefined)}
              />
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
