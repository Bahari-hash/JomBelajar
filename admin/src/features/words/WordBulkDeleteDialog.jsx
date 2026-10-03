import { useRef, useState } from "react";
import { AlertDialog, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog.jsx";
import { Button } from "@/components/ui/button.jsx";
import { useDeleteWordMutation } from "@/services/wordsApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

/** Deletes only confirmed snapshots; failed or changed words are reported for review. */
export function WordBulkDeleteDialog({ words, onClose, onDone }) {
  const [remove] = useDeleteWordMutation();
  const locked = useRef(false);
  const [progress, setProgress] = useState(null);
  const submit = async () => {
    if (locked.current) return;
    locked.current = true;
    const failed = []; let removed = 0;
    for (let index = 0; index < words.length; index++) {
      setProgress(`${index + 1} / ${words.length}`);
      const word = words[index];
      try { await remove({ wordId: word.id, concurrencyStamp: word.concurrencyStamp }).unwrap(); removed++; }
      catch (error) { failed.push({ word, message: getErrorMessage(error) }); }
    }
    onDone(removed, failed); onClose();
  };
  return <AlertDialog open onOpenChange={open => { if (!open && !locked.current) onClose(); }}>
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>批量删除 {words.length} 个单词</AlertDialogTitle>
        <AlertDialogDescription>永久删除所选单词及相关学习记录，此操作不可撤销。未选中的单词不会删除。</AlertDialogDescription>
      </AlertDialogHeader>
      <ul className="max-h-48 overflow-auto text-sm">{words.map(w => <li key={w.id}>{w.headword}</li>)}</ul>
      <AlertDialogFooter>
        <Button variant="outline" disabled={progress !== null} onClick={onClose}>取消</Button>
        <Button variant="destructive" disabled={progress !== null} onClick={() => void submit()}>{progress ? `正在删除 ${progress}` : "确认批量删除"}</Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>;
}
