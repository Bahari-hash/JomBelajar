import { useEffect, useState } from "react";
import WordStudyWorkspace from "@/features/wordStudy/WordStudyWorkspace";
import {
  useExcludeReviewItemMutation,
  useGetReviewOverviewQuery,
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
  const [session, setSession] = useState<WordStudySessionState | null>(null);
  useEffect(() => {
    if (overview.data?.activeSession) setSession(overview.data.activeSession);
    else if (
      (overview.data?.dueCount ?? 0) > 0 &&
      !startState.isLoading &&
      !session
    )
      void start().unwrap().then(setSession);
  }, [overview.data, session, start, startState.isLoading]);
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
      onToggleFavorite={async (item: WordStudyCurrentItem) => {
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
      }}
      onExclude={(item) => {
        if (window.confirm("确认停止复习这个单词吗？"))
          void exclude({
            sessionId: session.id,
            itemId: item.itemId,
            itemConcurrencyStamp: item.itemConcurrencyStamp,
          })
            .unwrap()
            .then((response) => setSession(response.session));
      }}
    />
  );
}
