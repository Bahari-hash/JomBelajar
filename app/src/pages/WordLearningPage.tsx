import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import WordStudyWorkspace from "@/features/wordStudy/WordStudyWorkspace";
import {
  useGetLearningOverviewQuery,
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
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordLearningPage() {
  useDocumentTitle("新词学习");
  const overview = useGetLearningOverviewQuery();
  const [start, startState] = useStartLearningMutation();
  const [memorize, memorizeState] = useSubmitLearningMemorizationMutation();
  const [spell, spellState] = useSubmitLearningSpellingMutation();
  const [setFavorite] = useSetFavoriteMutation();
  const [reloadSession] = useLazyGetLearningSessionQuery();
  const [session, setSession] = useState<WordStudySessionState | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (
      overview.data?.activeSession &&
      !error &&
      session?.id !== overview.data.activeSession.id
    ) {
      void reloadSession(overview.data.activeSession.id)
        .unwrap()
        .then(setSession)
        .catch(() => setError("新词学习会话加载失败，请重试。"));
    } else if (
      overview.data?.hasMoreWords &&
      !startState.isLoading &&
      !session &&
      !error
    ) {
      void start()
        .unwrap()
        .then(setSession)
        .catch(() => setError("新词学习会话创建失败，请重试。"));
    }
  }, [
    error,
    overview.data,
    reloadSession,
    session,
    start,
    startState.isLoading,
  ]);
  if (!session && overview.data?.hasMoreWords === false) {
    return (
      <section className="mx-auto max-w-3xl py-16 text-center">
        <h1 className="text-2xl font-bold">新词已经全部学完</h1>
        <p className="mt-3 text-base-content/65">
          稍后可以在复习模块巩固已学单词。
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
          {error ?? "新词学习暂时无法加载，请重试。"}
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
        <p className="mt-3">正在准备新词</p>
      </div>
    );
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
      mode="learning"
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
      canContinue={overview.data?.hasMoreWords ?? false}
      showTodayReview
      onContinue={async () => {
        const latest = await overview.refetch().unwrap();
        if (!latest.hasMoreWords) return;
        setSession(await start().unwrap());
      }}
    />
  );
}
