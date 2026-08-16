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

/** Resolves navigation blocked by unsaved word content. */
export function WordUnsavedChangesDialog({ blocker }) {
  if (blocker.state !== "blocked") return null;
  return (
    <AlertDialog open onOpenChange={(open) => !open && blocker.reset()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            离开单词编辑？
          </AlertDialogTitle>
          <AlertDialogDescription>
            尚未提交的内容将会丢失。
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
