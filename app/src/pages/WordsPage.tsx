import { BookOpenCheck, CalendarDays, RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import WordStudyCard from "@/features/wordStudy/WordStudyCard";
import {
  useGetTodayQuery,
  useLazyGetNextItemQuery,
  useStartTodayMutation,
  useSubmitResultMutation,
} from "@/features/wordStudy/wordStudyApi";
import { getWordStudyErrorMessage } from "@/features/wordStudy/wordStudyErrors";
import type {
  WordStudyNextItem,
  WordStudyResult,
  WordStudySession,
} from "@/features/wordStudy/wordStudyTypes";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordsPage() {
  useDocumentTitle("单词背诵");
  const todayQuery = useGetTodayQuery();
  const [startToday, startState] = useStartTodayMutation();
  const [loadNext, nextState] = useLazyGetNextItemQuery();
  const [submitResult, submitState] = useSubmitResultMutation();
  const [session, setSession] = useState<WordStudySession | null>(null);
  const [item, setItem] = useState<WordStudyNextItem | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const refetchToday = todayQuery.refetch;

  useEffect(() => {
    if (todayQuery.data?.session) {
      setSession(todayQuery.data.session);
    }
  }, [todayQuery.data]);

  useEffect(() => {
    if (session?.status !== "Active" || item) {
      return;
    }
    void loadNext(session.id, false)
      .unwrap()
      .then((nextItem) => {
        if (nextItem) {
          setItem(nextItem);
        } else {
          void refetchToday();
        }
      })
      .catch(() => undefined);
  }, [item, loadNext, refetchToday, session]);

  const handleStart = async () => {
    setMessage(null);
    try {
      const started = await startToday().unwrap();
      setSession(started);
      setItem(null);
    } catch (error) {
      setMessage(
        getWordStudyErrorMessage(error, "今天的单词暂时无法开始，请重试。"),
      );
    }
  };

  const handleResult = async (result: WordStudyResult) => {
    if (!session || !item || submitState.isLoading) {
      return;
    }
    setMessage(null);
    try {
      const updated = await submitResult({
        sessionId: session.id,
        itemId: item.itemId,
        result,
      }).unwrap();
      setSession(updated);
      setItem(null);
      if (updated.status === "Completed") {
        await todayQuery.refetch();
      }
    } catch (error) {
      setMessage(
        getWordStudyErrorMessage(error, "背诵结果保存失败，请重试。"),
      );
    }
  };

  if (todayQuery.isLoading) {
    return (
      <div aria-label="今日单词加载中" className="mx-auto max-w-3xl space-y-5" role="status">
        <div className="skeleton h-9 w-48" />
        <div className="skeleton h-[32rem] w-full" />
      </div>
    );
  }

  if (todayQuery.isError && !todayQuery.data) {
    return (
      <WordStudyStatus
        title="今日单词暂时无法加载"
        description={getWordStudyErrorMessage(todayQuery.error, "请求失败，请重试。")}
        actionLabel="重新加载"
        onAction={() => void todayQuery.refetch()}
      />
    );
  }

  const today = todayQuery.data;
  if (!today) {
    return null;
  }

  const completedSession =
    session?.status === "Completed" ? session : today.state === "Completed" ? today.session : null;
  if (completedSession) {
    return (
      <WordStudyStatus
        title="今天的单词已完成"
        description={`共完成 ${completedSession.completedCount} 个单词，记住 ${completedSession.rememberedCount} 个，没记住 ${completedSession.forgottenCount} 个。`}
      />
    );
  }

  if (!session && today.state === "NotStarted") {
    return (
      <section className="mx-auto max-w-3xl py-8 sm:py-14">
        <div className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm sm:p-10">
          <CalendarDays aria-hidden="true" className="size-10 text-primary" />
          <h1 className="mt-5 text-3xl font-bold">今日单词背诵</h1>
          <p className="mt-3 leading-7 text-base-content/70">
            今天将从词库中选出 {today.dailyWordStudyCount} 个单词。
          </p>
          {message ? (
            <div className="alert alert-error mt-5 text-sm" role="alert">
              {message}
            </div>
          ) : null}
          <button
            className="btn btn-primary mt-7"
            disabled={startState.isLoading}
            type="button"
            onClick={() => void handleStart()}
          >
            {startState.isLoading ? (
              <span className="loading loading-spinner loading-sm" />
            ) : (
              <BookOpenCheck aria-hidden="true" className="size-5" />
            )}
            {startState.isLoading ? "正在准备" : "开始今天的背诵"}
          </button>
        </div>
      </section>
    );
  }

  if (nextState.isFetching && !item) {
    return (
      <div aria-label="单词卡片加载中" className="mx-auto max-w-3xl space-y-5" role="status">
        <div className="skeleton h-8 w-full" />
        <div className="skeleton h-[30rem] w-full" />
      </div>
    );
  }

  if (nextState.isError && !item) {
    return (
      <WordStudyStatus
        title="单词卡片加载失败"
        description={getWordStudyErrorMessage(nextState.error, "请求失败，请重试。")}
        actionLabel="重新加载"
        onAction={() => {
          if (session) {
            void loadNext(session.id, false)
              .unwrap()
              .then((nextItem) => setItem(nextItem ?? null));
          }
        }}
      />
    );
  }

  if (!item) {
    return (
      <WordStudyStatus
        title="正在整理今日进度"
        description="请稍候，系统正在确认今天的背诵结果。"
        actionLabel="刷新状态"
        onAction={() => void todayQuery.refetch()}
      />
    );
  }

  return (
    <div className="space-y-4">
      <header className="mx-auto flex max-w-3xl items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">单词背诵</h1>
          <p className="mt-1 text-sm text-base-content/65">按 UTC 日期记录今日学习进度</p>
        </div>
      </header>
      {message ? (
        <div className="alert alert-error mx-auto max-w-3xl text-sm" role="alert">
          {message}
        </div>
      ) : null}
      <WordStudyCard
        item={item}
        submitting={submitState.isLoading}
        onResult={(result) => void handleResult(result)}
      />
    </div>
  );
}

interface WordStudyStatusProps {
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
}

function WordStudyStatus({
  title,
  description,
  actionLabel,
  onAction,
}: WordStudyStatusProps) {
  return (
    <section className="mx-auto flex min-h-96 max-w-2xl flex-col items-center justify-center text-center">
      <BookOpenCheck aria-hidden="true" className="size-12 text-primary" />
      <h1 className="mt-5 text-3xl font-bold">{title}</h1>
      <p className="mt-3 max-w-lg leading-7 text-base-content/70">{description}</p>
      {actionLabel && onAction ? (
        <button className="btn btn-primary mt-7" type="button" onClick={onAction}>
          <RefreshCw aria-hidden="true" className="size-4" />
          {actionLabel}
        </button>
      ) : null}
    </section>
  );
}
