import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog.jsx";
import { Button } from "@/components/ui/button.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";
import { useDeleteWordMutation } from "@/services/wordsApi.js";

/** Confirms concurrency-protected permanent word deletion. */
export function WordDeleteDialog({
  word,
  onClose,
  onDone,
  onConflict,
}) {
  const [error, setError] = useState(null);
  const [remove, state] = useDeleteWordMutation();
  const handleSubmit = async (event) => {
    event.preventDefault();
    if (state.isLoading) return;
    setError(null);
    try {
      await remove({
        wordId: word.id,
        concurrencyStamp: word.concurrencyStamp,
      }).unwrap();
      onDone("单词已删除");
      onClose();
    } catch (requestError) {
      setError(requestError);
      if (
        requestError.errorCode === "WordConcurrencyConflict" ||
        requestError.status === 409
      )
        onConflict?.();
    }
  };
  return (
    <AlertDialog
      open
      onOpenChange={(open) => !open && !state.isLoading && onClose()}
    >
      <AlertDialogContent>
        <form onSubmit={handleSubmit}>
          <AlertDialogHeader>
            <AlertDialogTitle>删除单词</AlertDialogTitle>
            <AlertDialogDescription>“{word.headword}”</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="mt-3 space-y-3 text-sm">
            <p>此操作不可撤销，并会删除该单词的相关学习记录。</p>
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {error.errorCode === "WordConcurrencyConflict"
                    ? "单词已被其他管理员修改，列表已刷新，请确认最新内容后重试。"
                    : getErrorMessage(error)}
                </AlertDescription>
              </Alert>
            ) : null}
          </div>
          <AlertDialogFooter className="mt-4">
            <AlertDialogCancel type="button" disabled={state.isLoading}>
              取消
            </AlertDialogCancel>
            <Button type="submit" variant="destructive" disabled={state.isLoading}>
              {state.isLoading ? "正在删除" : "确认删除"}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  );
}
