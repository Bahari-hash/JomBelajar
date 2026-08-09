import {
  Check,
  Circle,
  CircleSlash2,
  RotateCcw,
} from "lucide-react";
import type {
  WordStudySessionItem,
  WordStudySessionItemStatus,
} from "@/features/wordStudy/wordStudyTypes";

interface WordStudyDirectoryProps {
  items: WordStudySessionItem[];
  selectedItemId: string | null;
  disabled?: boolean;
  onSelect: (itemId: string) => void;
}

const STATUS_LABELS = {
  Pending: "待背诵",
  Remembered: "已记住",
  Forgotten: "没记住",
  Skipped: "已跳过",
} satisfies Record<WordStudySessionItemStatus, string>;

/** Renders the ordered daily word directory for desktop and narrow screens. */
export default function WordStudyDirectory({
  items,
  selectedItemId,
  disabled = false,
  onSelect,
}: WordStudyDirectoryProps) {
  return (
    <div className="lg:sticky lg:top-5 lg:self-start">
      <details className="rounded-lg border border-base-300 bg-base-100 shadow-sm lg:hidden">
        <summary className="cursor-pointer px-4 py-3 font-semibold">
          今日词单
        </summary>
        <div className="border-t border-base-300 p-3">
          <DirectoryList
            items={items}
            selectedItemId={selectedItemId}
            disabled={disabled}
            onSelect={onSelect}
          />
        </div>
      </details>
      <aside
        aria-label="今日词单"
        className="hidden rounded-lg border border-base-300 bg-base-100 p-3 shadow-sm lg:block"
      >
        <h2 className="px-2 pb-2 text-sm font-semibold">今日词单</h2>
        <DirectoryList
          items={items}
          selectedItemId={selectedItemId}
          disabled={disabled}
          onSelect={onSelect}
        />
      </aside>
    </div>
  );
}

interface DirectoryListProps {
  items: WordStudySessionItem[];
  selectedItemId: string | null;
  disabled: boolean;
  onSelect: (itemId: string) => void;
}

function DirectoryList({
  items,
  selectedItemId,
  disabled,
  onSelect,
}: DirectoryListProps) {
  if (items.length === 0) {
    return <p className="px-2 py-3 text-sm text-base-content/65">暂无词单</p>;
  }

  return (
    <ol className="max-h-[32rem] space-y-1 overflow-y-auto pr-1">
      {items.map((item) => {
        const selected = item.itemId === selectedItemId;
        const available = item.contentAvailable && item.content;
        const statusLabel = available
          ? STATUS_LABELS[item.status]
          : "内容不可用";
        return (
          <li key={item.itemId}>
            <button
              aria-current={selected ? "true" : undefined}
              className={`flex w-full items-start gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors ${
                selected
                  ? "bg-primary/10 text-primary"
                  : "hover:bg-base-200"
              }`}
              disabled={disabled || !available}
              type="button"
              onClick={() => onSelect(item.itemId)}
            >
              <StatusIcon status={item.status} unavailable={!available} />
              <span className="min-w-0 flex-1">
                <span className="block wrap-break-word font-medium">
                  {item.position + 1}. {item.content?.headword ?? "单词内容不可用"}
                </span>
                <span className="mt-0.5 block text-xs text-base-content/60">
                  {statusLabel}
                </span>
              </span>
            </button>
          </li>
        );
      })}
    </ol>
  );
}

function StatusIcon({
  status,
  unavailable,
}: {
  status: WordStudySessionItemStatus;
  unavailable: boolean;
}) {
  if (unavailable) {
    return <CircleSlash2 aria-hidden="true" className="mt-0.5 size-4 shrink-0" />;
  }
  if (status === "Remembered") {
    return <Check aria-hidden="true" className="mt-0.5 size-4 shrink-0" />;
  }
  if (status === "Forgotten") {
    return <RotateCcw aria-hidden="true" className="mt-0.5 size-4 shrink-0" />;
  }
  return <Circle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />;
}
