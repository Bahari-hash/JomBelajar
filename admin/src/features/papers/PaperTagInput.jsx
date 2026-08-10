import { useId, useState } from "react";
import { Plus, X } from "lucide-react";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";

const MAX_TAGS = 10;
const MAX_TAG_LENGTH = 30;

export function PaperTagInput({
  value,
  disabled = false,
  error,
  onChange,
  id,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedBy,
  "data-field-path": fieldPath,
}) {
  const [pending, setPending] = useState("");
  const [localError, setLocalError] = useState(null);
  const generatedId = useId();
  const limitReached = value.length >= MAX_TAGS;
  const validationMessage = error ?? localError;
  const statusMessage = limitReached ? "最多添加 10 个标签。" : null;
  const message = validationMessage ?? statusMessage;
  const messageId = `${id ?? generatedId}-message`;

  const addTag = () => {
    const normalized = pending.trim().toLowerCase();
    if (!normalized) {
      setLocalError("标签不能为空。");
      return;
    }
    if (normalized.length > MAX_TAG_LENGTH) {
      setLocalError("单个标签不能超过 30 个字符。");
      return;
    }
    if (value.includes(normalized)) {
      setPending("");
      setLocalError(null);
      return;
    }
    if (limitReached) return;
    onChange([...value, normalized]);
    setPending("");
    setLocalError(null);
  };

  const removeTag = (tag) => {
    onChange(value.filter((item) => item !== tag));
    setLocalError(null);
  };

  return (
    <div className="space-y-2">
      {value.length > 0 ? (
        <div className="flex flex-wrap gap-2" aria-label="已添加的试卷标签">
          {value.map((tag) => (
            <Badge
              key={tag}
              variant="secondary"
              className="max-w-full gap-1 py-1 pl-2 pr-1"
            >
              <span className="break-all">{tag}</span>
              <button
                type="button"
                className="inline-flex size-5 shrink-0 items-center justify-center rounded-sm hover:bg-background/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                aria-label={`删除标签 ${tag}`}
                title={`删除标签 ${tag}`}
                disabled={disabled}
                onClick={() => removeTag(tag)}
              >
                <X className="size-3.5" aria-hidden="true" />
              </button>
            </Badge>
          ))}
        </div>
      ) : null}

      <div className="flex flex-col gap-2 sm:flex-row">
        <Input
          id={id}
          value={pending}
          aria-label="试卷标签"
          aria-invalid={Boolean(validationMessage) || ariaInvalid}
          aria-describedby={message ? messageId : ariaDescribedBy}
          data-field-path={fieldPath}
          disabled={disabled || limitReached}
          placeholder="例如 grammar"
          onChange={(event) => {
            setPending(event.target.value);
            setLocalError(null);
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              addTag();
            }
          }}
        />
        <Button
          type="button"
          variant="outline"
          className="shrink-0"
          disabled={disabled || limitReached || pending.length === 0}
          onClick={addTag}
        >
          <Plus aria-hidden="true" />
          添加标签
        </Button>
      </div>

      <div className="flex items-start justify-between gap-3 text-xs text-muted-foreground">
        {message ? (
          <p
            id={messageId}
            className={validationMessage ? "text-destructive" : undefined}
          >
            {message}
          </p>
        ) : (
          <span />
        )}
        <span className="shrink-0">{value.length}/10</span>
      </div>
    </div>
  );
}
