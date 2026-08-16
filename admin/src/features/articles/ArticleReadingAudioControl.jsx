import { useState } from "react";
import { AudioLines, Library, Unlink, Upload } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { AudioResourcePickerDialog } from "@/features/audio/AudioResourcePickerDialog.jsx";
import { AudioUploadControl } from "@/features/audio/AudioUploadControl.jsx";
import { AUDIO_STATUS_LABELS } from "@/services/audioContracts.js";

/** Edits the article's optional association to one shared audio resource. */
export function ArticleReadingAudioControl({ value, onChange, disabled }) {
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);

  return (
    <div className="space-y-4">
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
                {AUDIO_STATUS_LABELS[value.status]}
              </Badge>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">未关联朗读音频</p>
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

      {value?.status === "Failed" ? (
        <Alert variant="destructive">
          <AlertDescription>
            当前朗读音频处理失败，关联仍会保留。请前往音频资源库重新上传或重新处理。
          </AlertDescription>
        </Alert>
      ) : null}

      {uploadOpen ? (
        <div className="border-b pb-4">
          <AudioUploadControl
            waitForProcessing={false}
            onStarted={(id, originalName) =>
              onChange({
                id,
                name: originalName,
                status: "Uploading",
                durationSeconds: null,
                lastFailureCode: null,
              })
            }
            onCompleted={(audio) => onChange(audio)}
          />
        </div>
      ) : null}

      <AudioResourcePickerDialog
        open={pickerOpen}
        value={value}
        onOpenChange={setPickerOpen}
        onSelect={(audio) => {
          onChange(audio);
          setPickerOpen(false);
        }}
      />
    </div>
  );
}
