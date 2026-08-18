import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import WordStudyWorkspace from "@/features/wordStudy/WordStudyWorkspace";
import {
  useExcludeReviewItemMutation,
  useGetReviewOverviewQuery,
  useLazyGetReviewSessionQuery,
  useSetFavoriteMutation,
  useStartReviewMutation,
  useSubmitReviewMemorizationMutation,
  useSubmitReviewSpellingMutation,
} from "@/features/wordStudy/wordStudyApi";
import type {
  WordSpellingResult,
  WordStudyCurrentItem,
  WordStudySessionState,
  WordMemorizationResult,
} from "@/features/wordStudy/wordStudyTypes";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordReviewPage() {
  useDocumentTitle("旧词复习");
  const overview = useGetReviewOverviewQuery();
  const [start, startState] = useStartReviewMutation();
  const [memorize, memorizeState] = useSubmitReviewMemorizationMutation();
  const [spell, spellState] = useSubmitReviewSpellingMutation();
  const [exclude] = useExcludeReviewItemMutation();
  const [setFavorite] = useSetFavoriteMutation();
  const [reloadSession] = useLazyGetReviewSessionQuery();
  const [session, setSession] = useState<WordStudySessionState | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (
      overview.data?.activeSession &&
      !error &&
      session?.id !== overview.data.activeSession.id
    )
      void reloadSession(overview.data.activeSession.id)
        .unwrap()
        .then(setSession)
        .catch(() => setError("复习会话加载失败，请重试。"));
    else if (
      (overview.data?.dueCount ?? 0) > 0 &&
      !startState.isLoading &&
      !session &&
      !error
    )
      void start()
        .unwrap()
        .then(setSession)
        .catch(() => setError("复习会话创建失败，请重试。"));
  }, [
    error,
    overview.data,
    reloadSession,
    session,
    start,
    startState.isLoading,
  ]);
  if (!session && overview.data?.dueCount === 0) {
    return (
      <section className="mx-auto max-w-3xl py-16 text-center">
        <h1 className="text-2xl font-bold">当前没有到期复习</h1>
        <p className="mt-3 text-base-content/65">
          完成新词学习后，单词会在到期时出现在这里。
        </p>
        <Link className="btn btn-primary mt-6" to="/words">
          返回单词首页
        </Link>
      </section>
    );
  }
  if (!session && (overview.isError || startState.isError || error))
    return (
      <section className="mx-auto max-w-3xl py-16 text-center">
        <p className="alert alert-error">
          {error ?? "复习暂时无法加载，请重试。"}
        </p>
        <button
          className="btn btn-primary mt-6"
          type="button"
          onClick={() => {
            setError(null);
            void overview.refetch();
          }}
        >
          重新加载
        </button>
      </section>
    );
  if (!session)
    return (
      <div className="mx-auto max-w-3xl py-16 text-center">
        <span className="loading loading-spinner" />
        <p className="mt-3">正在准备复习</p>
      </div>
    );
  return (
    <WordStudyWorkspace
      mode="review"
      session={session}
      submitting={memorizeState.isLoading || spellState.isLoading}
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
      onSpelling={async (item, answer): Promise<WordSpellingResult | null> => {
        try {
          const response = await spell({
            sessionId: session.id,
            itemId: item.itemId,
            answer,
            itemConcurrencyStamp: item.itemConcurrencyStamp,
          }).unwrap();
          setSession(response.session);
          return response.spellingResult;
        } catch (requestError) {
          setSession(await reloadSession(session.id).unwrap());
          throw requestError;
        }
      }}
      onToggleFavorite={async (item: WordStudyCurrentItem) => {
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
      }}
      onExclude={(item) =>
        exclude({
          sessionId: session.id,
          itemId: item.itemId,
          itemConcurrencyStamp: item.itemConcurrencyStamp,
        })
          .unwrap()
          .then((response) => setSession(response.session))
          .then(() => undefined)
      }
      canContinue={(overview.data?.dueCount ?? 0) > 0}
      showTodayReview
      onContinue={async () => {
        const latest = await overview.refetch().unwrap();
        if (latest.dueCount === 0) return;
        setSession(await start().unwrap());
      }}
    />
  );
}
