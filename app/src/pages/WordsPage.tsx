import { skipToken } from "@reduxjs/toolkit/query";
import { BookOpenCheck, CalendarDays, RefreshCw } from "lucide-react";
import { useEffect, useState } from "react";
import WordStudyCard from "@/features/wordStudy/WordStudyCard";
import WordStudyDirectory from "@/features/wordStudy/WordStudyDirectory";
import {
  useGetSessionItemsQuery,
  useGetTodayQuery,
  useLazyGetNextItemQuery,
  useStartTodayMutation,
  useSubmitResultMutation,
} from "@/features/wordStudy/wordStudyApi";
import { getWordStudyErrorMessage } from "@/features/wordStudy/wordStudyErrors";
import type {
  WordStudyResult,
  WordStudySession,
  WordStudySessionItem,
} from "@/features/wordStudy/wordStudyTypes";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordsPage() {
  useDocumentTitle("单词背诵");
  const todayQuery = useGetTodayQuery();
  const [startToday, startState] = useStartTodayMutation();
  const [loadNext] = useLazyGetNextItemQuery();
  const [submitResult, submitState] = useSubmitResultMutation();
  const [session, setSession] = useState<WordStudySession | null>(null);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [reconciledSessionId, setReconciledSessionId] = useState<string | null>(
    null,
  );
  const [message, setMessage] = useState<string | null>(null);

  const today = todayQuery.data;
  const currentSession = session ?? today?.session ?? null;
  const itemsQuery = useGetSessionItemsQuery(currentSession?.id ?? skipToken);
  const items = itemsQuery.data ?? [];
  const refetchToday = todayQuery.refetch;
  const refetchItems = itemsQuery.refetch;

  useEffect(() => {
    setSession((current) => {
      if (!today?.session) {
        return null;
      }
      return current?.id === today.session.id ? current : today.session;
    });
  }, [today?.session]);

  useEffect(() => {
    const loadedItems = itemsQuery.data;
    if (!loadedItems) {
      return;
    }
    setSelectedItemId((current) =>
      current && loadedItems.some((item) => item.itemId === current)
        ? current
        : findDefaultItemId(loadedItems),
    );
  }, [itemsQuery.data]);

  useEffect(() => {
    if (
      !currentSession ||
      currentSession.status !== "Active" ||
      !itemsQuery.data ||
      itemsQuery.isFetching ||
      reconciledSessionId === currentSession.id
    ) {
      return;
    }
    const hasAvailablePending = itemsQuery.data.some(
      (item) => item.status === "Pending" && item.contentAvailable,
    );
    if (hasAvailablePending) {
      return;
    }

    setReconciledSessionId(currentSession.id);
    void loadNext(currentSession.id, false)
      .unwrap()
      .then(async () => {
        await Promise.all([refetchToday(), refetchItems()]);
      })
      .catch(() => undefined);
  }, [
    currentSession,
    itemsQuery.data,
    itemsQuery.isFetching,
    loadNext,
    refetchItems,
    refetchToday,
    reconciledSessionId,
  ]);

  const handleStart = async () => {
    setMessage(null);
    try {
      const started = await startToday().unwrap();
      setSession(started);
      setSelectedItemId(null);
      setReconciledSessionId(null);
    } catch (error) {
      setMessage(
        getWordStudyErrorMessage(error, "今天的单词暂时无法开始，请重试。"),
      );
    }
  };

  const currentItem =
    items.find((item) => item.itemId === selectedItemId) ?? null;
  const currentIndex = currentItem
    ? items.findIndex((item) => item.itemId === currentItem.itemId)
    : -1;
  const hasPrevious = currentIndex > 0;
  const hasNext = currentIndex >= 0 && currentIndex < items.length - 1;

  const handleResult = async (result: WordStudyResult) => {
    if (!currentSession || !currentItem || submitState.isLoading) {
      return;
    }
    setMessage(null);
    try {
      const updated = await submitResult({
        sessionId: currentSession.id,
        itemId: currentItem.itemId,
        result,
      }).unwrap();
      setSession(updated);
      setSelectedItemId(
        findNextPendingItemId(items, currentItem.itemId) ?? currentItem.itemId,
      );
      if (updated.status === "Completed") {
        await todayQuery.refetch();
      }
    } catch (error) {
      setMessage(getWordStudyErrorMessage(error, "背诵结果保存失败，请重试。"));
    }
  };

  const handlePrevious = () => {
    if (hasPrevious) {
      setSelectedItemId(items[currentIndex - 1]?.itemId ?? null);
    }
  };

  const handleNext = () => {
    if (hasNext) {
      setSelectedItemId(items[currentIndex + 1]?.itemId ?? null);
    }
  };

  if (todayQuery.isLoading) {
    return (
      <div
        aria-label="今日单词加载中"
        className="mx-auto max-w-3xl space-y-5"
        role="status"
      >
        <div className="skeleton h-9 w-48" />
        <div className="skeleton h-[32rem] w-full" />
      </div>
    );
  }

  if (todayQuery.isError && !today) {
    return (
      <WordStudyStatus
        title="今日单词暂时无法加载"
        description={getWordStudyErrorMessage(
          todayQuery.error,
          "请求失败，请重试。",
        )}
        actionLabel="重新加载"
        onAction={() => void todayQuery.refetch()}
      />
    );
  }

  if (!today) {
    return null;
  }

  if (!currentSession && today.state === "NotStarted") {
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

  if (!currentSession) {
    return (
      <WordStudyStatus
        title="今日会话暂时无法加载"
        description="今日状态缺少会话信息，请刷新后重试。"
        actionLabel="刷新状态"
        onAction={() => void todayQuery.refetch()}
      />
    );
  }

  if (itemsQuery.isLoading && !itemsQuery.data) {
    return (
      <div
        aria-label="今日词单加载中"
        className="mx-auto max-w-3xl space-y-5"
        role="status"
      >
        <div className="skeleton h-8 w-full" />
        <div className="skeleton h-[30rem] w-full" />
      </div>
    );
  }

  if (itemsQuery.isError && !itemsQuery.data) {
    return (
      <WordStudyStatus
        title="今日词单加载失败"
        description={getWordStudyErrorMessage(
          itemsQuery.error,
          "请求失败，请重试。",
        )}
        actionLabel="重新加载"
        onAction={() => void itemsQuery.refetch()}
      />
    );
  }

  if (items.length === 0) {
    return (
      <WordStudyStatus
        title="今日词单暂时为空"
        description="当前会话没有可查看的词条，请刷新后重试。"
        actionLabel="刷新词单"
        onAction={() => void itemsQuery.refetch()}
      />
    );
  }

  if (!currentItem) {
    return (
      <WordStudyStatus
        title="正在整理今日进度"
        description="请稍候，系统正在确认今天的背诵结果。"
        actionLabel="刷新状态"
        onAction={() =>
          void Promise.all([todayQuery.refetch(), itemsQuery.refetch()])
        }
      />
    );
  }

  const completed =
    currentSession.status === "Completed" || today.state === "Completed";
  return (
    <div className="space-y-4">
      <header className="mx-auto flex max-w-6xl items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">单词背诵</h1>
          <p className="mt-1 text-sm text-base-content/65">
            按 UTC 日期记录今日学习进度
          </p>
        </div>
      </header>
      {completed ? (
        <div
          className="alert alert-success mx-auto max-w-6xl text-sm"
          role="status"
        >
          今天的单词已完成，共完成 {currentSession.completedCount} 个，记住{" "}
          {currentSession.rememberedCount} 个，没记住{" "}
          {currentSession.forgottenCount} 个。
        </div>
      ) : null}
      {message ? (
        <div
          className="alert alert-error mx-auto max-w-6xl text-sm"
          role="alert"
        >
          {message}
        </div>
      ) : null}
      {itemsQuery.isError && itemsQuery.data ? (
        <div
          className="alert alert-warning mx-auto flex max-w-6xl flex-wrap justify-between gap-3 text-sm"
          role="alert"
        >
          <span>
            词单刷新失败：
            {getWordStudyErrorMessage(itemsQuery.error, "请稍后重试。")}
          </span>
          <button
            className="btn btn-sm"
            type="button"
            onClick={() => void itemsQuery.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重试词单
          </button>
        </div>
      ) : null}
      <div className="mx-auto grid max-w-6xl gap-5 lg:grid-cols-[16rem_minmax(0,1fr)]">
        <WordStudyDirectory
          items={items}
          selectedItemId={selectedItemId}
          disabled={submitState.isLoading}
          onSelect={setSelectedItemId}
        />
        <WordStudyCard
          item={currentItem}
          total={currentSession.actualCount}
          submitting={submitState.isLoading}
          hasPrevious={hasPrevious}
          hasNext={hasNext}
          onPrevious={handlePrevious}
          onNext={handleNext}
          onResult={(result) => void handleResult(result)}
        />
      </div>
    </div>
  );
}

function findDefaultItemId(items: WordStudySessionItem[]) {
  return (
    items.find((item) => item.status === "Pending" && item.contentAvailable)
      ?.itemId ??
    items.find((item) => item.contentAvailable)?.itemId ??
    items[0]?.itemId ??
    null
  );
}

function findNextPendingItemId(
  items: WordStudySessionItem[],
  currentItemId: string,
) {
  const currentIndex = items.findIndex((item) => item.itemId === currentItemId);
  const candidates = [
    ...items.slice(currentIndex + 1),
    ...items.slice(0, Math.max(0, currentIndex)),
  ];
  return (
    candidates.find(
      (item) => item.status === "Pending" && item.contentAvailable,
    )?.itemId ?? null
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
      <p className="mt-3 max-w-lg leading-7 text-base-content/70">
        {description}
      </p>
      {actionLabel && onAction ? (
        <button
          className="btn btn-primary mt-7"
          type="button"
          onClick={onAction}
        >
          <RefreshCw aria-hidden="true" className="size-4" />
          {actionLabel}
        </button>
      ) : null}
    </section>
  );
}
