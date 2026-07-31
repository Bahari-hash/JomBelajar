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
  useArchiveVideoMutation,
  usePublishVideoMutation,
  useRetryVideoMutation,
  useUnpublishVideoMutation,
} from "@/services/videosApi.js";

const DETAILS = {
  publish: {
    title: "发布视频",
    description: "发布后学习用户可以在视频目录中看到并播放该视频。",
    confirm: "确认发布",
  },
  unpublish: {
    title: "下架视频",
    description: "下架后学习用户将无法继续打开该视频，之后可重新发布。",
    confirm: "确认下架",
  },
  retry: {
    title: "重试视频处理",
    description: "系统会创建新的处理任务，保留已有失败记录。",
    confirm: "确认重试",
  },
  archive: {
    title: "归档视频",
    description:
      "归档是终态，视频会从学习端和默认管理列表隐藏，但不会删除源文件、处理历史或学习进度。",
    confirm: "确认归档",
  },
};

/** Confirms one concurrency-stamped video state mutation. */
export function VideoActionDialog({
  action,
  video,
  onClose,
  onDone,
  onConflict,
}) {
  const [error, setError] = useState(null);
  const mutations = {
    publish: usePublishVideoMutation(),
    unpublish: useUnpublishVideoMutation(),
    retry: useRetryVideoMutation(),
    archive: useArchiveVideoMutation(),
  };
  const [mutate, state] = mutations[action];
  const detail = DETAILS[action];
  const handleSubmit = async (event) => {
    event.preventDefault();
    if (state.isLoading) return;
    try {
      const saved = await mutate({
        videoId: video.id,
        concurrencyStamp: video.concurrencyStamp,
      }).unwrap();
      onDone(
        `${saved.title} 已完成${detail.title.replace("视频", "")}。`,
        saved,
      );
      onClose();
    } catch (requestError) {
      setError(requestError);
      if (
        [
          "VideoConcurrencyConflict",
          "VideoStatusConflict",
          "VideoArchiveConflict",
          "VideoRetryConflict",
        ].includes(requestError.errorCode)
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
            <AlertDialogTitle>{detail.title}</AlertDialogTitle>
            <AlertDialogDescription>“{video.title}”</AlertDialogDescription>
          </AlertDialogHeader>
          <div className="mt-3 space-y-3 text-sm">
            <p>{detail.description}</p>
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {error.errorCode === "VideoConcurrencyConflict"
                    ? "视频已被其他管理员修改，列表已刷新，请确认最新状态后重试。"
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
              variant={action === "archive" ? "destructive" : "default"}
              disabled={state.isLoading}
            >
              {state.isLoading ? "正在处理" : detail.confirm}
            </Button>
          </AlertDialogFooter>
        </form>
      </AlertDialogContent>
    </AlertDialog>
  );
}
