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
import {
  useClearArticleCategoryMutation,
  useDeleteArticleCategoryMutation,
} from "@/services/articleCategoriesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

/** Keeps category association clearing and deletion as two explicit confirmations. */
export function CategoryDeleteDialog({ category, onClose, onDone }) {
  const [mode, setMode] = useState(
    category.articleCount > 0 ? "clear" : "delete",
  );
  const [clearedCount, setClearedCount] = useState(null);
  const [error, setError] = useState(null);
  const [clearCategory, clearState] = useClearArticleCategoryMutation();
  const [deleteCategory, deleteState] = useDeleteArticleCategoryMutation();
  const pending = clearState.isLoading || deleteState.isLoading;

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (pending) return;
    setError(null);
    try {
      if (mode === "clear") {
        const result = await clearCategory({
          categoryId: category.id,
        }).unwrap();
        setClearedCount(result.removedArticleCount);
        setMode("delete");
        return;
      }
      await deleteCategory({ categoryId: category.id }).unwrap();
      onDone(`分类“${category.name}”已删除。`);
      onClose();
    } catch (requestError) {
      if (requestError.errorCode === "ArticleCategoryInUse") setMode("clear");
      setError(requestError);
    }
  };

  return (
    <AlertDialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <AlertDialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit}>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {mode === "clear" ? "解除文章关联" : "删除文章分类"}
            </AlertDialogTitle>
            <AlertDialogDescription>“{category.name}”</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="mt-3 space-y-3 text-sm">
            {mode === "clear" ? (
              <p>
                该操作会解除所有草稿、已发布和已归档文章与此分类的关联，但不会删除文章。当前显示的{" "}
                {category.articleCount} 篇不包含已归档文章。
              </p>
            ) : (
              <p>
                {clearedCount === null
                  ? "仅当分类没有任何文章关联时才能删除。"
                  : `已解除 ${clearedCount} 条关联。删除仍需再次确认。`}
              </p>
            )}
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {error.errorCode === "ArticleCategoryInUse"
                    ? "分类仍有关联，请先明确解除所有文章关联。"
                    : getErrorMessage(error)}
                </AlertDescription>
              </Alert>
            ) : null}
          </div>
          <AlertDialogFooter className="mt-4">
            <AlertDialogCancel type="button" disabled={pending}>
              取消
            </AlertDialogCancel>
            <Button type="submit" variant="destructive" disabled={pending}>
              {pending
                ? "正在处理"
                : mode === "clear"
                  ? "确认解除关联"
                  : "确认删除"}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  );
}
