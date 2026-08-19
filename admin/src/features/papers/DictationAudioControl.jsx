import { useEffect, useState } from "react";
import { AudioLines, Library, Unlink, Upload } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { AudioResourcePickerDialog } from "@/features/audio/AudioResourcePickerDialog.jsx";
import { AudioUploadControl } from "@/features/audio/AudioUploadControl.jsx";
import { AUDIO_STATUS_LABELS } from "@/services/audioContracts.js";

/** Associates one shared audio resource with a dictation question. */
export function DictationAudioControl({ value, onChange, disabled, error }) {
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);

  useEffect(() => {
    if (!disabled) return;
    setPickerOpen(false);
    setUploadOpen(false);
  }, [disabled]);

  return (
    <section className="space-y-3 border-t pt-4">
      <div className="flex min-h-16 flex-wrap items-center justify-between gap-3 border-y py-3">
        <div className="flex min-w-0 items-center gap-3">
          <AudioLines
            aria-hidden="true"
            className="size-5 shrink-0 text-muted-foreground"
          />
          {value ? (
            <div className="min-w-0">
              <p className="break-all text-sm font-medium">{value.name}</p>
              <Badge
                className="mt-1"
                variant={value.status === "Failed" ? "destructive" : "outline"}
              >
                {AUDIO_STATUS_LABELS[value.status] ?? value.status}
              </Badge>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">未关联听写音频</p>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={disabled}
            onClick={() => setPickerOpen(true)}
          >
            <Library aria-hidden="true" />
            从资源库选择
          </Button>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={disabled}
            onClick={() => setUploadOpen((open) => !open)}
          >
            <Upload aria-hidden="true" />
            上传新音频
          </Button>
          {value ? (
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={disabled}
              onClick={() => onChange(null)}
            >
              <Unlink aria-hidden="true" />
              解除关联
            </Button>
          ) : null}
        </div>
      </div>
      {error ? (
        <p className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
      {value && value.status !== "Ready" ? (
        <Alert variant="destructive">
          <AlertDescription>
            听写题只能使用处理成功的音频。请等待处理完成或重新选择资源。
          </AlertDescription>
        </Alert>
      ) : null}
      {uploadOpen ? (
        <div className="border-b pb-4">
          <AudioUploadControl
            waitForProcessing
            onStarted={(id, originalName) =>
              onChange({
                id,
                name: originalName,
                status: "Uploading",
                durationSeconds: null,
                lastFailureCode: null,
              })
            }
            onCompleted={onChange}
          />
        </div>
      ) : null}
      <AudioResourcePickerDialog
        open={pickerOpen}
        value={value}
        requiredStatus="Ready"
        onOpenChange={setPickerOpen}
        onSelect={(audio) => {
          if (audio.status === "Ready") onChange(audio);
          setPickerOpen(false);
        }}
      />
    </section>
  );
}
