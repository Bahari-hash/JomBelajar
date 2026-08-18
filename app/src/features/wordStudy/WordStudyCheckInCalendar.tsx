import { ChevronLeft, ChevronRight, RefreshCw } from "lucide-react";
import { useMemo, useState } from "react";
import { useGetCheckInCalendarQuery } from "@/features/wordStudy/wordStudyApi";

const weekLabels = ["一", "二", "三", "四", "五", "六", "日"];

function daysInMonth(year: number, month: number) {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

function firstWeekday(year: number, month: number) {
  const weekday = new Date(Date.UTC(year, month - 1, 1)).getUTCDay();
  return weekday === 0 ? 6 : weekday - 1;
}

export default function WordStudyCheckInCalendar() {
  const now = new Date();
  const [view, setView] = useState({
    year: now.getUTCFullYear(),
    month: now.getUTCMonth() + 1,
  });
  const calendar = useGetCheckInCalendarQuery(view);
  const checkedDates = useMemo(
    () =>
      new Set(
        (calendar.data?.checkedInDates ?? []).map((value) =>
          value.studyDateUtc.slice(0, 10),
        ),
      ),
    [calendar.data],
  );
  const cells = useMemo(() => {
    const total = daysInMonth(view.year, view.month);
    const padding = firstWeekday(view.year, view.month);
    return Array.from({ length: padding + total }, (_, index) =>
      index < padding ? null : index - padding + 1,
    );
  }, [view]);

  const moveMonth = (delta: number) => {
    const next = new Date(Date.UTC(view.year, view.month - 1 + delta, 1));
    setView({ year: next.getUTCFullYear(), month: next.getUTCMonth() + 1 });
  };

  return (
    <section aria-labelledby="check-in-heading" className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold" id="check-in-heading">
            学习打卡
          </h2>
          <p className="mt-1 text-sm text-base-content/65">
            完成当天新词学习即可打卡。
          </p>
        </div>
        <div className="join" aria-label="切换月份">
          <button
            aria-label="上个月"
            className="btn btn-ghost btn-sm join-item"
            type="button"
            onClick={() => moveMonth(-1)}
          >
            <ChevronLeft aria-hidden="true" className="size-4" />
          </button>
          <span className="join-item flex min-w-28 items-center justify-center px-3 text-sm font-medium">
            {view.year} 年 {view.month} 月
          </span>
          <button
            aria-label="下个月"
            className="btn btn-ghost btn-sm join-item"
            type="button"
            onClick={() => moveMonth(1)}
          >
            <ChevronRight aria-hidden="true" className="size-4" />
          </button>
        </div>
      </div>

      {calendar.isLoading ? (
        <div
          className="skeleton h-64 w-full"
          role="status"
          aria-label="打卡日历加载中"
        />
      ) : null}
      {calendar.isError ? (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm text-error" role="alert">
            打卡日历加载失败，请重试。
          </p>
          <button
            className="btn btn-ghost btn-sm"
            type="button"
            onClick={() => void calendar.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重试
          </button>
        </div>
      ) : null}

      {!calendar.isLoading && !calendar.isError && calendar.data ? (
        <>
          <div className="grid grid-cols-7 gap-1 text-center text-sm">
            {weekLabels.map((label) => (
              <div
                className="py-2 font-medium text-base-content/60"
                key={label}
              >
                {label}
              </div>
            ))}
            {cells.map((day, index) => {
              const dateKey = day
                ? `${view.year.toString().padStart(4, "0")}-${view.month.toString().padStart(2, "0")}-${day.toString().padStart(2, "0")}`
                : null;
              return (
                <div
                  className={`flex aspect-square items-center justify-center rounded-md text-sm ${dateKey && checkedDates.has(dateKey) ? "bg-primary text-primary-content" : "bg-base-200/60"}`}
                  key={dateKey ?? `padding-${index}`}
                >
                  {day}
                </div>
              );
            })}
          </div>
          <div className="grid gap-3 border-t border-base-300 pt-4 text-sm sm:grid-cols-3">
            <p>当前连续 {calendar.data.currentStreak} 天</p>
            <p>最长连续 {calendar.data.longestStreak} 天</p>
            <p>累计打卡 {calendar.data.totalCheckInDays} 天</p>
          </div>
        </>
      ) : null}
    </section>
  );
}
