import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import { useGetFavoritesQuery, useSetFavoriteMutation } from "./wordStudyApi";
export default function WordFavoritesPanel() {
  const query = useGetFavoritesQuery({ page: 1, pageSize: 20 });
  const [setFavorite] = useSetFavoriteMutation();
  return (
    <section className="border-t border-base-300 py-8">
      <h2 className="text-xl font-semibold">收藏本</h2>
      <div className="mt-5 divide-y divide-base-300">
        {query.data?.items.map((word) => (
          <div
            className="flex items-center justify-between gap-4 py-4"
            key={word.wordId}
          >
            <div>
              <p className="font-semibold">{word.headword}</p>
              <p className="text-sm text-base-content/60">
                {word.senses[0]?.definition}
              </p>
            </div>
            <div className="flex gap-2">
              {word.audioResourceId ? (
                <AudioPlaybackButton
                  audioResourceId={word.audioResourceId}
                  variant="icon"
                />
              ) : null}
              <button
                className="btn btn-ghost btn-sm"
                onClick={() =>
                  void setFavorite({ wordId: word.wordId, favorite: false })
                }
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
    </section>
  );
}
