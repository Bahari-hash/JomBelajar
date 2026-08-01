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
import {
  useArchiveWordMutation,
  useDeleteWordMutation,
  usePublishWordMutation,
  useUnpublishWordMutation,
} from "@/services/wordsApi.js";

const COPY = {
  publish: {
    title: "发布单词",
    consequence: "发布后该单词会进入用户可学习内容。",
    submit: "确认发布",
  },
  unpublish: {
    title: "下架单词",
    consequence: "下架后用户将无法继续选择该单词，已有学习记录会保留。",
    submit: "确认下架",
  },
  archive: {
    title: "归档单词",
    consequence: "归档后不能编辑、恢复或永久删除，但内容和学习历史会保留。",
    submit: "确认归档",
  },
  delete: {
    title: "永久删除单词",
    consequence: "此操作不可撤销。存在学习历史的单词将被服务端拒绝删除。",
    submit: "永久删除",
  },
};

/** Confirms one concurrency-protected word lifecycle command. */
export function WordActionDialog({
  action,
  word,
  onClose,
  onDone,
  onConflict,
}) {
  const [error, setError] = useState(null);
  const [publish, publishState] = usePublishWordMutation();
  const [unpublish, unpublishState] = useUnpublishWordMutation();
  const [archive, archiveState] = useArchiveWordMutation();
  const [remove, deleteState] = useDeleteWordMutation();
  const pending =
    publishState.isLoading ||
    unpublishState.isLoading ||
    archiveState.isLoading ||
    deleteState.isLoading;
  const copy = COPY[action];
  const handleSubmit = async () => {
    if (pending) return;
    setError(null);
    const argument = {
      wordId: word.id,
      concurrencyStamp: word.concurrencyStamp,
    };
    try {
      let result;
      if (action === "publish") result = await publish(argument).unwrap();
      if (action === "unpublish") result = await unpublish(argument).unwrap();
      if (action === "archive") result = await archive(argument).unwrap();
      if (action === "delete") await remove(argument).unwrap();
      onDone(`${copy.title}成功。`, result ?? null);
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
    <AlertDialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{copy.title}</AlertDialogTitle>
          <AlertDialogDescription>“{word.headword}”</AlertDialogDescription>
        </AlertDialogHeader>
        <p className="mt-3 text-sm">{copy.consequence}</p>
        {error ? (
          <Alert variant="destructive" className="mt-4">
            <AlertDescription>
              {error.errorCode === "WordConcurrencyConflict"
                ? "单词已被其他管理员修改，正在获取最新内容，请确认后重试。"
                : getErrorMessage(error)}
            </AlertDescription>
          </Alert>
        ) : null}
        <AlertDialogFooter className="mt-4">
          <AlertDialogCancel type="button" disabled={pending}>
            取消
          </AlertDialogCancel>
          <Button
            type="button"
            variant={
              action === "archive" || action === "delete"
                ? "destructive"
                : "default"
            }
            disabled={pending}
            onClick={handleSubmit}
          >
            {pending ? "正在处理" : copy.submit}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
