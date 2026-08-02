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
  useArchivePaperMutation,
  useDeletePaperMutation,
  usePublishPaperMutation,
  useUnpublishPaperMutation,
  useValidatePaperMutation,
} from "@/services/papersApi.js";

const COPY = {
  publish: {
    title: "发布试卷",
    consequence: "发布后用户可以开始在线测验。发布前服务端会重新检查完整性。",
    submit: "确认发布",
  },
  unpublish: {
    title: "下架试卷",
    consequence: "下架后用户目录不再展示该试卷，已有测验和结果会保留。",
    submit: "确认下架",
  },
  archive: {
    title: "归档试卷",
    consequence: "归档后不可恢复、不可编辑，但已有测验历史会继续保留。",
    submit: "确认归档",
  },
  delete: {
    title: "永久删除试卷",
    consequence: "此操作不可撤销。存在测验历史或当前状态不允许时，服务端会拒绝删除。",
    submit: "永久删除",
  },
};

/** Confirms one concurrency-protected Paper lifecycle command. */
export function PaperActionDialog({
  action,
  paper,
  onClose,
  onDone,
  onConflict,
}) {
  const [error, setError] = useState(null);
  const [publish, publishState] = usePublishPaperMutation();
  const [unpublish, unpublishState] = useUnpublishPaperMutation();
  const [archive, archiveState] = useArchivePaperMutation();
  const [remove, deleteState] = useDeletePaperMutation();
  const [validate, validateState] = useValidatePaperMutation();
  const [validationIssues, setValidationIssues] = useState([]);
  const pending =
    publishState.isLoading ||
    unpublishState.isLoading ||
    archiveState.isLoading ||
    deleteState.isLoading ||
    validateState.isLoading;
  const copy = COPY[action];

  const handleSubmit = async () => {
    if (pending) return;
    setError(null);
    setValidationIssues([]);
    const argument = {
      paperId: paper.id,
      concurrencyStamp: paper.concurrencyStamp,
    };
    try {
      let result;
      if (action === "publish") {
        const validation = await validate(argument).unwrap();
        if (!validation.isValid) {
          setValidationIssues(validation.issues);
          setError({
            detail: "发布检查未通过，请先修正详情页中的问题。",
          });
          return;
        }
        result = await publish(argument).unwrap();
      }
      if (action === "unpublish") result = await unpublish(argument).unwrap();
      if (action === "archive") result = await archive(argument).unwrap();
      if (action === "delete") await remove(argument).unwrap();
      onDone(`${copy.title}成功。`, result ?? null);
      onClose();
    } catch (requestError) {
      setError(requestError);
      if (
        requestError.errorCode === "PaperConcurrencyConflict" ||
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
          <AlertDialogDescription>“{paper.title || "未命名试卷"}”</AlertDialogDescription>
        </AlertDialogHeader>
        <p className="mt-3 text-sm">{copy.consequence}</p>
        {paper.attemptCount > 0 ? (
          <p className="mt-2 text-sm text-muted-foreground">
            当前已有 {paper.attemptCount} 次测验历史，内容已永久锁定。
          </p>
        ) : null}
        {error ? (
          <Alert variant="destructive" className="mt-4">
            <AlertDescription>
              {error.errorCode === "PaperConcurrencyConflict"
                ? "试卷已被其他管理员修改，已请求刷新最新内容，请确认后重试。"
                : getErrorMessage(error)}
            </AlertDescription>
          </Alert>
        ) : null}
        {validationIssues.length ? (
          <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-destructive">
            {validationIssues.map((issue, index) => (
              <li key={`${issue.field}-${issue.errorCode}-${index}`}>
                {issue.message}
              </li>
            ))}
          </ul>
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
