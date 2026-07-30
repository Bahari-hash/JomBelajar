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
  useArchiveArticleMutation,
  usePublishArticleMutation,
  useUnpublishArticleMutation,
} from "@/services/articlesApi.js";

const COPY = {
  publish: {
    title: "发布文章",
    consequence: "发布后文章将对读者公开。",
    submit: "确认发布",
  },
  unpublish: {
    title: "下架文章",
    consequence: "下架后文章将不再公开，并恢复为草稿。",
    submit: "确认下架",
  },
  archive: {
    title: "归档文章",
    consequence: "归档是终态，当前无法恢复或继续编辑。",
    submit: "确认归档",
  },
};

/** Confirms one article state transition and keeps server conflicts recoverable. */
export function ArticleActionDialog({
  action,
  article,
  onClose,
  onDone,
  onConflict,
}) {
  const [error, setError] = useState(null);
  const [publishArticle, publishState] = usePublishArticleMutation();
  const [unpublishArticle, unpublishState] = useUnpublishArticleMutation();
  const [archiveArticle, archiveState] = useArchiveArticleMutation();
  const pending =
    publishState.isLoading ||
    unpublishState.isLoading ||
    archiveState.isLoading;
  const copy = COPY[action];

  const handleSubmit = async () => {
    if (pending) return;
    setError(null);
    try {
      if (action === "publish")
        await publishArticle({ articleId: article.id }).unwrap();
      if (action === "unpublish")
        await unpublishArticle({ articleId: article.id }).unwrap();
      if (action === "archive")
        await archiveArticle({ articleId: article.id }).unwrap();
      onDone(`${copy.title}成功。`);
      onClose();
    } catch (requestError) {
      setError(requestError);
      if (requestError.errorCode === "ArticleStatusConflict") onConflict();
    }
  };

  return (
    <AlertDialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{copy.title}</AlertDialogTitle>
          <AlertDialogDescription>“{article.title}”</AlertDialogDescription>
        </AlertDialogHeader>
        <p className="mt-3 text-sm">{copy.consequence}</p>
        {error ? (
          <Alert variant="destructive" className="mt-4">
            <AlertDescription>
              {error.errorCode === "ArticleStatusConflict"
                ? "文章状态已变化，列表正在刷新，请确认最新状态后重试。"
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
            onClick={handleSubmit}
            variant={action === "archive" ? "destructive" : "default"}
            disabled={pending}
          >
            {pending ? "正在处理" : copy.submit}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
