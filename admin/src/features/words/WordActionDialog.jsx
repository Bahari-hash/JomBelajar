import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog.jsx";
import { Button } from "@/components/ui/button.jsx";
import { WordDeleteDialog } from "@/features/words/WordDeleteDialog.jsx";

/** Temporary compatibility entry until the word editor is rebuilt in Task 4. */
export function WordActionDialog({
  action,
  word,
  onClose,
  onDone,
  onConflict,
}) {
  if (action === "delete")
    return (
      <WordDeleteDialog
        word={word}
        onClose={onClose}
        onDone={(message) => onDone(message, null)}
        onConflict={onConflict}
      />
    );
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>操作已移除</AlertDialogTitle>
          <AlertDialogDescription>
            单词创建后立即可用，不再支持发布、下架或归档操作。
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <Button type="button" onClick={onClose}>
            关闭
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
