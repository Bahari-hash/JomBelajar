import { useRef, useState } from "react";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { AlertDialog, AlertDialogContent, AlertDialogDescription, AlertDialogHeader, AlertDialogTitle, AlertDialogFooter } from "@/components/ui/alert-dialog.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";

import { collectResourceSnapshot } from "./collectResourceSnapshot.js";

/** Shared explicit confirmation and per-resource outcomes for destructive admin operations. */
export function BulkResourceActions({ label, selected = [], onClear, loadPage, remove, onDone, allOnly = false, description }) {
  const [target, setTarget] = useState(null);
  const [phrase, setPhrase] = useState("");
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState("");
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);
  const locked = useRef(false);
  const prepareAll = async () => {
    if (locked.current) return;
    locked.current = true; setBusy(true); setError(null); setPhrase("");
    try {
      const items = await collectResourceSnapshot(loadPage);
      if (!items.length) setError(`没有可删除的${label}。`);
      else setTarget({ items, all: true });
    } catch (e) { setError(getErrorMessage(e)); }
    finally { locked.current = false; setBusy(false); }
  };
  const execute = async () => {
    if (locked.current || !target || (target.all && phrase !== `删除全部${label}`)) return;
    locked.current = true; setBusy(true); let removed = 0; const failed = [];
    for (const [index, item] of target.items.entries()) {
      setProgress(`${index + 1} / ${target.items.length}`);
      try { await remove(item); removed++; }
      catch (e) { failed.push({ id: item.id, name: item.name ?? item.headword, message: e?.errorCode === "AudioInUse" ? "音频被内容引用，请先解除关联后重试。" : getErrorMessage(e) }); }
    }
    setResult({ removed, failed }); setTarget(null); setProgress(""); setBusy(false); locked.current = false;
    onClear?.(); onDone?.();
  };
  return <div className="space-y-3">
    <div className="flex flex-wrap items-center gap-3">
      {!allOnly ? <>
        <span className="text-sm">已选 {selected.length} 个（可跨页勾选）</span>
        <Button variant="outline" disabled={busy || !selected.length} onClick={onClear}>清空选择</Button>
        <Button variant="destructive" disabled={busy || !selected.length} onClick={() => { setPhrase(""); setTarget({ items: [...selected], all: false }); }}>批量删除{label}</Button>
      </> : null}
      <Button variant="destructive" disabled={busy} onClick={() => void prepareAll()}>{busy && !target ? "正在读取全部内容…" : `删除全部${label}`}</Button>
    </div>
    {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
    {result ? <div role="status" className="text-sm"><p>已删除 {result.removed} 个{label}，{result.failed.length} 个未删除。</p>
      {result.failed.length ? <ul className="max-h-48 overflow-auto">{result.failed.map(x => <li key={x.id}>{x.name}：{x.message}</li>)}</ul> : null}</div> : null}
    <AlertDialog open={Boolean(target)} onOpenChange={open => { if (!open && !busy) setTarget(null); }}>
      <AlertDialogContent>
        <AlertDialogHeader><AlertDialogTitle>{target?.all ? "删除全部" : "批量删除"}{label}（{target?.items.length ?? 0} 个）</AlertDialogTitle>
          <AlertDialogDescription>{target?.all ? "范围是整个库，不受当前搜索、筛选和分页限制；只删除本次读取确认的条目。" : "仅删除本次勾选的条目。"}此操作不可撤销。{description}</AlertDialogDescription></AlertDialogHeader>
        <ul className="max-h-40 overflow-auto text-sm">{target?.items.map(x => <li key={x.id}>{x.name ?? x.headword}</li>)}</ul>
        {target?.all ? <label className="space-y-2 text-sm">请输入“删除全部{label}”确认<Input aria-label="删除确认文字" value={phrase} disabled={busy} onChange={e => setPhrase(e.target.value)} /></label> : null}
        <AlertDialogFooter><Button variant="outline" disabled={busy} onClick={() => setTarget(null)}>取消</Button>
          <Button variant="destructive" disabled={busy || (target?.all && phrase !== `删除全部${label}`)} onClick={() => void execute()}>{busy ? `正在删除 ${progress}` : "确认删除"}</Button></AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  </div>;
}
