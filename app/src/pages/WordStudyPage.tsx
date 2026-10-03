import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import WordStudyWorkspace from "@/features/wordStudy/WordStudyWorkspace";
import {
  useGetLearningOverviewQuery,
  useGetReviewOverviewQuery,
  useLazyGetReviewSessionQuery,
  useStartReviewMutation,
  useSubmitReviewMemorizationMutation,
  useSubmitReviewSpellingMutation,
  useExcludeReviewItemMutation,
  useLazyGetLearningSessionQuery,
  useSetFavoriteMutation,
  useStartLearningMutation,
  useSubmitLearningMemorizationMutation,
  useSubmitLearningSpellingMutation,
} from "@/features/wordStudy/wordStudyApi";
import type {
  WordStudyCommandResponse,
  WordStudyCurrentItem,
  WordStudySessionState,
  WordMemorizationResult,
} from "@/features/wordStudy/wordStudyTypes";
import { useFinishSummaryMutation } from "@/features/wordStudy/wordStudyApi";
import { chooseStudyGroup } from "@/features/wordStudy/chooseStudyGroup";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordStudyPage() {
  useDocumentTitle("单词学习");
  const learning = useGetLearningOverviewQuery(undefined, { refetchOnMountOrArgChange: true });
  const review = useGetReviewOverviewQuery(undefined, { refetchOnMountOrArgChange: true });
  const [startLearning] = useStartLearningMutation();
  const [startReview] = useStartReviewMutation();
  const [learningMemorize, learningMemorizeState] = useSubmitLearningMemorizationMutation();
  const [reviewMemorize, reviewMemorizeState] = useSubmitReviewMemorizationMutation();
  const [learningSpell, learningSpellState] = useSubmitLearningSpellingMutation();
  const [reviewSpell, reviewSpellState] = useSubmitReviewSpellingMutation();
  const [reloadLearning] = useLazyGetLearningSessionQuery();
  const [reloadReview] = useLazyGetReviewSessionQuery();
  const [exclude] = useExcludeReviewItemMutation();
  const [setFavorite] = useSetFavoriteMutation();
  const [finishSummary] = useFinishSummaryMutation();
  const [session, setSession] = useState<WordStudySessionState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [empty, setEmpty] = useState(false);
  const opening = useRef(false);

  const openGroup = async (fresh = false) => {
    if (opening.current) return;
    opening.current = true;
    try {
      const [l, r] = fresh
        ? await Promise.all([learning.refetch().unwrap(), review.refetch().unwrap()])
        : [learning.data!, review.data!];
      const choice = chooseStudyGroup(l, r);
      if (!choice) { setEmpty(true); setSession(null); return; }
      const reload = choice.mode === "review" ? reloadReview : reloadLearning;
      const start = choice.mode === "review" ? startReview : startLearning;
      setSession(await (choice.sessionId ? reload(choice.sessionId) : start()).unwrap());
      setEmpty(false);
    } finally { opening.current = false; }
  };

  useEffect(() => {
    if (learning.data && review.data && !learning.isFetching && !review.isFetching &&
        !learning.isError && !review.isError && !session && !error && !empty) {
      void openGroup().catch(() => setError("单词学习加载失败，请重试。"));
    }
  });

  if (!session && (error || learning.isError || review.isError)) return (
    <section className="mx-auto max-w-3xl py-16 text-center">
      <p className="alert alert-error" role="alert">{error ?? "单词学习加载失败，请重试。"}</p>
      <button className="btn btn-primary mt-6" onClick={() => {
        void openGroup(true).then(() => setError(null)).catch(() => setError("单词学习加载失败，请重试。"));
      }}>重新加载</button>
    </section>
  );
  if (!session && empty) return (
    <section className="mx-auto max-w-3xl py-16 text-center">
      <h1 className="text-2xl font-bold">当前学习已完成</h1>
      <p className="mt-3 text-base-content/65">没有到期复习或待学新词，之后有单词到期时再来学习。</p>
      <Link className="btn btn-primary mt-6" to="/words">返回单词首页</Link>
    </section>
  );
  if (!session) return <div className="mx-auto max-w-3xl py-16 text-center" role="status">
    <span className="loading loading-spinner" /><p className="mt-3">正在准备单词学习</p>
  </div>;
  const mode = session.sessionType === "Review" ? "review" : "learning";
  const reloadSession = mode === "review" ? reloadReview : reloadLearning;
  const memorize = mode === "review" ? reviewMemorize : learningMemorize;
  const spell = mode === "review" ? reviewSpell : learningSpell;
  const updateFavorite = async (item: WordStudyCurrentItem) => {
    const nextFavorite = !item.isFavorite;
    setSession((current) =>
      current?.currentItem?.wordId === item.wordId
        ? {
            ...current,
            currentItem: {
              ...current.currentItem,
              isFavorite: nextFavorite,
            } as WordStudyCurrentItem,
          }
        : current,
    );
    try {
      await setFavorite({
        wordId: item.wordId,
        favorite: nextFavorite,
      }).unwrap();
    } catch (requestError) {
      setSession((current) =>
        current?.currentItem?.wordId === item.wordId
          ? {
              ...current,
              currentItem: {
                ...current.currentItem,
                isFavorite: item.isFavorite,
              } as WordStudyCurrentItem,
            }
          : current,
      );
      throw requestError;
    }
  };
  return (
    <WordStudyWorkspace
      mode={mode}
      session={session}
      onSummaryChoice={async (skipSpelling) => {
        try { setSession(await finishSummary({ mode, sessionId: session.id, skipSpelling }).unwrap()); }
        catch (error) { setSession(await reloadSession(session.id).unwrap()); throw error; }
      }}
      onReload={async () => { setSession(await reloadSession(session.id).unwrap()); }}
      submitting={learningMemorizeState.isLoading || reviewMemorizeState.isLoading || learningSpellState.isLoading || reviewSpellState.isLoading}
      onMemorization={async (item, result: WordMemorizationResult) => {
        try {
          const response = await memorize({
            sessionId: session.id,
            itemId: item.itemId,
            result,
            itemConcurrencyStamp: item.itemConcurrencyStamp,
          }).unwrap();
          setSession(response.session);
        } catch (requestError) {
          setSession(await reloadSession(session.id).unwrap());
          throw requestError;
        }
      }}
      onSpelling={async (item, answer): Promise<WordStudyCommandResponse | null> => {
        try {
          const response = await spell({
            sessionId: session.id,
            itemId: item.itemId,
            answer,
            itemConcurrencyStamp: item.itemConcurrencyStamp,
          }).unwrap();
          return response;
        } catch (requestError) {
          setSession(await reloadSession(session.id).unwrap());
          throw requestError;
        }
      }}
      onSpellingAdvance={setSession}
      onToggleFavorite={updateFavorite}
      onExclude={mode === "review" ? async (item) => {
        try {
          const response = await exclude({ sessionId: session.id, itemId: item.itemId, itemConcurrencyStamp: item.itemConcurrencyStamp }).unwrap();
          setSession(response.session);
        } catch (requestError) { setSession(await reloadSession(session.id).unwrap()); throw requestError; }
      } : undefined}
      canContinue
      showTodayReview
      onContinue={() => openGroup(true)}
    />
  );
}
