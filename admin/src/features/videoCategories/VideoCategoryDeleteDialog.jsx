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
  useClearVideoCategoryMutation,
  useDeleteVideoCategoryMutation,
} from "@/services/videoCategoriesApi.js";

/** Keeps video-association clearing and category deletion as two explicit commands. */
export function VideoCategoryDeleteDialog({
  category,
  onClose,
  onDone,
  onCleared,
}) {
  const [mode, setMode] = useState(
    category.videoCount > 0 ? "clear" : "delete",
  );
  const [clearedCount, setClearedCount] = useState(null);
  const [error, setError] = useState(null);
  const [clearCategory, clearState] = useClearVideoCategoryMutation();
  const [deleteCategory, deleteState] = useDeleteVideoCategoryMutation();
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
        setClearedCount(result.removedVideoCount);
        await onCleared?.();
        setMode("delete");
        return;
      }
      await deleteCategory({ categoryId: category.id }).unwrap();
      onDone(`分类“${category.name}”已删除。`);
      onClose();
    } catch (requestError) {
      if (requestError.errorCode === "VideoCategoryInUse") setMode("clear");
      setError(requestError);
    }
  };
  return (
    <AlertDialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <AlertDialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit}>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {mode === "clear" ? "清空分类中的视频" : "删除视频分类"}
            </AlertDialogTitle>
            <AlertDialogDescription>“{category.name}”</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="mt-3 space-y-3 text-sm">
            {mode === "clear" ? (
              <p>
                该操作只解除所有状态视频与此分类的关联，不会删除视频。当前关联数量为{" "}
                {category.videoCount}。
              </p>
            ) : (
              <p>
                {clearedCount === null
                  ? "仅当分类没有任何视频关联时才能删除。"
                  : `已解除 ${clearedCount} 条关联并刷新分类数据。删除仍需再次确认。`}
              </p>
            )}
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {error.errorCode === "VideoCategoryInUse"
                    ? "分类仍有关联，可能有其他管理员刚刚添加视频。请重新清空后再删除。"
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
