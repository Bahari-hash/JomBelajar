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
import { useDeletePaperCategoryMutation } from "@/services/paperCategoriesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

export function PaperCategoryDeleteDialog({ category, onClose, onDone }) {
  const [error, setError] = useState(null);
  const [remove, state] = useDeletePaperCategoryMutation();
  const submit = async (event) => {
    event.preventDefault();
    setError(null);
    try {
      await remove({ categoryId: category.id }).unwrap();
      onDone(`分类“${category.name}”已删除。`);
      onClose();
    } catch (requestError) {
      setError(requestError);
    }
  };
  return (
    <AlertDialog
      open
      onOpenChange={(open) => !open && !state.isLoading && onClose()}
    >
      <AlertDialogContent className="sm:max-w-md">
        <form onSubmit={submit}>
          <AlertDialogHeader>
            <AlertDialogTitle>删除试卷分类</AlertDialogTitle>
            <AlertDialogDescription>“{category.name}”</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="mt-3 space-y-3 text-sm">
            <p>仅当分类没有任何试卷关联时才能删除。此操作不可撤销。</p>
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {error.errorCode === "PaperCategoryInUse"
                    ? "该分类仍被试卷使用，请先在相关试卷中解除关联。"
                    : getErrorMessage(error)}
                </AlertDescription>
              </Alert>
            ) : null}
          </div>
          <AlertDialogFooter className="mt-4">
            <AlertDialogCancel type="button" disabled={state.isLoading}>
              取消
            </AlertDialogCancel>
            <Button
              type="submit"
              variant="destructive"
              disabled={state.isLoading || category.paperCount > 0}
            >
              {state.isLoading
                ? "正在删除"
                : category.paperCount > 0
                  ? `仍关联 ${category.paperCount} 份试卷`
                  : "确认删除"}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  );
}
