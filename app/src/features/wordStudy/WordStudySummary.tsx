import { useRef, useState } from "react";
import type { WordStudySessionState } from "./wordStudyTypes";

/** Persisted recall summary before the user's optional spelling practice. */
export default function WordStudySummary({ session, onChoose }: {
  session: WordStudySessionState; onChoose?: (skipSpelling: boolean) => Promise<void>;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState(false);
  const locked = useRef(false);
  const choose = async (skip: boolean) => {
    if (!onChoose || locked.current) return;
    locked.current = true; setPending(true); setError(false);
    try { await onChoose(skip); } catch { setError(true); }
    finally { locked.current = false; setPending(false); }
  };
  const summary = session.summary;
  return <section className="mx-auto max-w-3xl py-12 text-center">
    <h1 className="text-2xl font-bold">本组学习小结</h1>
    <p className="mt-4 text-base-content/70">完成 {summary?.wordCount ?? session.memorizationPassedCount} 个单词的记忆练习</p>
    {summary ? <>
      <p className="mt-2 text-sm text-base-content/60">共评分 {summary.ratingCount} 次（同一个词可能评分多次）</p>
      <dl className="my-8 grid grid-cols-2 gap-4 sm:grid-cols-4">
        {[["重来", summary.againCount], ["困难", summary.hardCount], ["良好", summary.goodCount], ["简单", summary.easyCount]].map(([label, count]) =>
          <div key={label} className="rounded-box bg-base-200 p-4"><dt>{label}</dt><dd className="mt-2 text-2xl font-semibold">{count}</dd></div>)}
      </dl>
    </> : null}
    <p className="mt-4 text-base-content/60">复习安排已保存。可以继续练习拼写，也可以直接结束本组。</p>
    {error ? <p role="alert" className="mt-4 text-error">操作未完成，请重试。</p> : null}
    <div className="mt-8 flex flex-wrap justify-center gap-3">
      <button className="btn btn-primary" disabled={pending || !onChoose} onClick={() => void choose(false)}>确认，进入拼写</button>
      <button className="btn btn-outline" disabled={pending || !onChoose} onClick={() => void choose(true)}>跳过拼写，完成本组</button>
    </div>
  </section>;
}
