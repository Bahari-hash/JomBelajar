import { useRef, useState } from "react";
import { ArrowLeft, Download, FileJson, Upload } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  PAPER_BATCH_EXAMPLE_URL,
  readPaperBatchFile,
} from "@/features/papers/paperBatchFile.js";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useImportPaperBatchMutation,
  useValidatePaperBatchMutation,
} from "@/services/papersApi.js";

const SUMMARY = [
  ["paperCount", "试卷"],
  ["questionCount", "题目"],
  ["dictationQuestionCount", "听写题"],
  ["dictationBlankCount", "听写空"],
  ["categoryReferenceCount", "分类引用"],
  ["matchedCategoryReferenceCount", "已匹配分类"],
  ["audioReferenceCount", "音频引用"],
  ["matchedAudioReferenceCount", "已匹配音频"],
];

function Preview({ validation }) {
  return (
    <div className="space-y-5">
      <Alert variant={validation.isValid ? "default" : "destructive"}>
        <AlertTitle>
          {validation.isValid ? "校验通过" : "校验未通过"}
        </AlertTitle>
        <AlertDescription>
          {validation.isValid
            ? "全部试卷可以作为草稿整批导入。"
            : `发现 ${validation.errors.length} 个错误，本批次不会写入任何试卷。`}
        </AlertDescription>
      </Alert>
      <section className="space-y-3 border-y py-4">
        <h2 className="text-sm font-semibold">批次统计</h2>
        <dl className="grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          {SUMMARY.map(([key, label]) => (
            <div key={key} className="flex justify-between gap-4">
              <dt className="text-muted-foreground">{label}</dt>
              <dd className="font-medium tabular-nums">
                {validation.summary[key]}
              </dd>
            </div>
          ))}
        </dl>
      </section>
      {validation.errors.length ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold">校验错误</h2>
          <div className="overflow-x-auto border-y">
            <table className="w-full min-w-3xl text-left text-sm">
              <thead className="bg-muted/50 text-xs text-muted-foreground">
                <tr>
                  <th className="px-3 py-2">试卷</th>
                  <th className="px-3 py-2">题目</th>
                  <th className="px-3 py-2">字段</th>
                  <th className="px-3 py-2">错误</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {validation.errors.map((error, index) => (
                  <tr key={`${error.field}-${index}`}>
                    <td className="px-3 py-2">
                      {error.paperIndex === null
                        ? "批次"
                        : error.paperIndex + 1}
                    </td>
                    <td className="px-3 py-2">
                      {error.questionIndex === null
                        ? "-"
                        : error.questionIndex + 1}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs">
                      {error.field}
                    </td>
                    <td className="px-3 py-2">{error.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
      <section className="space-y-3">
        <h2 className="text-sm font-semibold">试卷预览</h2>
        <div className="overflow-x-auto border-y">
          <table className="w-full min-w-3xl text-left text-sm">
            <thead className="bg-muted/50 text-xs text-muted-foreground">
              <tr>
                <th className="px-3 py-2">序号</th>
                <th className="px-3 py-2">标题</th>
                <th className="px-3 py-2">题目</th>
                <th className="px-3 py-2">分类匹配</th>
                <th className="px-3 py-2">音频匹配</th>
                <th className="px-3 py-2">状态</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {validation.papers.map((paper) => (
                <tr key={paper.paperIndex}>
                  <td className="px-3 py-2">{paper.paperIndex + 1}</td>
                  <td className="px-3 py-2 font-medium">
                    {paper.title || "未命名"}
                  </td>
                  <td className="px-3 py-2">{paper.questionCount}</td>
                  <td className="px-3 py-2">
                    {paper.matchedCategoryCount} / {paper.categoryNames.length}
                  </td>
                  <td className="px-3 py-2">
                    {paper.matchedAudioCount} / {paper.audioFileNames.length}
                  </td>
                  <td className="px-3 py-2">
                    {paper.isValid ? "可导入" : "有错误"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

export default function PaperBatchImport() {
  useAdminPage("批量导入试卷");
  const inputRef = useRef(null);
  const navigate = useNavigate();
  const [file, setFile] = useState(null);
  const [payload, setPayload] = useState(null);
  const [validation, setValidation] = useState(null);
  const [error, setError] = useState(null);
  const [validateBatch, validateState] = useValidatePaperBatchMutation();
  const [importBatch, importState] = useImportPaperBatchMutation();
  const pending = validateState.isLoading || importState.isLoading;
  const selectFile = async (nextFile) => {
    setFile(nextFile);
    setPayload(null);
    setValidation(null);
    setError(null);
    if (!nextFile) return;
    try {
      const nextPayload = await readPaperBatchFile(nextFile);
      setPayload(nextPayload);
      setValidation(await validateBatch(nextPayload).unwrap());
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : getErrorMessage(reason, "批量校验失败，请重试。"),
      );
    }
  };
  const confirmImport = async () => {
    if (!payload || !validation?.isValid || pending) return;
    setError(null);
    try {
      const result = await importBatch(payload).unwrap();
      navigate("/papers", {
        replace: true,
        state: { notice: `已批量导入 ${result.importedCount} 份草稿试卷。` },
      });
    } catch (reason) {
      if (reason?.status === 422 && reason.data) setValidation(reason.data);
      else setError(getErrorMessage(reason, "批量导入失败，请重试。"));
    }
  };
  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">试卷管理</p>
          <h1 className="mt-1 text-2xl font-semibold">批量导入试卷</h1>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link to="/papers">
              <ArrowLeft />
              返回列表
            </Link>
          </Button>
          <Button variant="outline" asChild>
            <a
              href={PAPER_BATCH_EXAMPLE_URL}
              download="tiny-lang-paper-import-example.json"
            >
              <Download />
              下载示例 JSON
            </a>
          </Button>
        </div>
      </header>
      <section className="space-y-3 border-y py-4">
        <h2 className="text-sm font-semibold">JSON 文件</h2>
        <p className="text-xs text-muted-foreground">
          服务端会校验整个批次；任意一项失败时不会写入任何试卷。
        </p>
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
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={pending}
            onClick={() => inputRef.current?.click()}
          >
            <FileJson />
            {file ? "重新选择 JSON" : "选择 JSON 文件"}
          </Button>
          <span className="truncate text-sm text-muted-foreground">
            {file?.name ?? "尚未选择文件"}
          </span>
        </div>
      </section>
      {error ? (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
      {validation ? <Preview validation={validation} /> : null}
      <div className="flex justify-end">
        <Button
          type="button"
          disabled={!payload || !validation?.isValid || pending}
          onClick={confirmImport}
        >
          <Upload />
          {importState.isLoading ? "正在导入" : "确认整批导入"}
        </Button>
      </div>
    </div>
  );
}
