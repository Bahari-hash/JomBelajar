import { useEffect, useState } from "react";
import WordStudyWorkspace from "@/features/wordStudy/WordStudyWorkspace";
import {
  useGetLearningOverviewQuery,
  useSetFavoriteMutation,
  useStartLearningMutation,
  useSubmitLearningMemorizationMutation,
  useSubmitLearningSpellingMutation,
} from "@/features/wordStudy/wordStudyApi";
import type {
  WordSpellingResult,
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
  const [session, setSession] = useState<WordStudySessionState | null>(null);
  useEffect(() => {
    if (overview.data?.activeSession) setSession(overview.data.activeSession);
    else if (overview.data?.hasMoreWords && !startState.isLoading && !session)
      void start().unwrap().then(setSession);
  }, [overview.data, session, start, startState.isLoading]);
  if (!session)
    return (
      <div className="mx-auto max-w-3xl py-16 text-center">
        <span className="loading loading-spinner" />
        <p className="mt-3">正在准备新词</p>
      </div>
    );
  const updateFavorite = async (item: WordStudyCurrentItem) => {
    await setFavorite({
      wordId: item.wordId,
      favorite: !item.isFavorite,
    }).unwrap();
    setSession((current) =>
      current?.currentItem
        ? {
            ...current,
            currentItem: {
              ...current.currentItem,
              isFavorite: !item.isFavorite,
            } as WordStudyCurrentItem,
          }
        : current,
    );
  };
  return (
    <WordStudyWorkspace
      mode="learning"
      session={session}
      submitting={memorizeState.isLoading || spellState.isLoading}
      onMemorization={async (item, result: WordMemorizationResult) => {
        const response = await memorize({
          sessionId: session.id,
          itemId: item.itemId,
          result,
          itemConcurrencyStamp: item.itemConcurrencyStamp,
        }).unwrap();
        setSession(response.session);
      }}
      onSpelling={async (item, answer): Promise<WordSpellingResult | null> => {
        const response = await spell({
          sessionId: session.id,
          itemId: item.itemId,
          answer,
          itemConcurrencyStamp: item.itemConcurrencyStamp,
        }).unwrap();
        setSession(response.session);
        return response.spellingResult;
      }}
      onToggleFavorite={updateFavorite}
    />
  );
}
