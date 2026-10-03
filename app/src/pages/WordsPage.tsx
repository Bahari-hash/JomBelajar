import { Link } from "react-router-dom";
import {
  useGetLearningOverviewQuery,
  useGetReviewOverviewQuery,
} from "@/features/wordStudy/wordStudyApi";
import WordStudyCheckInCalendar from "@/features/wordStudy/WordStudyCheckInCalendar";
import WordStudyTodayReview from "@/features/wordStudy/WordStudyTodayReview";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordsPage() {
  useDocumentTitle("单词学习");
  const learning = useGetLearningOverviewQuery(undefined, { refetchOnMountOrArgChange: true });
  const review = useGetReviewOverviewQuery(undefined, { refetchOnMountOrArgChange: true });
  const active = learning.data?.activeSession || review.data?.activeSession;
  const available = active || learning.data?.hasMoreWords || (review.data?.dueCount ?? 0) > 0;
  const loading = learning.isLoading || review.isLoading;
  const error = learning.isError || review.isError;
  return (
    <main className="mx-auto max-w-6xl space-y-8">
      <header>
        <h1 className="text-3xl font-bold">单词学习</h1>
        <p className="mt-2 text-base-content/65">先完成到期复习，再学习新词；未完成的一组会继续保留。</p>
      </header>
      <section className="space-y-6 border-y border-base-300 py-8" aria-label="学习安排">
        {loading ? <div className="skeleton h-20 w-full" /> : error ? (
          <div role="alert">
            <p>学习安排加载失败，请重试。</p>
            <button className="btn btn-ghost mt-3" onClick={() => { void learning.refetch(); void review.refetch(); }}>重新加载</button>
          </div>
        ) : <>
          <div className="grid gap-4 text-lg sm:grid-cols-3">
            <p>待复习 {review.data?.dueCount ?? 0} 个</p>
            <p>今日已学习 {learning.data?.todayLearnedCount ?? 0} 个</p>
            <p>累计学习 {learning.data?.totalLearnedCount ?? 0} 个</p>
          </div>
          {available ? <Link className="btn btn-primary" to="/words/study">{active ? "继续学习" : "开始学习"}</Link>
            : <p className="text-base-content/60">当前学习已完成，没有到期复习或待学新词。</p>}
        </>}
      </section>
      <div className="grid gap-10 border-b border-base-300 pb-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.25fr)]">
        <WordStudyCheckInCalendar />
        <WordStudyTodayReview />
      </div>
    </main>
  );
}
