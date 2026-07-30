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

/** Resolves a React Router blocker without silently discarding editor content. */
export function UnsavedChangesDialog({ blocker }) {
  if (blocker.state !== "blocked") return null;
  return (
    <AlertDialog open onOpenChange={(open) => !open && blocker.reset()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>离开文章编辑？</AlertDialogTitle>
          <AlertDialogDescription>
            尚未保存的修改将会丢失，正在上传的图片也会被取消。
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel type="button" onClick={() => blocker.reset()}>
            继续编辑
          </AlertDialogCancel>
          <Button
            type="button"
            variant="destructive"
            onClick={() => blocker.proceed()}
          >
            放弃修改并离开
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
