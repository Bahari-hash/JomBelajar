import { useEffect, useRef, useState } from "react";
import { ImagePlus, X } from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import { Progress } from "@/components/ui/progress.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  putPresignedObject,
  useConfirmArticlePictureMutation,
  usePresignArticlePictureMutation,
} from "@/services/articleMediaApi.js";

const ALLOWED_TYPES = new Set([
  "image/png",
  "image/jpeg",
  "image/gif",
  "image/webp",
]);
const MAX_SIZE = 5 * 1024 * 1024;

/** Coordinates one cancellable presign, isolated Axios PUT and idempotent confirm flow. */
export function ImageUploadControl({ label, onUploaded, disabled }) {
  const inputRef = useRef(null);
  const controllerRef = useRef(null);
  const requestRef = useRef(null);
  const [state, setState] = useState({
    status: "idle",
    progress: 0,
    file: null,
    error: null,
  });
  const [presign] = usePresignArticlePictureMutation();
  const [confirm] = useConfirmArticlePictureMutation();
  const uploading = ["presigning", "uploading", "confirming"].includes(
    state.status,
  );

  useEffect(
    () => () => {
      controllerRef.current?.abort();
      requestRef.current?.abort?.();
    },
    [],
  );

  const upload = async (file) => {
    if (
      !ALLOWED_TYPES.has(file.type) ||
      file.size <= 0 ||
      file.size > MAX_SIZE
    ) {
      setState({
        status: "error",
        progress: 0,
        file,
        error: "仅支持不超过 5 MB 的 PNG、JPEG、GIF 或 WebP 图片。",
      });
      return;
    }
    const controller = new AbortController();
    controllerRef.current = controller;
    try {
      setState({ status: "presigning", progress: 0, file, error: null });
      requestRef.current = presign(file);
      const signed = await requestRef.current.unwrap();
      setState({ status: "uploading", progress: 0, file, error: null });
      await putPresignedObject({
        url: signed.presignedUrl,
        file,
        signal: controller.signal,
        onProgress: (progress) =>
          setState((current) => ({ ...current, progress })),
      });
      setState({ status: "confirming", progress: 100, file, error: null });
      requestRef.current = confirm(signed.resourceId);
      const media = await requestRef.current.unwrap();
      onUploaded({ ...media, name: file.name });
      setState({ status: "idle", progress: 0, file: null, error: null });
      if (inputRef.current) inputRef.current.value = "";
    } catch (error) {
      setState({
        status: error?.kind === "aborted" ? "cancelled" : "error",
        progress: 0,
        file,
        error:
          error?.kind === "aborted"
            ? "上传已取消。"
            : getErrorMessage(error, "图片上传失败，请重试。"),
      });
    } finally {
      controllerRef.current = null;
      requestRef.current = null;
    }
  };

  const handleCancel = () => {
    controllerRef.current?.abort();
    requestRef.current?.abort?.();
  };
  return (
    <div className="space-y-2" data-uploading={uploading}>
      <input
        ref={inputRef}
        className="sr-only"
        type="file"
        accept="image/png,image/jpeg,image/gif,image/webp"
        disabled={disabled || uploading}
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) upload(file);
        }}
      />
      <div className="flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled || uploading}
          onClick={() => inputRef.current?.click()}
        >
          <ImagePlus aria-hidden="true" />
          {label}
        </Button>
        {uploading ? (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={handleCancel}
          >
            <X aria-hidden="true" />
            取消
          </Button>
        ) : null}
        <span
          className="truncate text-xs text-muted-foreground"
          aria-live="polite"
        >
          {uploading
            ? `${state.file?.name} · ${state.status === "presigning" ? "正在准备" : state.status === "confirming" ? "正在确认" : `上传 ${state.progress}%`}`
            : state.error}
        </span>
      </div>
      {uploading ? (
        <Progress
          value={state.progress}
          aria-label={`上传进度 ${state.progress}%`}
        />
      ) : null}
    </div>
  );
}
