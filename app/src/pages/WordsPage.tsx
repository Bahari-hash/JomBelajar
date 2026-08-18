import { BookOpenCheck, Brain, RefreshCw } from "lucide-react";
import type { ReactNode } from "react";
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
  const learning = useGetLearningOverviewQuery();
  const review = useGetReviewOverviewQuery();

  return (
    <main className="mx-auto max-w-6xl space-y-8">
      <header>
        <h1 className="text-3xl font-bold">单词学习</h1>
        <p className="mt-2 text-base-content/65">选择当前要完成的学习任务。</p>
      </header>
      <div className="grid gap-8 border-y border-base-300 py-8 lg:grid-cols-2">
        <TaskSection
          icon={<BookOpenCheck className="size-6" />}
          title="新词学习"
          loading={learning.isLoading}
          error={learning.isError}
          metrics={
            learning.data
              ? [
                  `今日已学习 ${learning.data.todayLearnedCount} 个`,
                  `累计学习 ${learning.data.totalLearnedCount} 个`,
                ]
              : []
          }
          empty={
            learning.data?.hasMoreWords === false &&
            !learning.data.activeSession
          }
          emptyText="词库中的单词已经全部学习完成"
          href="/words/learning"
          action={
            learning.data?.activeSession ? "继续新词学习" : "开始新词学习"
          }
          retry={() => void learning.refetch()}
        />
        <TaskSection
          icon={<Brain className="size-6" />}
          title="旧词复习"
          loading={review.isLoading}
          error={review.isError}
          metrics={
            review.data
              ? [
                  `待复习 ${review.data.dueCount} 个`,
                  `其中逾期 ${review.data.overdueCount} 个`,
                ]
              : []
          }
          empty={review.data?.dueCount === 0 && !review.data.activeSession}
          emptyText="当前没有到期单词"
          href="/words/review"
          action={review.data?.activeSession ? "继续旧词复习" : "开始旧词复习"}
          retry={() => void review.refetch()}
        />
      </div>
      <div className="grid gap-10 border-b border-base-300 pb-8 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.25fr)]">
        <WordStudyCheckInCalendar />
        <WordStudyTodayReview />
      </div>
    </main>
  );
}

interface TaskSectionProps {
  icon: ReactNode;
  title: string;
  loading: boolean;
  error: boolean;
  metrics: string[];
  empty: boolean;
  emptyText: string;
  href: string;
  action: string;
  retry: () => void;
}

function TaskSection(props: TaskSectionProps) {
  return (
    <section className="min-h-64 space-y-6 lg:px-6">
      <div className="flex items-center gap-3 text-primary">
        {props.icon}
        <h2 className="text-xl font-semibold text-base-content">
          {props.title}
        </h2>
      </div>
      {props.loading ? <div className="skeleton h-20 w-full" /> : null}
      {props.error ? (
        <button className="btn btn-ghost" onClick={props.retry} type="button">
          <RefreshCw className="size-4" />
          重新加载
        </button>
      ) : null}
      {!props.loading && !props.error ? (
        <>
          <div className="space-y-2 text-lg">
            {props.metrics.map((metric) => (
              <p key={metric}>{metric}</p>
            ))}
          </div>
          {props.empty ? (
            <p className="text-base-content/60">{props.emptyText}</p>
          ) : (
            <Link className="btn btn-primary" to={props.href}>
              {props.action}
            </Link>
          )}
        </>
      ) : null}
    </section>
  );
}
