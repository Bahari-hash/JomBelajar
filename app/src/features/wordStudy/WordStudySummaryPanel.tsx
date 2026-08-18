import { useGetLearningOverviewQuery } from "./wordStudyApi";
export default function WordStudySummaryPanel() {
  const { data, isLoading } = useGetLearningOverviewQuery();
  return (
    <section className="border-t border-base-300 py-8">
      <h2 className="text-xl font-semibold">学习统计</h2>
      {isLoading ? (
        <div className="skeleton mt-4 h-16" />
      ) : (
        <div className="mt-5 flex gap-10">
          <div>
            <strong className="text-2xl">{data?.todayLearnedCount ?? 0}</strong>
            <p className="text-sm text-base-content/60">今日学习</p>
          </div>
          <div>
            <strong className="text-2xl">{data?.totalLearnedCount ?? 0}</strong>
            <p className="text-sm text-base-content/60">累计学习</p>
          </div>
        </div>
      )}
    </section>
  );
}
