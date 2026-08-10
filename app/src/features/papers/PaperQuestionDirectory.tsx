import { Check, Circle, CircleX, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type {
  LocalPaperAnswer,
  PaperAttemptQuestion,
} from "@/features/papers/paperTypes";
import { getQuestionTypeLabel } from "@/features/papers/paperUtils";

interface PaperQuestionDirectoryProps {
  questions: PaperAttemptQuestion[];
  answers: Record<string, LocalPaperAnswer>;
  currentQuestionId: string;
  disabled: boolean;
  onSelect: (questionId: string) => void;
}

function status(answer: LocalPaperAnswer | undefined) {
  if (
    !answer ||
    answer.value === null ||
    (typeof answer.value === "string" && answer.value.trim() === "")
  )
    return "未作答";
  if (answer.status === "saving") return "保存中";
  if (answer.status === "error") return "保存失败";
  return answer.status === "saved" ? "已保存" : "未保存";
}

function List({
  questions,
  answers,
  currentQuestionId,
  disabled,
  onSelect,
  close,
}: PaperQuestionDirectoryProps & { close?: () => void }) {
  return (
    <ol className="max-h-[32rem] space-y-1 overflow-y-auto pr-1">
      {questions.map((question, index) => {
        const selected = question.id === currentQuestionId;
        const answer = answers[question.id];
        return (
          <li key={question.id}>
            <button
              aria-current={selected ? "step" : undefined}
              aria-label={`第 ${index + 1} 题，${getQuestionTypeLabel(question.type)}`}
              className={`flex w-full min-w-0 items-start gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors ${selected ? "bg-primary/10 text-primary" : "hover:bg-base-200"}`}
              disabled={disabled}
              type="button"
              onClick={() => {
                onSelect(question.id);
                close?.();
              }}
            >
              <StatusIcon answer={answer} />
              <span className="min-w-0 flex-1">
                <span className="block break-words font-medium">
                  第 {index + 1} 题 · {getQuestionTypeLabel(question.type)}
                </span>
                <span className="mt-0.5 block text-xs text-base-content/60">
                  {status(answer)}
                </span>
              </span>
            </button>
          </li>
        );
      })}
    </ol>
  );
}

function StatusIcon({ answer }: { answer: LocalPaperAnswer | undefined }) {
  if (answer?.status === "error")
    return (
      <CircleX
        aria-hidden="true"
        className="mt-0.5 size-4 shrink-0 text-error"
      />
    );
  if (
    answer?.value !== null &&
    answer?.value !== undefined &&
    !(typeof answer.value === "string" && answer.value.trim() === "")
  )
    return (
      <Check
        aria-hidden="true"
        className="mt-0.5 size-4 shrink-0 text-success"
      />
    );
  return <Circle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />;
}

export default function PaperQuestionDirectory(
  props: PaperQuestionDirectoryProps,
) {
  const [open, setOpen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    closeRef.current?.focus();
    document.body.style.overflow = "hidden";
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
      if (event.key === "Tab") {
        const elements = Array.from(
          panelRef.current?.querySelectorAll<HTMLElement>(
            'button:not([disabled]), [tabindex]:not([tabindex="-1"])',
          ) ?? [],
        );
        const first = elements[0];
        const last = elements.at(-1);
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first?.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = "";
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);
  const close = () => {
    setOpen(false);
    triggerRef.current?.focus();
  };
  return (
    <>
      <div className="mb-4 flex items-center justify-between gap-3 lg:hidden">
        <p className="text-sm font-semibold">题目目录</p>
        <button
          ref={triggerRef}
          aria-label="打开题目目录"
          className="btn btn-outline btn-sm"
          type="button"
          disabled={props.disabled}
          onClick={() => setOpen(true)}
        >
          题目目录
        </button>
      </div>
      <aside
        aria-label="题目目录"
        className="hidden rounded-lg border border-base-300 bg-base-100 p-3 shadow-sm lg:block lg:sticky lg:top-5 lg:self-start"
      >
        <h2 className="px-2 pb-2 text-sm font-semibold">题目目录</h2>
        <List {...props} />
      </aside>
      {open ? (
        <div
          className="fixed inset-0 z-40 lg:hidden"
          role="dialog"
          aria-modal="true"
          aria-label="题目目录"
        >
          <button
            aria-label="关闭题目目录"
            className="absolute inset-0 bg-neutral/45"
            type="button"
            onClick={close}
          />
          <div
            ref={panelRef}
            className="absolute inset-y-0 right-0 flex w-[min(20rem,88vw)] flex-col border-l border-base-300 bg-base-100 p-4 shadow-xl"
          >
            <div className="flex h-12 items-center justify-between">
              <span className="font-semibold">题目目录</span>
              <button
                ref={closeRef}
                aria-label="关闭题目目录"
                className="btn btn-square btn-ghost btn-sm"
                type="button"
                onClick={close}
              >
                <X aria-hidden="true" className="size-5" />
              </button>
            </div>
            <div className="mt-4">
              <List {...props} close={close} />
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
