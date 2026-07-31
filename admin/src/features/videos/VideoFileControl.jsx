import { useRef } from "react";
import { FileUp } from "lucide-react";
import { Button } from "@/components/ui/button.jsx";

function formatFileSize(size) {
  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

/** Accessible video file picker styled consistently with administrator upload controls. */
export function VideoFileControl({
  id,
  file,
  accept,
  disabled,
  error,
  status,
  onFileChange,
}) {
  const inputRef = useRef(null);
  const statusId = `${id}-status`;

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-2">
      <input
        ref={inputRef}
        id={id}
        className="sr-only"
        type="file"
        accept={accept}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        aria-describedby={statusId}
        onClick={(event) => {
          event.currentTarget.value = "";
        }}
        onChange={(event) => onFileChange(event.target.files?.[0] ?? null)}
      />
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled}
        onClick={() => inputRef.current?.click()}
      >
        <FileUp aria-hidden="true" />
        {file ? "重新选择视频" : "选择视频"}
      </Button>
      <span
        id={statusId}
        className={
          error
            ? "min-w-0 truncate text-xs text-destructive"
            : "min-w-0 truncate text-xs text-muted-foreground"
        }
        title={file?.name}
        aria-live="polite"
      >
        {error ??
          status ??
          (file
            ? `${file.name} · ${formatFileSize(file.size)}`
            : "尚未选择文件")}
      </span>
    </div>
  );
}
