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

type RemovalSource = "detail" | "row";

type RemovalError = {
  source: RemovalSource;
  wordId: string;
};

const removalErrorMessage = "收藏状态更新失败，请重试。";

export default function WordFavoritesPanel({
  modal = false,
  onClose,
}: WordFavoritesPanelProps) {
  const [page, setPage] = useState(1);
  const [selectedWord, setSelectedWord] = useState<WordFavorite | null>(null);
  const query = useGetFavoritesQuery({ page, pageSize: 20 });
  const [setFavorite] = useSetFavoriteMutation();
  const [removalError, setRemovalError] = useState<RemovalError | null>(null);
  const [removingWordIds, setRemovingWordIds] = useState<string[]>([]);
  const openersRef = useRef(new Map<string, HTMLButtonElement>());
  const openerWordIdRef = useRef<string | null>(null);
  const pendingRemovalsRef = useRef(new Set<string>());
  const modalRef = useRef<HTMLDivElement | null>(null);
  const modalCloseRef = useRef<HTMLButtonElement | null>(null);

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

  useEffect(() => {
    if (!modal) return;
    const opener = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    modalCloseRef.current?.focus();
    return () => opener?.focus();
  }, [modal]);

  const closeDetails = () => {
    setRemovalError((current) =>
      current?.source === "detail" && current.wordId === selectedWord?.wordId
        ? null
        : current,
    );
    setSelectedWord(null);
  };

  const openDetails = (word: WordFavorite) => {
    openerWordIdRef.current = word.wordId;
    setRemovalError(null);
    setSelectedWord(word);
  };

  const remove = async (wordId: string, source: RemovalSource) => {
    if (pendingRemovalsRef.current.has(wordId)) return;
    pendingRemovalsRef.current.add(wordId);
    setRemovingWordIds((current) => [...current, wordId]);
    setRemovalError((current) =>
      current?.wordId === wordId ? null : current,
    );
    try {
      await setFavorite({ wordId, favorite: false }).unwrap();
      setSelectedWord((current) => {
        if (current?.wordId !== wordId) return current;
        openerWordIdRef.current = null;
        return null;
      });
    } catch {
      setRemovalError({ source, wordId });
    } finally {
      pendingRemovalsRef.current.delete(wordId);
      setRemovingWordIds((current) => current.filter((id) => id !== wordId));
    }
  };

  const isRemoving = (wordId: string) => removingWordIds.includes(wordId);
  const visibleRemovalError = selectedWord
    ? removalError?.source === "detail" && removalError.wordId === selectedWord.wordId
      ? removalErrorMessage
      : null
    : removalError?.source === "row"
      ? removalErrorMessage
      : null;

  const handleModalKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      if (selectedWord) closeDetails();
      else onClose?.();
      return;
    }
    if (event.key !== "Tab") return;
    const focusableElements = Array.from(
      modalRef.current?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      ) ?? [],
    );
    const first = focusableElements.at(0);
    const last = focusableElements.at(-1);
    if (!first || !last) return;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const content = (
    <section
      className={modal ? undefined : "border-t border-base-300 py-8"}
    >
      {query.isError || visibleRemovalError ? (
        <p className="alert alert-error mt-4 text-sm" role="alert">
          {visibleRemovalError ?? "收藏本暂时无法加载。"}
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
          onRemove={() => void remove(selectedWord.wordId, "detail")}
          removing={isRemoving(selectedWord.wordId)}
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
                    disabled={isRemoving(word.wordId)}
                    onClick={() => void remove(word.wordId, "row")}
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
      ref={modalRef}
      className="modal modal-open"
      role="dialog"
      aria-modal="true"
      aria-labelledby={
        selectedWord ? "favorite-word-details-title" : "favorites-panel-title"
      }
      onKeyDown={handleModalKeyDown}
    >
      <div className="modal-box relative w-11/12 max-w-3xl">
        <button
          ref={modalCloseRef}
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
