import { useRef, useState } from "react";
import { ArrowLeft, CheckCircle2, FileJson } from "lucide-react";
import { Link } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import { WordUnsavedChangesDialog } from "@/features/words/WordUnsavedChangesDialog.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { useUnsavedChanges } from "@/hooks/useUnsavedChanges.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useImportWordBatchMutation,
  useValidateWordBatchMutation,
} from "@/services/wordsApi.js";

const EXAMPLE = JSON.stringify(
  {
    rows: [
      {
        headword: "bonjour",
        senses: [
          {
            partOfSpeech: "Interjection",
            definition: "你好",
            usageNote: "用于见面问候。",
            sortOrder: 0,
            examples: [
              {
                sentence: "Bonjour, Marie!",
                translation: "你好，玛丽！",
                audioClipId: null,
                sortOrder: 0,
              },
            ],
          },
        ],
        pronunciations: [],
      },
    ],
  },
  null,
  2,
);

function parseRequest(text) {
  let value;
  try {
    value = JSON.parse(text);
  } catch {
    throw new Error("JSON 语法无效，请检查括号、引号和逗号。");
  }
  if (
    !value ||
    typeof value !== "object" ||
    Array.isArray(value) ||
    !Array.isArray(value.rows)
  )
    throw new Error("顶层必须是包含 rows 数组的 JSON 对象。");
  if (value.rows.length < 1 || value.rows.length > 100)
    throw new Error("rows 必须包含 1 至 100 行。");
  return value;
}

function WordBatchImport() {
  useAdminPage("批量录入单词");
  const allowNavigationRef = useRef(false);
  const [text, setText] = useState("");
  const [baseline, setBaseline] = useState("");
  const [syntaxError, setSyntaxError] = useState(null);
  const [validation, setValidation] = useState(null);
  const [validatedPayload, setValidatedPayload] = useState(null);
  const [result, setResult] = useState(null);
  const [requestError, setRequestError] = useState(null);
  const [validateBatch, validateState] = useValidateWordBatchMutation();
  const [importBatch, importState] = useImportWordBatchMutation();
  const pending = validateState.isLoading || importState.isLoading;
  const blocker = useUnsavedChanges(text !== baseline, allowNavigationRef);

  const handleTextChange = (value) => {
    setText(value);
    setSyntaxError(null);
    setValidation(null);
    setValidatedPayload(null);
    setResult(null);
    setRequestError(null);
  };

  const handleValidate = async () => {
    if (pending) return;
    setSyntaxError(null);
    setRequestError(null);
    setResult(null);
    let payload;
    try {
      payload = parseRequest(text);
    } catch (error) {
      setSyntaxError(error.message);
      return;
    }
    try {
      const response = await validateBatch(payload).unwrap();
      setValidation(response);
      setValidatedPayload(payload);
    } catch (error) {
      setRequestError(error);
    }
  };

  const handleImport = async () => {
    if (pending || !validation?.isValid || !validatedPayload) return;
    setRequestError(null);
    try {
      const response = await importBatch(validatedPayload).unwrap();
      setResult(response);
      setBaseline(text);
    } catch (error) {
      setRequestError(error);
    }
  };

  const lineCount = text ? text.split(/\r?\n/).length : 0;
  return (
    <div className="space-y-5">
      <header>
        <Button variant="ghost" size="sm" asChild className="-ml-3 mb-2">
          <Link to="/words">
            <ArrowLeft aria-hidden="true" />
            返回单词列表
          </Link>
        </Button>
        <h1 className="text-2xl font-semibold">批量录入单词</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          先校验完整 JSON，再确认原子导入。导入结果均为草稿。
        </p>
      </header>
      <section className="space-y-2">
        <div className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <Label htmlFor="word-batch-json">
              批量 JSON <span aria-hidden="true">*</span>
            </Label>
            <p className="text-xs text-muted-foreground">
              最多 100 行，请求上限 2 MiB。音频字段只填写现有 audioClipId。
            </p>
          </div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={pending}
            onClick={() => handleTextChange(EXAMPLE)}
          >
            <FileJson aria-hidden="true" />
            插入示例
          </Button>
        </div>
        <Textarea
          id="word-batch-json"
          value={text}
          disabled={pending}
          aria-invalid={Boolean(syntaxError)}
          className="min-h-120 resize-y font-mono text-sm"
          spellCheck="false"
          onChange={(event) => handleTextChange(event.target.value)}
        />
        <div className="flex justify-between gap-3 text-xs text-muted-foreground">
          <span>
            {syntaxError ? (
              <span className="text-destructive" role="alert">
                {syntaxError}
              </span>
            ) : (
              "JSON 输入变化后需要重新校验。"
            )}
          </span>
          <span className="shrink-0 tabular-nums">
            {lineCount} 行 · {text.length.toLocaleString()} 字符
          </span>
        </div>
      </section>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          disabled={pending || !text.trim()}
          onClick={handleValidate}
        >
          {validateState.isLoading ? "正在校验" : "校验内容"}
        </Button>
        <Button
          type="button"
          variant="outline"
          disabled={pending || !validation?.isValid || !validatedPayload}
          onClick={handleImport}
        >
          {importState.isLoading ? "正在导入" : "确认原子导入"}
        </Button>
      </div>
      {requestError ? (
        <Alert variant="destructive">
          <AlertTitle>
            {importState.isError ? "批量导入失败" : "批量校验失败"}
          </AlertTitle>
          <AlertDescription>{getErrorMessage(requestError)}</AlertDescription>
          {Object.keys(requestError.fieldErrors ?? {}).length ? (
            <ul className="mt-3 space-y-1 text-sm">
              {Object.entries(requestError.fieldErrors).map(
                ([field, messages]) => (
                  <li key={field}>
                    <code>{field}</code>：{messages.join("；")}
                  </li>
                ),
              )}
            </ul>
          ) : null}
        </Alert>
      ) : null}
      {validation ? <ValidationResult validation={validation} /> : null}
      {result ? (
        <Alert role="status">
          <CheckCircle2 aria-hidden="true" />
          <AlertTitle>批量导入成功</AlertTitle>
          <AlertDescription>
            已创建 {result.createdCount} 个草稿，批次 ID：
            <code>{result.batchId}</code>
          </AlertDescription>
          <div className="mt-3 flex flex-wrap gap-2">
            {result.items.map((item) => (
              <Button key={item.wordId} size="sm" variant="outline" asChild>
                <Link to={`/words/${item.wordId}`}>
                  第 {item.rowIndex + 1} 行
                </Link>
              </Button>
            ))}
          </div>
        </Alert>
      ) : null}
      <WordUnsavedChangesDialog blocker={blocker} batch />
    </div>
  );
}

function ValidationResult({ validation }) {
  const errorCount =
    validation.errors.length +
    validation.rows.reduce((count, row) => count + row.errors.length, 0);
  return (
    <section className="space-y-3" aria-live="polite">
      <Alert variant={validation.isValid ? "default" : "destructive"}>
        <AlertTitle>
          {validation.isValid
            ? "校验通过，可以导入"
            : `校验未通过，共 ${errorCount} 个字段问题`}
        </AlertTitle>
        <AlertDescription>
          {validation.isValid
            ? "以下是服务端规范化后的预览。"
            : "请根据字段路径修正 JSON 后重新校验。"}
        </AlertDescription>
      </Alert>
      {validation.errors.length ? (
        <ErrorList title="批次错误" errors={validation.errors} />
      ) : null}
      <div className="space-y-2">
        {validation.rows.map((row) => (
          <div key={row.rowIndex} className="rounded-lg border p-3">
            <div className="flex items-center justify-between gap-3">
              <h3 className="font-medium">第 {row.rowIndex + 1} 行</h3>
              <span className="text-sm text-muted-foreground">
                {row.normalized
                  ? row.normalized.headword
                  : "无法生成预览"}
              </span>
            </div>
            {row.errors.length ? (
              <ErrorList errors={row.errors} />
            ) : row.normalized ? (
              <p className="mt-2 text-sm text-muted-foreground">
                {row.normalized.senses.length} 个释义 ·{" "}
                {row.normalized.pronunciations.length} 个发音
              </p>
            ) : null}
          </div>
        ))}
      </div>
    </section>
  );
}

function ErrorList({ title, errors }) {
  return (
    <div className="mt-3">
      {title ? <h3 className="text-sm font-medium">{title}</h3> : null}
      <ul className="mt-1 space-y-1 text-sm">
        {errors.map((error, index) => (
          <li key={`${error.field}-${index}`}>
            <code>{error.field}</code>：{error.messages.join("；")}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default WordBatchImport;
