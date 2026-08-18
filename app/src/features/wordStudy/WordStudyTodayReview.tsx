import { ChevronLeft, ChevronRight, Heart, RefreshCw } from "lucide-react";
import { useState } from "react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import {
  useGetTodayReviewQuery,
  useSetFavoriteMutation,
} from "@/features/wordStudy/wordStudyApi";
import type { WordStudyTodayReviewItem } from "@/features/wordStudy/wordStudyTypes";

function formatActivity(activity: WordStudyTodayReviewItem["activityType"]) {
  return activity === "Learning" ? "今日新学" : "今日复习";
}

export default function WordStudyTodayReview() {
  const [page, setPage] = useState(1);
  const review = useGetTodayReviewQuery({ page, pageSize: 20 });
  const [setFavorite] = useSetFavoriteMutation();
  const [favoriteError, setFavoriteError] = useState<string | null>(null);

  const toggleFavorite = async (item: WordStudyTodayReviewItem) => {
    setFavoriteError(null);
    try {
      await setFavorite({
        wordId: item.wordId,
        favorite: !item.isFavorite,
      }).unwrap();
    } catch {
      setFavoriteError(`“${item.headword}”收藏状态更新失败，请重试。`);
    }
  };

  return (
    <section aria-labelledby="today-review-heading" className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold" id="today-review-heading">
            今日回顾
          </h2>
          <p className="mt-1 text-sm text-base-content/65">
            回顾今天新学和复习过的单词。
          </p>
        </div>
        {review.data && review.data.totalCount > 0 ? (
          <span className="text-sm text-base-content/60">
            共 {review.data.totalCount} 条
          </span>
        ) : null}
      </div>

      {review.isLoading ? (
        <div className="space-y-3" role="status" aria-label="今日回顾加载中">
          <div className="skeleton h-36 w-full" />
          <div className="skeleton h-36 w-full" />
        </div>
      ) : null}

      {review.isError ? (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm text-error" role="alert">
            今日回顾加载失败，请重试。
          </p>
          <button
            className="btn btn-ghost btn-sm"
            type="button"
            onClick={() => void review.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重试
          </button>
        </div>
      ) : null}

      {!review.isLoading &&
      !review.isError &&
      (review.data?.items ?? []).length === 0 ? (
        <p className="border-y border-base-300 py-10 text-center text-base-content/60">
          今天还没有学习记录。
        </p>
      ) : null}

      {!review.isLoading &&
      !review.isError &&
      (review.data?.items ?? []).length ? (
        <>
          <div className="space-y-4">
            {(review.data?.items ?? []).map((item) => (
              <article
                className="space-y-4 border-b border-base-300 pb-5 last:border-b-0"
                key={`${item.wordId}-${item.activityType}-${item.completedAtUtc}`}
              >
                <header className="flex flex-wrap items-start justify-between gap-3">
                  <div className="flex min-w-0 flex-wrap items-center gap-3">
                    <h3 className="wrap-break-word text-xl font-semibold">
                      {item.headword}
                    </h3>
                    <span className="badge badge-outline">
                      {formatActivity(item.activityType)}
                    </span>
                    {item.audioResourceId ? (
                      <AudioPlaybackButton
                        audioResourceId={item.audioResourceId}
                        label={`${item.headword}读音`}
                        variant="icon"
                      />
                    ) : null}
                  </div>
                  <button
                    aria-label={
                      item.isFavorite
                        ? `取消收藏${item.headword}`
                        : `收藏${item.headword}`
                    }
                    className="btn btn-ghost btn-sm btn-square"
                    type="button"
                    onClick={() => void toggleFavorite(item)}
                  >
                    <Heart
                      aria-hidden="true"
                      className="size-5"
                      fill={item.isFavorite ? "currentColor" : "none"}
                    />
                  </button>
                </header>
                <div className="space-y-4">
                  {item.senses.map((sense) => (
                    <div
                      className="space-y-2"
                      key={`${sense.sortOrder}-${sense.definition}`}
                    >
                      <p>
                        <span className="mr-2 text-sm text-base-content/60">
                          {sense.partOfSpeech}
                        </span>
                        {sense.definition}
                      </p>
                      {sense.examples.map((example) => (
                        <div
                          className="pl-4 text-sm text-base-content/75"
                          key={`${example.sortOrder}-${example.sentence}`}
                        >
                          <div className="flex flex-wrap items-center gap-2">
                            <span>{example.sentence}</span>
                            {example.audioResourceId ? (
                              <AudioPlaybackButton
                                audioResourceId={example.audioResourceId}
                                label="例句"
                                variant="icon"
                              />
                            ) : null}
                          </div>
                          <p className="mt-1 text-base-content/55">
                            {example.translation}
                          </p>
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              </article>
            ))}
          </div>
          {favoriteError ? (
            <p className="text-sm text-error" role="status">
              {favoriteError}
            </p>
          ) : null}
          {review.data?.totalPages && review.data.totalPages > 1 ? (
            <nav
              aria-label="今日回顾分页"
              className="flex items-center justify-between gap-3"
            >
              <button
                className="btn btn-ghost btn-sm"
                disabled={page <= 1}
                type="button"
                onClick={() => setPage((value) => Math.max(1, value - 1))}
              >
                <ChevronLeft aria-hidden="true" className="size-4" />
                上一页
              </button>
              <span className="text-sm text-base-content/60">
                {review.data.page} / {review.data.totalPages}
              </span>
              <button
                className="btn btn-ghost btn-sm"
                disabled={page >= (review.data?.totalPages ?? 0)}
                type="button"
                onClick={() => setPage((value) => value + 1)}
              >
                下一页
                <ChevronRight aria-hidden="true" className="size-4" />
              </button>
            </nav>
          ) : null}
        </>
      ) : null}
    </section>
  );
}
