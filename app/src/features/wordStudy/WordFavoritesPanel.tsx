import { ChevronLeft, ChevronRight, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import WordFavoriteDetails from "./WordFavoriteDetails";
import { useGetFavoritesQuery, useSetFavoriteMutation } from "./wordStudyApi";
import type { WordFavorite } from "./wordStudyTypes";

type WordFavoritesPanelProps = {
  modal?: boolean;
  onClose?: () => void;
};

export default function WordFavoritesPanel({
  modal = false,
  onClose,
}: WordFavoritesPanelProps) {
  const [page, setPage] = useState(1);
  const [selectedWord, setSelectedWord] = useState<WordFavorite | null>(null);
  const query = useGetFavoritesQuery({ page, pageSize: 20 });
  const [setFavorite] = useSetFavoriteMutation();
  const [error, setError] = useState<string | null>(null);
  const openersRef = useRef(new Map<string, HTMLButtonElement>());
  const openerWordIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (query.data && page > query.data.totalPages) {
      setPage(Math.max(1, query.data.totalPages));
    }
  }, [page, query.data]);

  useEffect(() => {
    if (selectedWord || !openerWordIdRef.current) return;
    openersRef.current.get(openerWordIdRef.current)?.focus();
    openerWordIdRef.current = null;
  }, [selectedWord]);

  const closeDetails = () => {
    setError(null);
    setSelectedWord(null);
  };

  const openDetails = (word: WordFavorite) => {
    openerWordIdRef.current = word.wordId;
    setError(null);
    setSelectedWord(word);
  };

  const remove = async (wordId: string) => {
    setError(null);
    try {
      await setFavorite({ wordId, favorite: false }).unwrap();
      if (selectedWord?.wordId === wordId) {
        openerWordIdRef.current = null;
        setSelectedWord(null);
      }
    } catch {
      setError("收藏状态更新失败，请重试。");
    }
  };

  const content = (
    <section
      className={modal ? undefined : "border-t border-base-300 py-8"}
      onKeyDown={(event) => {
        if (selectedWord && event.key === "Escape") {
          event.preventDefault();
          event.stopPropagation();
          closeDetails();
        }
      }}
    >
      {query.isError || error ? (
        <p className="alert alert-error mt-4 text-sm" role="alert">
          {error ?? "收藏本暂时无法加载。"}
        </p>
      ) : null}
      {query.isError ? (
        <button
          className="btn btn-ghost btn-sm mt-3"
          type="button"
          onClick={() => void query.refetch()}
        >
          重新加载
        </button>
      ) : null}
      {selectedWord ? (
        <WordFavoriteDetails
          word={selectedWord}
          onClose={closeDetails}
          onRemove={() => void remove(selectedWord.wordId)}
        />
      ) : (
        <>
          <h2
            id={modal ? "favorites-panel-title" : undefined}
            className="text-xl font-semibold"
          >
            收藏本
          </h2>
          <div className="mt-5 divide-y divide-base-300">
            {query.data?.items.map((word) => (
              <div
                className="flex flex-col items-stretch gap-3 py-4 sm:flex-row sm:items-center sm:justify-between"
                key={word.wordId}
              >
                <button
                  ref={(element) => {
                    if (element) openersRef.current.set(word.wordId, element);
                    else openersRef.current.delete(word.wordId);
                  }}
                  className="btn btn-ghost h-auto min-h-0 min-w-0 flex-1 justify-start px-0 py-0 text-left"
                  aria-label={`查看 ${word.headword}`}
                  onClick={() => openDetails(word)}
                  type="button"
                >
                  <span className="min-w-0">
                    <span className="block wrap-break-word font-semibold">
                      {word.headword}
                    </span>
                    <span className="block wrap-break-word text-sm text-base-content/60">
                      {word.senses[0]?.definition}
                    </span>
                  </span>
                </button>
                <div className="flex flex-wrap gap-2 sm:justify-end">
                  {word.audioResourceId ? (
                    <AudioPlaybackButton
                      audioResourceId={word.audioResourceId}
                      variant="icon"
                    />
                  ) : null}
                  <button
                    className="btn btn-ghost btn-sm"
                    aria-label={`取消收藏 ${word.headword}`}
                    onClick={() => void remove(word.wordId)}
                    type="button"
                  >
                    取消收藏
                  </button>
                </div>
              </div>
            ))}
          </div>
          {query.data?.totalCount === 0 ? (
            <p className="mt-4 text-base-content/60">暂无收藏单词</p>
          ) : null}
          {query.data && query.data.totalPages > 1 ? (
            <nav
              className="mt-5 flex items-center justify-between"
              aria-label="收藏本分页"
            >
              <button
                className="btn btn-ghost btn-square"
                aria-label="上一页"
                disabled={page <= 1}
                type="button"
                onClick={() => setPage((value) => value - 1)}
              >
                <ChevronLeft className="size-4" />
              </button>
              <span className="text-sm">
                第 {query.data.page} / {query.data.totalPages} 页
              </span>
              <button
                className="btn btn-ghost btn-square"
                aria-label="下一页"
                disabled={page >= query.data.totalPages}
                type="button"
                onClick={() => setPage((value) => value + 1)}
              >
                <ChevronRight className="size-4" />
              </button>
            </nav>
          ) : null}
        </>
      )}
    </section>
  );

  if (!modal) return content;

  return (
    <div
      className="modal modal-open"
      role="dialog"
      aria-modal="true"
      aria-labelledby={
        selectedWord ? "favorite-word-details-title" : "favorites-panel-title"
      }
    >
      <div className="modal-box relative w-11/12 max-w-3xl">
        <button
          className="btn btn-ghost btn-sm btn-circle absolute right-2 top-2"
          aria-label="关闭收藏本"
          type="button"
          onClick={onClose}
        >
          <X aria-hidden="true" className="size-4" />
        </button>
        {content}
      </div>
      <button
        className="modal-backdrop"
        aria-label="关闭收藏本弹窗"
        type="button"
        onClick={onClose}
      />
    </div>
  );
}
