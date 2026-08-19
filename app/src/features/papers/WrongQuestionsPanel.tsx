import { useState } from "react";
import { X } from "lucide-react";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import {
  useGetWrongQuestionQuery,
  useGetWrongQuestionsQuery,
  useRedoWrongQuestionMutation,
  type WrongQuestionItem,
  type WrongQuestionRedo,
} from "@/features/papers/wrongQuestionApi";

function RedoDialog({
  item,
  onClose,
}: {
  item: WrongQuestionItem;
  onClose: () => void;
}) {
  const detail = useGetWrongQuestionQuery(item.id);
  const [selected, setSelected] = useState<string | null>(null);
  const [booleanAnswer, setBooleanAnswer] = useState<boolean | null>(null);
  const [textAnswer, setTextAnswer] = useState("");
  const [textAnswers, setTextAnswers] = useState<string[]>([]);
  const [result, setResult] = useState<WrongQuestionRedo | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [redo, redoState] = useRedoWrongQuestionMutation();
  const submit = async () => {
    if (!detail.data) return;
    const answer =
      detail.data.type === "SingleChoice"
        ? { selectedOptionId: selected }
        : detail.data.type === "TrueFalse"
          ? { booleanAnswer }
          : detail.data.type === "Dictation"
            ? {
                textAnswers: detail.data.dictationBlanks.map(
                  (_blank, index) => textAnswers[index] ?? "",
                ),
              }
            : { textAnswer };
    setSubmitError(null);
    try {
      setResult(await redo({ id: item.id, answer }).unwrap());
    } catch {
      setSubmitError("提交失败，请检查答案后重试。");
    }
  };
  const canSubmit = detail.data
    ? detail.data.type === "SingleChoice"
      ? selected !== null
      : detail.data.type === "TrueFalse"
        ? booleanAnswer !== null
        : detail.data.type === "Dictation"
          ? textAnswers.some((value) => value?.trim())
          : textAnswer.trim().length > 0
    : false;
  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center p-4"
      role="dialog"
      aria-modal="true"
      aria-label="重做错题"
    >
      <button
        className="absolute inset-0 bg-neutral/45"
        aria-label="关闭重做"
        onClick={onClose}
      />
      <div className="relative max-h-[calc(100dvh-2rem)] w-full max-w-2xl overflow-y-auto rounded-lg bg-base-100 p-6 shadow-xl">
        <div className="flex justify-between gap-3">
          <h2 className="text-xl font-bold">重做错题</h2>
          <button
            className="btn btn-ghost btn-square btn-sm"
            aria-label="关闭重做"
            onClick={onClose}
          >
            <X className="size-4" />
          </button>
        </div>
        {detail.isLoading ? (
          <p className="mt-5">正在加载题目...</p>
        ) : detail.isError || !detail.data ? (
          <p className="mt-5 text-error">题目加载失败。</p>
        ) : (
          <div className="mt-5 space-y-4">
            <p className="text-lg font-semibold">{detail.data.prompt}</p>
            {detail.data.audioResourceId ? (
              <AudioPlaybackButton
                audioResourceId={detail.data.audioResourceId}
                label="题目音频"
              />
            ) : null}
            {detail.data.type === "SingleChoice" ? (
              <fieldset className="space-y-2">
                {detail.data.options.map((option) => (
                  <label
                    key={option.id}
                    className="flex gap-2 rounded-lg border border-base-300 p-3"
                  >
                    <input
                      type="radio"
                      className="radio radio-primary"
                      checked={selected === option.id}
                      onChange={() => setSelected(option.id)}
                    />
                    {option.text}
                  </label>
                ))}
              </fieldset>
            ) : detail.data.type === "TrueFalse" ? (
              <div className="flex gap-3">
                {[
                  [true, "正确"],
                  [false, "错误"],
                ].map(([value, label]) => (
                  <button
                    key={String(value)}
                    className={`btn ${booleanAnswer === value ? "btn-primary" : "btn-outline"}`}
                    onClick={() => setBooleanAnswer(value as boolean)}
                  >
                    {label as string}
                  </button>
                ))}
              </div>
            ) : detail.data.type === "Dictation" ? (
              <div className="space-y-3">
                {detail.data.dictationBlanks.map((blank, index) => (
                  <input
                    key={blank.sortOrder}
                    aria-label={`第 ${index + 1} 空`}
                    className="input input-bordered w-full"
                    value={textAnswers[index] ?? ""}
                    onChange={(event) => {
                      const next = [...textAnswers];
                      next[index] = event.target.value;
                      setTextAnswers(next);
                    }}
                  />
                ))}
              </div>
            ) : (
              <input
                aria-label="填空答案"
                className="input input-bordered w-full"
                value={textAnswer}
                onChange={(event) => setTextAnswer(event.target.value)}
              />
            )}
            {result ? (
              <div
                className={`rounded-lg p-4 ${result.isCorrect ? "bg-success/10 text-success" : "bg-error/10 text-error"}`}
                role="status"
              >
                {result.isCorrect
                  ? "回答正确，已掌握。"
                  : "回答错误，已恢复为待掌握。"}
                {result.explanation ? (
                  <p className="mt-2 text-base-content">
                    解析：{result.explanation}
                  </p>
                ) : null}
                {!result.isCorrect ? (
                  <p className="mt-2 text-base-content">
                    标准答案：
                    {result.type === "SingleChoice"
                      ? detail.data.options.find(
                          (option) => option.id === result.correctOptionId,
                        )?.text
                      : result.type === "TrueFalse"
                        ? result.correctBoolean
                          ? "正确"
                          : "错误"
                        : result.type === "Dictation"
                          ? result.dictationAnswers.join(" / ")
                          : result.acceptedAnswers.join("、")}
                  </p>
                ) : null}
              </div>
            ) : null}
            {submitError ? (
              <p className="text-sm text-error" role="alert">
                {submitError}
              </p>
            ) : null}
            <button
              className="btn btn-primary"
              disabled={redoState.isLoading || !canSubmit}
              onClick={() => void submit()}
            >
              {redoState.isLoading ? "正在判分" : "提交答案"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default function WrongQuestionsPanel({
  modal = false,
  onClose,
}: {
  modal?: boolean;
  onClose?: () => void;
}) {
  const [status, setStatus] = useState<"Pending" | "Mastered">("Pending");
  const [redoItem, setRedoItem] = useState<WrongQuestionItem | null>(null);
  const query = useGetWrongQuestionsQuery({ page: 1, pageSize: 50, status });
  const content = (
    <div
      className={
        modal ? "max-h-[calc(100dvh-2rem)] overflow-y-auto p-6" : "space-y-6"
      }
    >
      <header className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">错题本</h1>
          <p className="mt-2 text-sm text-base-content/65">
            答错和未作答题目会自动收录。
          </p>
        </div>
        {modal ? (
          <button
            className="btn btn-ghost btn-square btn-sm"
            aria-label="关闭错题本"
            onClick={onClose}
          >
            <X className="size-4" />
          </button>
        ) : null}
      </header>
      <div className="mt-5 flex gap-2">
        <button
          className={`btn btn-sm ${status === "Pending" ? "btn-primary" : "btn-ghost"}`}
          onClick={() => setStatus("Pending")}
        >
          待掌握
        </button>
        <button
          className={`btn btn-sm ${status === "Mastered" ? "btn-primary" : "btn-ghost"}`}
          onClick={() => setStatus("Mastered")}
        >
          已掌握
        </button>
      </div>
      {query.isLoading ? (
        <p className="mt-5" role="status">
          正在加载错题...
        </p>
      ) : query.isError ? (
        <p className="mt-5 text-error" role="alert">
          错题加载失败，请重试。
        </p>
      ) : query.data?.items.length ? (
        <ol className="mt-5 space-y-3">
          {query.data.items.map((item) => (
            <li key={item.id} className="rounded-lg border border-base-300 p-4">
              <div className="flex flex-wrap justify-between gap-2">
                <span className="text-sm text-base-content/60">
                  {item.paperTitle}
                </span>
                <span className="badge badge-outline">
                  错误 {item.wrongCount} 次
                </span>
              </div>
              <p className="mt-2 font-semibold">{item.prompt}</p>
              {item.status === "Pending" ? (
                <button
                  className="btn btn-outline btn-sm mt-3"
                  onClick={() => setRedoItem(item)}
                >
                  重做此题
                </button>
              ) : null}
            </li>
          ))}
        </ol>
      ) : (
        <p className="mt-8 text-center text-base-content/60">
          暂无{status === "Pending" ? "待掌握" : "已掌握"}错题。
        </p>
      )}
      {redoItem ? (
        <RedoDialog
          item={redoItem}
          onClose={() => {
            setRedoItem(null);
            void query.refetch();
          }}
        />
      ) : null}
    </div>
  );
  return modal ? (
    <div
      className="fixed inset-0 z-40 grid place-items-center p-4"
      role="dialog"
      aria-modal="true"
      aria-label="错题本"
    >
      <button
        className="absolute inset-0 bg-neutral/45"
        aria-label="关闭错题本"
        onClick={onClose}
      />
      <div className="relative w-full max-w-4xl rounded-lg bg-base-100 shadow-xl">
        {content}
      </div>
    </div>
  ) : (
    content
  );
}
