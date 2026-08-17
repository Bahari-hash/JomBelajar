import { useRef, useState } from "react";
import { ArrowLeft, Download, FileJson, Upload } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  readWordBatchFile,
  WORD_BATCH_EXAMPLE_URL,
} from "@/features/words/wordBatchFile.js";
import { WordUnsavedChangesDialog } from "@/features/words/WordUnsavedChangesDialog.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { useUnsavedChanges } from "@/hooks/useUnsavedChanges.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useImportWordBatchMutation,
  useValidateWordBatchMutation,
} from "@/services/wordsApi.js";

const SUMMARY_ITEMS = [
  ["wordCount", "单词数"],
  ["senseCount", "释义数"],
  ["exampleCount", "例句数"],
  ["wordAudioReferenceCount", "单词音频引用"],
  ["exampleAudioReferenceCount", "例句音频引用"],
  ["matchedAudioReferenceCount", "已匹配音频"],
];

function ValidationSummary({ validation }) {
  return (
    <>
      <Alert variant={validation.isValid ? "default" : "destructive"}>
        <AlertTitle>{validation.isValid ? "校验通过" : "校验未通过"}</AlertTitle>
        <AlertDescription>
          {validation.isValid
            ? "校验通过，可以导入。"
            : `发现 ${validation.errors.length} 个错误，修正 JSON 后重新选择文件。`}
        </AlertDescription>
      </Alert>

      <section className="space-y-3 border-y py-4" aria-labelledby="batch-summary-title">
        <h2 id="batch-summary-title" className="text-sm font-semibold">
          批次统计
        </h2>
        <dl className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2 lg:grid-cols-3">
          {SUMMARY_ITEMS.map(([key, label]) => (
            <div key={key} className="flex items-center justify-between gap-4">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="font-medium tabular-nums">{validation.summary[key]}</dd>
            </div>
          ))}
        </dl>
      </section>

      {validation.errors.length > 0 ? (
        <section className="space-y-3" aria-labelledby="batch-errors-title">
          <h2 id="batch-errors-title" className="text-sm font-semibold">
            校验错误
          </h2>
          <div className="overflow-x-auto border-y">
            <table className="w-full min-w-2xl text-left text-sm">
              <thead className="bg-muted/50 text-xs text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 font-medium">位置</th>
                  <th className="px-3 py-2 font-medium">字段</th>
                  <th className="px-3 py-2 font-medium">错误</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {validation.errors.map((error, index) => (
                  <tr key={`${error.rowNumber ?? "batch"}-${error.field}-${index}`}>
                    <td className="whitespace-nowrap px-3 py-2">
                      {error.rowNumber ? `第 ${error.rowNumber} 行` : "整个批次"}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs">{error.field}</td>
                    <td className="px-3 py-2">{error.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      <section className="space-y-3" aria-labelledby="batch-preview-title">
        <h2 id="batch-preview-title" className="text-sm font-semibold">
          逐行预览
        </h2>
        <div className="overflow-x-auto border-y">
          <table className="w-full min-w-3xl text-left text-sm">
            <thead className="bg-muted/50 text-xs text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">行</th>
                <th className="px-3 py-2 font-medium">词头</th>
                <th className="px-3 py-2 font-medium">规范化词头</th>
                <th className="px-3 py-2 font-medium">单词音频</th>
                <th className="px-3 py-2 font-medium">释义 / 例句</th>
                <th className="px-3 py-2 font-medium">音频匹配</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {validation.rows.map((row) => (
                <tr key={row.rowNumber}>
                  <td className="px-3 py-2 tabular-nums">{row.rowNumber}</td>
                  <td className="px-3 py-2 font-medium">{row.headword ?? "-"}</td>
                  <td className="px-3 py-2">{row.normalizedHeadword ?? "-"}</td>
                  <td className="px-3 py-2">{row.wordAudioName ?? "-"}</td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {row.senseCount} / {row.exampleCount}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2">
                    {row.matchedAudioCount} / {row.audioReferenceCount}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </>
  );
}

function WordBatchImport() {
  useAdminPage("批量导入单词");
  const inputRef = useRef(null);
  const allowNavigationRef = useRef(false);
  const navigate = useNavigate();
  const [file, setFile] = useState(null);
  const [payload, setPayload] = useState(null);
  const [validation, setValidation] = useState(null);
  const [fileError, setFileError] = useState(null);
  const [requestError, setRequestError] = useState(null);
  const [completed, setCompleted] = useState(false);
  const [validateBatch, validateState] = useValidateWordBatchMutation();
  const [importBatch, importState] = useImportWordBatchMutation();
  const pending = validateState.isLoading || importState.isLoading;
  const blocker = useUnsavedChanges(Boolean(payload) && !completed, allowNavigationRef);
  const canImport = Boolean(payload && validation?.isValid && !pending);

  const selectFile = async (nextFile) => {
    setFile(nextFile);
    setPayload(null);
    setValidation(null);
    setFileError(null);
    setRequestError(null);
    setCompleted(false);
    if (!nextFile) return;
    try {
      const nextPayload = await readWordBatchFile(nextFile);
      setPayload(nextPayload);
      const result = await validateBatch(nextPayload).unwrap();
      setValidation(result);
    } catch (error) {
      if (error instanceof Error) setFileError(error.message);
      else setRequestError(getErrorMessage(error, "批量校验失败，请重试。"));
    }
  };

  const confirmImport = async () => {
    if (!canImport) return;
    setRequestError(null);
    try {
      const result = await importBatch(payload).unwrap();
      setCompleted(true);
      allowNavigationRef.current = true;
      navigate("/words", {
        replace: true,
        state: { notice: `已批量创建 ${result.createdCount} 个单词。` },
      });
    } catch (error) {
      if (error?.status === 422 && error.data) {
        setValidation(error.data);
        setRequestError(null);
      } else {
        setRequestError(getErrorMessage(error, "批量导入失败，请重试。"));
      }
    }
  };

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">单词管理</p>
          <h1 className="mt-1 text-2xl font-semibold">批量导入单词</h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" asChild>
            <Link to="/words">
              <ArrowLeft aria-hidden="true" />
              返回单词列表
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <a
              href={WORD_BATCH_EXAMPLE_URL}
              download="tiny-lang-word-import-example.json"
            >
              <Download aria-hidden="true" />
              下载示例 JSON
            </a>
          </Button>
        </div>
      </header>

      <section className="space-y-3 border-y py-4" aria-labelledby="batch-file-title">
        <div>
          <h2 id="batch-file-title" className="text-sm font-semibold">
            JSON 文件
          </h2>
          <p className="mt-1 text-xs text-muted-foreground">
            单批最多 1000 个单词，文件不能超过 20 MB。
          </p>
        </div>
        <input
          ref={inputRef}
          className="sr-only"
          type="file"
          accept=".json,application/json"
          disabled={pending}
          onClick={(event) => {
            event.currentTarget.value = "";
          }}
          onChange={(event) => void selectFile(event.target.files?.[0] ?? null)}
        />
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={pending}
            onClick={() => inputRef.current?.click()}
          >
            <FileJson aria-hidden="true" />
            {file ? "重新选择 JSON" : "选择 JSON 文件"}
          </Button>
          <span className="min-w-0 truncate text-sm text-muted-foreground" title={file?.name}>
            {file?.name ?? "尚未选择文件"}
          </span>
          {validateState.isLoading ? <span className="text-sm">正在校验</span> : null}
        </div>
      </section>

      {fileError ? (
        <Alert variant="destructive">
          <AlertTitle>文件无法读取</AlertTitle>
          <AlertDescription>{fileError}</AlertDescription>
        </Alert>
      ) : null}
      {requestError ? (
        <Alert variant="destructive">
          <AlertTitle>请求失败</AlertTitle>
          <AlertDescription>{requestError}</AlertDescription>
        </Alert>
      ) : null}
      {validation ? <ValidationSummary validation={validation} /> : null}

      <div className="flex justify-end">
        <Button type="button" disabled={!canImport} onClick={confirmImport}>
          <Upload aria-hidden="true" />
          {importState.isLoading ? "正在导入" : "确认导入"}
        </Button>
      </div>

      <WordUnsavedChangesDialog
        blocker={blocker}
        title="离开批量导入？"
        description="当前文件和校验结果将会丢失。"
      />
    </div>
  );
}

export default WordBatchImport;
