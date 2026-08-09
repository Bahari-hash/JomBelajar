interface WordStudyProgressProps {
  current: number;
  total: number;
}

/** Displays stable, accessible progress for the current daily session. */
export default function WordStudyProgress({
  current,
  total,
}: WordStudyProgressProps) {
  const percentage = total > 0 ? Math.min(100, (current / total) * 100) : 0;
  return (
    <div className="space-y-2" aria-label={`今日进度 ${current}/${total}`}>
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium">今日进度</span>
        <span className="tabular-nums text-base-content/70">
          {current} / {total}
        </span>
      </div>
      <progress
        aria-label="今日单词背诵进度"
        className="progress progress-primary h-2 w-full"
        max={total}
        value={current}
      />
      <span className="sr-only">已完成 {Math.round(percentage)}%</span>
    </div>
  );
}
