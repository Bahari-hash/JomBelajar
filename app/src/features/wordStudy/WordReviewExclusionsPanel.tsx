import {
  useGetReviewExclusionsQuery,
  useRestoreReviewMutation,
} from "./wordStudyApi";
export default function WordReviewExclusionsPanel() {
  const query = useGetReviewExclusionsQuery({ page: 1, pageSize: 20 });
  const [restore] = useRestoreReviewMutation();
  return (
    <section className="border-t border-base-300 py-8">
      <h2 className="text-xl font-semibold">已停止复习</h2>
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
            <button
              className="btn btn-outline btn-sm"
              onClick={() => {
                if (window.confirm("恢复后将在 1 天后到期，确认恢复吗？"))
                  void restore(word.wordId);
              }}
            >
              恢复复习
            </button>
          </div>
        ))}
      </div>
      {query.data?.totalCount === 0 ? (
        <p className="mt-4 text-base-content/60">没有停止复习的单词</p>
      ) : null}
    </section>
  );
}
