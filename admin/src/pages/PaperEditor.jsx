import { cloneElement, useEffect, useId, useRef, useState } from "react";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  Eye,
  Plus,
  Save,
  Send,
  Trash2,
} from "lucide-react";
import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group.jsx";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Switch } from "@/components/ui/switch.jsx";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import {
  PAPER_QUESTION_TYPE_OPTIONS,
  getPaperQuestionTypeLabel,
  getPaperStatusLabel,
  isPaperEditable,
} from "@/constants/paperStatus.js";
import { PaperActionDialog } from "@/features/papers/PaperActionDialog.jsx";
import { PaperTagInput } from "@/features/papers/PaperTagInput.jsx";
import { PaperUnsavedChangesDialog } from "@/features/papers/PaperUnsavedChangesDialog.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { useUnsavedChanges } from "@/hooks/useUnsavedChanges.js";
import { formatDateTime } from "@/lib/dateTime.js";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useCreatePaperMutation,
  useGetAdminPaperQuery,
  usePublishPaperMutation,
  useUpdatePaperMutation,
  useValidatePaperMutation,
} from "@/services/papersApi.js";

let draftSequence = 0;
const draftKey = (type) => `${type}-draft-${Date.now()}-${draftSequence++}`;
const TYPE_RESET_COPY = {
  SingleChoice: "将只保留单选题选项，清除判断答案和填空答案。",
  TrueFalse: "将只保留判断题答案，清除单选选项和填空答案。",
  FillBlank: "将只保留填空可接受答案，清除单选选项和判断答案。",
};

const emptyForm = () => ({
  title: "",
  description: "",
  instructions: "",
  languageTag: "ms",
  tags: [],
  passingScore: 0,
  concurrencyStamp: null,
  status: "Draft",
  attemptCount: 0,
  questions: [],
});

function createQuestion(type = "SingleChoice") {
  return {
    _key: draftKey("question"),
    id: null,
    type,
    prompt: "",
    explanation: "",
    points: 1,
    correctBoolean: null,
    fillBlankCaseSensitive: false,
    options: [],
    acceptedAnswers: [],
  };
}

function createOption() {
  return {
    _key: draftKey("option"),
    id: null,
    text: "",
    isCorrect: false,
  };
}

function createAnswer() {
  return {
    _key: draftKey("answer"),
    id: null,
    text: "",
  };
}

function formFromPaper(paper) {
  return {
    title: paper.title,
    description: paper.description ?? "",
    instructions: paper.instructions ?? "",
    languageTag: paper.languageTag,
    tags: paper.tags,
    passingScore: paper.passingScore,
    concurrencyStamp: paper.concurrencyStamp,
    status: paper.status,
    attemptCount: paper.attemptCount,
    questions: paper.questions.map((question) => ({
      _key: question.id,
      id: question.id,
      type: question.type,
      prompt: question.prompt,
      explanation: question.explanation ?? "",
      points: question.points,
      correctBoolean: question.correctBoolean,
      fillBlankCaseSensitive: question.fillBlankCaseSensitive,
      options: question.options.map((option) => ({
        _key: option.id,
        id: option.id,
        text: option.text,
        isCorrect: option.isCorrect,
      })),
      acceptedAnswers: question.acceptedAnswers.map((answer) => ({
        _key: answer.id,
        id: answer.id,
        text: answer.text,
      })),
    })),
  };
}

function compactText(value) {
  const text = value.trim();
  return text.length ? text : null;
}

function payloadFromForm(form, includeStamp) {
  const payload = {
    title: form.title,
    description: compactText(form.description),
    instructions: compactText(form.instructions),
    languageTag: form.languageTag,
    tags: form.tags,
    passingScore: Number(form.passingScore) || 0,
    questions: form.questions.map((question, questionIndex) => ({
      id: question.id,
      type: question.type,
      prompt: question.prompt,
      explanation: compactText(question.explanation),
      points: Number(question.points) || 0,
      sortOrder: questionIndex,
      correctBoolean:
        question.type === "TrueFalse" ? question.correctBoolean : null,
      fillBlankCaseSensitive:
        question.type === "FillBlank"
          ? Boolean(question.fillBlankCaseSensitive)
          : false,
      options:
        question.type === "SingleChoice"
          ? question.options.map((option, optionIndex) => ({
              id: option.id,
              text: option.text,
              isCorrect: Boolean(option.isCorrect),
              sortOrder: optionIndex,
            }))
          : [],
      acceptedAnswers:
        question.type === "FillBlank"
          ? question.acceptedAnswers.map((answer, answerIndex) => ({
              id: answer.id,
              text: answer.text,
              sortOrder: answerIndex,
            }))
          : [],
    })),
  };
  if (includeStamp) payload.concurrencyStamp = form.concurrencyStamp;
  return payload;
}

function compareForm(form) {
  return JSON.stringify(payloadFromForm(form, Boolean(form.concurrencyStamp)));
}

function totalScore(form) {
  return form.questions.reduce(
    (sum, question) => sum + (Number(question.points) || 0),
    0,
  );
}

function move(values, index, offset) {
  const next = [...values];
  const [item] = next.splice(index, 1);
  next.splice(index + offset, 0, item);
  return next;
}

function fieldId(field) {
  return `paper-field-${field.replace(/[^a-zA-Z0-9]+/g, "-")}`;
}

function firstIssueField(issues) {
  return issues.find((issue) => issue.field)?.field ?? null;
}

function collectIssueMap(issues) {
  return Object.fromEntries(
    issues.map((issue) => [issue.field, [issue.message]]),
  );
}

function normalizeFieldPath(value) {
  return value.replace(
    /(^|\.)([A-Z])/g,
    (_match, prefix, letter) => `${prefix}${letter.toLowerCase()}`,
  );
}

function basicValidate(form) {
  const errors = {};
  if (!form.title.trim()) errors.title = ["请输入试卷标题。"];
  if (form.title.length > 200) errors.title = ["标题不能超过 200 个字符。"];
  if (!form.languageTag.trim()) errors.languageTag = ["请输入语言标签。"];
  if (form.languageTag.length > 35)
    errors.languageTag = ["语言标签不能超过 35 个字符。"];
  if (form.description.length > 2000)
    errors.description = ["说明不能超过 2,000 个字符。"];
  if (form.instructions.length > 5000)
    errors.instructions = ["答题说明不能超过 5,000 个字符。"];
  const score = Number(form.passingScore);
  if (!Number.isInteger(score) || score < 0 || score > totalScore(form))
    errors.passingScore = ["及格分必须是不超过总分的非负整数。"];
  form.questions.forEach((question, questionIndex) => {
    const prefix = `questions[${questionIndex}]`;
    if (!question.prompt.trim()) errors[`${prefix}.prompt`] = ["请输入题干。"];
    if (question.prompt.length > 5000)
      errors[`${prefix}.prompt`] = ["题干不能超过 5,000 个字符。"];
    if (question.explanation.length > 5000)
      errors[`${prefix}.explanation`] = ["解析不能超过 5,000 个字符。"];
    const points = Number(question.points);
    if (!Number.isInteger(points) || points < 1 || points > 100)
      errors[`${prefix}.points`] = ["分值必须是 1-100 的整数。"];
    question.options.forEach((option, optionIndex) => {
      const field = `${prefix}.options[${optionIndex}].text`;
      if (!option.text.trim()) errors[field] = ["请输入选项文本。"];
      if (option.text.length > 2000)
        errors[field] = ["选项不能超过 2,000 个字符。"];
    });
    question.acceptedAnswers.forEach((answer, answerIndex) => {
      const field = `${prefix}.acceptedAnswers[${answerIndex}].text`;
      if (!answer.text.trim()) errors[field] = ["请输入可接受答案。"];
      if (answer.text.length > 1000)
        errors[field] = ["答案不能超过 1,000 个字符。"];
    });
  });
  return errors;
}

function PaperEditor() {
  const { paperId } = useParams();
  const isNew = !paperId;
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = searchParams.get("tab") === "preview" ? "preview" : "edit";
  const [form, setForm] = useState(emptyForm);
  const [baseline, setBaseline] = useState(() => compareForm(emptyForm()));
  const [formError, setFormError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [validationIssues, setValidationIssues] = useState([]);
  const [publishConfirmation, setPublishConfirmation] = useState(null);
  const [typeChange, setTypeChange] = useState(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const [statusAction, setStatusAction] = useState(null);
  const allowNavigationRef = useRef(false);
  const {
    data: paper,
    error: loadError,
    isLoading,
    refetch,
  } = useGetAdminPaperQuery(paperId, { skip: isNew });
  const [createPaper, createState] = useCreatePaperMutation();
  const [updatePaper, updateState] = useUpdatePaperMutation();
  const [validatePaper, validateState] = useValidatePaperMutation();
  const pending =
    createState.isLoading || updateState.isLoading || validateState.isLoading;
  const dirty = compareForm(form) !== baseline;
  const readOnly =
    !isNew &&
    paper &&
    !isPaperEditable({ status: form.status, attemptCount: form.attemptCount });
  const pageTitle = isNew ? "新建试卷" : form.title || "试卷详情";
  const blocker = useUnsavedChanges(dirty || pending, allowNavigationRef);
  const issueErrors = collectIssueMap(validationIssues);

  useAdminPage(pageTitle, pageTitle);

  useEffect(() => {
    if (
      searchParams.has("tab") &&
      !["edit", "preview"].includes(searchParams.get("tab"))
    ) {
      const next = new URLSearchParams(searchParams);
      next.set("tab", "edit");
      setSearchParams(next, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    if (paper) applyPaper(paper);
  }, [paper]);

  const applyPaper = (value) => {
    const next = formFromPaper(value);
    setForm(next);
    setBaseline(compareForm(next));
    setValidationIssues([]);
    setFormError(null);
  };

  const fieldError = (field) => {
    const responseError = Object.entries(formError?.fieldErrors ?? {}).find(
      ([value]) => normalizeFieldPath(value) === field,
    )?.[1]?.[0];
    return responseError ?? issueErrors[field]?.[0];
  };

  const updateQuestion = (questionKey, updater) => {
    setForm((current) => ({
      ...current,
      questions: current.questions.map((question) =>
        question._key === questionKey ? updater(question) : question,
      ),
    }));
    setFormError(null);
    setValidationIssues([]);
  };

  const focusIssue = (field) => {
    window.setTimeout(() => {
      const control =
        document.getElementById(fieldId(field)) ??
        document.querySelector(`[data-field-path="${field}"]`);
      control?.focus?.();
      control?.scrollIntoView?.({ block: "center", behavior: "smooth" });
    }, 0);
  };

  const handleSave = async (event) => {
    event.preventDefault();
    if (pending || readOnly) return;
    const clientErrors = basicValidate(form);
    if (Object.keys(clientErrors).length > 0) {
      setFormError({ fieldErrors: clientErrors });
      focusIssue(Object.keys(clientErrors)[0]);
      return;
    }
    setFormError(null);
    setNotice(null);
    setValidationIssues([]);
    try {
      const payload = payloadFromForm(form, !isNew);
      const saved = isNew
        ? await createPaper(payload).unwrap()
        : await updatePaper({ paperId, ...payload }).unwrap();
      applyPaper(saved);
      setNotice("试卷已保存。");
      if (isNew) {
        allowNavigationRef.current = true;
        navigate(`/papers/${saved.id}`, { replace: true });
      }
    } catch (error) {
      setFormError(error);
      if (error.errorCode === "PaperConcurrencyConflict")
        setConcurrencyConflict(true);
    }
  };

  const handleValidateForPublish = async () => {
    if (dirty) {
      setFormError({
        detail: "请先保存当前修改，再执行发布检查。",
        fieldErrors: {},
      });
      return;
    }
    if (!form.concurrencyStamp || pending) return;
    setFormError(null);
    setValidationIssues([]);
    try {
      const result = await validatePaper({
        paperId,
        concurrencyStamp: form.concurrencyStamp,
      }).unwrap();
      if (!result.isValid) {
        setValidationIssues(result.issues);
        const field = firstIssueField(result.issues);
        if (field) focusIssue(field);
        return;
      }
      setPublishConfirmation({
        paperId,
        concurrencyStamp: form.concurrencyStamp,
      });
    } catch (error) {
      setFormError(error);
      if (error.errorCode === "PaperConcurrencyConflict")
        setConcurrencyConflict(true);
    }
  };

  const handleTypeChange = ({ questionKey, nextType }) => {
    updateQuestion(questionKey, (question) => ({
      ...question,
      type: nextType,
      correctBoolean: null,
      fillBlankCaseSensitive:
        nextType === "FillBlank" ? question.fillBlankCaseSensitive : false,
      options: [],
      acceptedAnswers: [],
    }));
  };

  const changeTab = (value) => {
    const next = new URLSearchParams(searchParams);
    if (value === "edit") next.delete("tab");
    else next.set("tab", value);
    setSearchParams(next, { replace: true });
  };

  if (isLoading)
    return (
      <div className="space-y-4" role="status" aria-label="正在加载试卷">
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    );

  if (loadError)
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {loadError.status === 404
            ? "试卷不存在"
            : loadError.status === 403
              ? "无权查看试卷"
              : "试卷加载失败"}
        </AlertTitle>
        <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <span>{getErrorMessage(loadError)}</span>
          <Button asChild variant="outline" size="sm">
            <Link to="/papers">
              <ArrowLeft aria-hidden="true" />
              返回试卷列表
            </Link>
          </Button>
        </AlertDescription>
      </Alert>
    );

  return (
    <form onSubmit={handleSave} className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">
            {isNew
              ? "创建草稿"
              : readOnly
                ? "只读试卷"
                : getPaperStatusLabel(form.status)}
          </p>
          <h1 className="mt-1 max-w-3xl truncate text-2xl font-semibold">
            {pageTitle}
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild type="button" variant="outline">
            <Link to="/papers">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
          {!isNew ? (
            <Button
              type="button"
              variant="outline"
              onClick={() => changeTab("preview")}
            >
              <Eye aria-hidden="true" />
              预览
            </Button>
          ) : null}
          {!isNew &&
          (form.status === "Draft" || form.status === "Unpublished") ? (
            <Button
              type="button"
              variant="outline"
              disabled={pending}
              onClick={handleValidateForPublish}
            >
              <Send aria-hidden="true" />
              {validateState.isLoading ? "正在检查" : "发布"}
            </Button>
          ) : null}
          <Button type="submit" disabled={pending || readOnly || !dirty}>
            <Save aria-hidden="true" />
            {pending ? "正在保存" : dirty ? "保存" : "已保存"}
          </Button>
        </div>
      </header>
      {!isNew ? (
        <Tabs value={activeTab} onValueChange={changeTab}>
          <TabsList>
            <TabsTrigger value="edit">编辑</TabsTrigger>
            <TabsTrigger value="preview">预览</TabsTrigger>
          </TabsList>
        </Tabs>
      ) : null}
      {readOnly ? (
        <Alert>
          <AlertTitle>
            {form.status === "Archived"
              ? "试卷已归档"
              : form.attemptCount > 0
                ? "内容已锁定"
                : "试卷已发布"}
          </AlertTitle>
          <AlertDescription>
            {form.status === "Archived"
              ? "归档是终态，只能查看。"
              : form.attemptCount > 0
                ? `已有 ${form.attemptCount} 次测验历史，题目和答案不能再修改。`
                : "请先下架试卷，再继续编辑内容。"}
          </AlertDescription>
        </Alert>
      ) : null}
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      {formError ? (
        <Alert variant="destructive" aria-live="assertive">
          <AlertDescription>
            {formError.errorCode === "PaperConcurrencyConflict"
              ? "试卷已被其他管理员修改。你的输入仍然保留，请选择是否重新加载。"
              : getErrorMessage(formError, "请修正表单后重试。")}
          </AlertDescription>
        </Alert>
      ) : null}
      {validationIssues.length ? (
        <Alert variant="destructive" aria-live="assertive">
          <AlertTitle>发布检查未通过</AlertTitle>
          <AlertDescription>
            已定位到首个问题。请修正 {validationIssues.length}{" "}
            项服务端检查结果后重试。
          </AlertDescription>
        </Alert>
      ) : null}
      {activeTab === "preview" ? (
        <PaperPreview form={form} />
      ) : (
        <>
          <PaperBasics
            form={form}
            readOnly={readOnly}
            fieldError={fieldError}
            onChange={(patchValue) => {
              setForm({ ...form, ...patchValue });
              setFormError(null);
              setValidationIssues([]);
            }}
          />
          <QuestionEditor
            form={form}
            readOnly={readOnly}
            fieldError={fieldError}
            onChange={setForm}
            updateQuestion={updateQuestion}
            onTypeChange={(questionKey, nextType) =>
              setTypeChange({ questionKey, nextType })
            }
          />
        </>
      )}
      {!isNew && paper ? (
        <section className="grid gap-3 border-y py-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Info label="状态" value={getPaperStatusLabel(form.status)} />
          <Info label="测验次数" value={String(form.attemptCount)} />
          <Info
            label="创建者"
            value={paper.createdBy.nickname ?? paper.createdBy.id}
          />
          <Info
            label="最后修改"
            value={paper.lastEditor.nickname ?? paper.lastEditor.id}
          />
          <Info label="创建时间" value={formatDateTime(paper.createdAt)} />
          <Info label="更新时间" value={formatDateTime(paper.updatedAt)} />
          <Info label="发布时间" value={formatDateTime(paper.publishedAt)} />
          <Info label="归档时间" value={formatDateTime(paper.archivedAt)} />
        </section>
      ) : null}
      <div className="sticky bottom-3 flex flex-wrap justify-end gap-2">
        {!isNew && form.status === "Published" ? (
          <Button
            type="button"
            variant="outline"
            disabled={pending}
            onClick={() => setStatusAction("unpublish")}
          >
            下架
          </Button>
        ) : null}
        {!isNew &&
        (form.status === "Draft" || form.status === "Unpublished") ? (
          <>
            <Button
              type="button"
              variant="outline"
              disabled={pending || dirty}
              onClick={() => setStatusAction("archive")}
            >
              归档
            </Button>
            {form.attemptCount === 0 ? (
              <Button
                type="button"
                variant="destructive"
                disabled={pending || dirty}
                onClick={() => setStatusAction("delete")}
              >
                永久删除
              </Button>
            ) : null}
          </>
        ) : null}
        <Button type="submit" disabled={pending || readOnly || !dirty}>
          <Save aria-hidden="true" />
          {pending ? "正在保存" : dirty ? "保存修改" : "已保存"}
        </Button>
      </div>
      <PaperUnsavedChangesDialog blocker={blocker} />
      {typeChange ? (
        <TypeChangeDialog
          nextType={typeChange.nextType}
          onClose={() => setTypeChange(null)}
          onConfirm={() => {
            handleTypeChange(typeChange);
            setTypeChange(null);
          }}
        />
      ) : null}
      {publishConfirmation ? (
        <PublishDialog
          form={form}
          argument={publishConfirmation}
          onClose={() => setPublishConfirmation(null)}
          onSaved={applyPaper}
          onError={(error) => {
            setFormError(error);
            if (error.errorCode === "PaperConcurrencyConflict")
              setConcurrencyConflict(true);
          }}
        />
      ) : null}
      {concurrencyConflict ? (
        <AlertDialog
          open
          onOpenChange={(open) => !open && setConcurrencyConflict(false)}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>服务端内容已更新</AlertDialogTitle>
              <AlertDialogDescription>
                当前输入已保留。重新加载会永久丢弃本地修改，也可以关闭此窗口继续保留本地内容。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel onClick={() => setConcurrencyConflict(false)}>
                保留本地内容
              </AlertDialogCancel>
              <Button
                type="button"
                variant="destructive"
                onClick={async () => {
                  const result = await refetch();
                  if (result.data) applyPaper(result.data);
                  setConcurrencyConflict(false);
                }}
              >
                丢弃并重新加载
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      ) : null}
      {statusAction && paper ? (
        <PaperActionDialog
          action={statusAction}
          paper={{
            ...paper,
            title: form.title,
            status: form.status,
            attemptCount: form.attemptCount,
            concurrencyStamp: form.concurrencyStamp,
          }}
          onClose={() => setStatusAction(null)}
          onDone={(message, saved) => {
            setNotice(message);
            if (saved) applyPaper(saved);
            else {
              allowNavigationRef.current = true;
              navigate("/papers", { replace: true });
            }
          }}
          onConflict={async () => {
            const result = await refetch();
            if (result.data && !dirty) applyPaper(result.data);
          }}
        />
      ) : null}
    </form>
  );
}

function PaperBasics({ form, readOnly, fieldError, onChange }) {
  return (
    <section className="flex flex-col gap-4">
      <div className="w-full sm:w-1/2">
        <Field label="标题" required error={fieldError("title")} path="title">
          <Input
            value={form.title}
            maxLength={200}
            disabled={readOnly}
            onChange={(event) => onChange({ title: event.target.value })}
          />
        </Field>
        {/* <Field
        label="语言标签"
        required
        error={fieldError("languageTag")}
        path="languageTag"
      >
        <Input
          value={form.languageTag}
          maxLength={35}
          disabled={readOnly}
          placeholder="例如 en"
          onChange={(event) => onChange({ languageTag: event.target.value })}
        />
      </Field> */}
      </div>

      <div className="w-full sm:w-1/2">
        <Field label="标签" error={fieldError("tags")} path="tags">
          <PaperTagInput
            value={form.tags}
            disabled={readOnly}
            onChange={(tags) => onChange({ tags })}
          />
        </Field>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Field
          label="试卷说明"
          error={fieldError("description")}
          path="description"
        >
          <Textarea
            value={form.description}
            maxLength={2000}
            rows={3}
            disabled={readOnly}
            onChange={(event) => onChange({ description: event.target.value })}
          />
        </Field>
        <Field
          label="答题说明"
          error={fieldError("instructions")}
          path="instructions"
        >
          <Textarea
            value={form.instructions}
            maxLength={5000}
            rows={3}
            disabled={readOnly}
            onChange={(event) => onChange({ instructions: event.target.value })}
          />
        </Field>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Field
          label="及格分"
          required
          error={fieldError("passingScore")}
          path="passingScore"
        >
          <Input
            type="number"
            min="0"
            value={form.passingScore}
            disabled={readOnly}
            onChange={(event) =>
              onChange({ passingScore: Number(event.target.value) })
            }
          />
        </Field>
        <div className="space-y-1.5">
          <Label>总分</Label>
          <div className="flex h-8 items-center rounded-lg border px-2.5 text-sm">
            {totalScore(form)} 分，由题目分值自动汇总
          </div>
        </div>
      </div>
    </section>
  );
}

function QuestionEditor({
  form,
  readOnly,
  fieldError,
  onChange,
  updateQuestion,
  onTypeChange,
}) {
  return (
    <section className="space-y-4" data-field-path="questions" tabIndex={-1}>
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">题目</h2>
          <p className="text-sm text-muted-foreground">
            草稿可先保存不完整答案；发布完整性由服务端检查。
          </p>
        </div>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={readOnly || form.questions.length >= 200}
          onClick={() =>
            onChange({
              ...form,
              questions: [...form.questions, createQuestion("SingleChoice")],
            })
          }
        >
          <Plus aria-hidden="true" />
          添加题目
        </Button>
      </div>
      {form.questions.length === 0 ? (
        <p className="border-y py-8 text-center text-sm text-muted-foreground">
          尚未添加题目。可以保存空草稿，但发布前至少需要一道题。
        </p>
      ) : (
        form.questions.map((question, questionIndex) => (
          <QuestionBlock
            key={question._key}
            form={form}
            question={question}
            questionIndex={questionIndex}
            readOnly={readOnly}
            fieldError={fieldError}
            updateQuestion={updateQuestion}
            onChange={onChange}
            onTypeChange={onTypeChange}
          />
        ))
      )}
    </section>
  );
}

function QuestionBlock({
  form,
  question,
  questionIndex,
  readOnly,
  fieldError,
  updateQuestion,
  onChange,
  onTypeChange,
}) {
  const prefix = `questions[${questionIndex}]`;
  return (
    <section className="space-y-4 rounded-lg border p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2">
          <h3 className="font-medium">题目 {questionIndex + 1}</h3>
          <Badge variant="outline">
            {getPaperQuestionTypeLabel(question.type)}
          </Badge>
        </div>
        <OrderButtons
          label={`题目 ${questionIndex + 1}`}
          index={questionIndex}
          count={form.questions.length}
          disabled={readOnly}
          onMove={(offset) =>
            onChange({
              ...form,
              questions: move(form.questions, questionIndex, offset),
            })
          }
          onDelete={() =>
            onChange({
              ...form,
              questions: form.questions.filter(
                (value) => value._key !== question._key,
              ),
            })
          }
        />
      </div>
      <div className="grid gap-3 md:grid-cols-[12rem_8rem_1fr]">
        <div className="space-y-1.5">
          <Label id={`${fieldId(`${prefix}.type`)}-label`}>题型</Label>
          <Select
            value={question.type}
            disabled={readOnly}
            onValueChange={(nextType) =>
              nextType === question.type
                ? undefined
                : onTypeChange(question._key, nextType)
            }
          >
            <SelectTrigger
              id={fieldId(`${prefix}.type`)}
              aria-labelledby={`${fieldId(`${prefix}.type`)}-label`}
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {PAPER_QUESTION_TYPE_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <Field
          label="分值"
          required
          error={fieldError(`${prefix}.points`)}
          path={`${prefix}.points`}
        >
          <Input
            type="number"
            min="1"
            max="100"
            value={question.points}
            disabled={readOnly}
            onChange={(event) =>
              updateQuestion(question._key, (value) => ({
                ...value,
                points: Number(event.target.value),
              }))
            }
          />
        </Field>
      </div>
      <Field
        label="题干"
        required
        error={fieldError(`${prefix}.prompt`)}
        path={`${prefix}.prompt`}
      >
        <Textarea
          value={question.prompt}
          maxLength={5000}
          disabled={readOnly}
          rows={3}
          onChange={(event) =>
            updateQuestion(question._key, (value) => ({
              ...value,
              prompt: event.target.value,
            }))
          }
        />
      </Field>
      <Field
        label="解析说明"
        error={fieldError(`${prefix}.explanation`)}
        path={`${prefix}.explanation`}
      >
        <Textarea
          value={question.explanation}
          maxLength={5000}
          disabled={readOnly}
          rows={2}
          onChange={(event) =>
            updateQuestion(question._key, (value) => ({
              ...value,
              explanation: event.target.value,
            }))
          }
        />
      </Field>
      {question.type === "SingleChoice" ? (
        <SingleChoiceEditor
          question={question}
          questionIndex={questionIndex}
          readOnly={readOnly}
          fieldError={fieldError}
          updateQuestion={updateQuestion}
        />
      ) : null}
      {question.type === "TrueFalse" ? (
        <TrueFalseEditor
          question={question}
          questionIndex={questionIndex}
          readOnly={readOnly}
          fieldError={fieldError}
          updateQuestion={updateQuestion}
        />
      ) : null}
      {question.type === "FillBlank" ? (
        <FillBlankEditor
          question={question}
          questionIndex={questionIndex}
          readOnly={readOnly}
          fieldError={fieldError}
          updateQuestion={updateQuestion}
        />
      ) : null}
    </section>
  );
}

function SingleChoiceEditor({
  question,
  questionIndex,
  readOnly,
  fieldError,
  updateQuestion,
}) {
  const prefix = `questions[${questionIndex}]`;
  const correctKey =
    question.options.find((option) => option.isCorrect)?._key ?? "";
  return (
    <section
      className="space-y-3 border-t pt-4"
      data-field-path={`${prefix}.options`}
      tabIndex={-1}
    >
      <div className="flex items-center justify-between gap-3">
        <div>
          <h4 className="text-sm font-medium">选项</h4>
          <p className="text-xs text-muted-foreground">
            发布前需要 2-10 个选项，并且恰好选择一个正确答案。
          </p>
        </div>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={readOnly || question.options.length >= 10}
          onClick={() =>
            updateQuestion(question._key, (value) => ({
              ...value,
              options: [...value.options, createOption()],
            }))
          }
        >
          <Plus aria-hidden="true" />
          添加选项
        </Button>
      </div>
      {fieldError(`${prefix}.options`) ? (
        <p className="text-xs text-destructive" role="alert">
          {fieldError(`${prefix}.options`)}
        </p>
      ) : null}
      <RadioGroup
        value={correctKey}
        onValueChange={(key) =>
          updateQuestion(question._key, (value) => ({
            ...value,
            options: value.options.map((option) => ({
              ...option,
              isCorrect: option._key === key,
            })),
          }))
        }
      >
        {question.options.map((option, optionIndex) => {
          const field = `${prefix}.options[${optionIndex}].text`;
          return (
            <div
              key={option._key}
              className="grid gap-2 border-t pt-3 first:border-t-0 first:pt-0 md:grid-cols-[auto_1fr_auto]"
            >
              <div className="flex items-center gap-2 pt-1.5">
                <RadioGroupItem
                  value={option._key}
                  id={`correct-${option._key}`}
                  disabled={readOnly}
                  aria-label={`选项 ${optionIndex + 1} 为正确答案`}
                />
                <Label htmlFor={`correct-${option._key}`} className="text-xs">
                  正确
                </Label>
              </div>
              <Field
                label={`选项 ${optionIndex + 1}`}
                required
                error={fieldError(field)}
                path={field}
              >
                <Input
                  value={option.text}
                  maxLength={2000}
                  disabled={readOnly}
                  onChange={(event) =>
                    updateQuestion(question._key, (value) => ({
                      ...value,
                      options: value.options.map((item) =>
                        item._key === option._key
                          ? { ...item, text: event.target.value }
                          : item,
                      ),
                    }))
                  }
                />
              </Field>
              <OrderButtons
                label={`选项 ${optionIndex + 1}`}
                index={optionIndex}
                count={question.options.length}
                disabled={readOnly}
                onMove={(offset) =>
                  updateQuestion(question._key, (value) => ({
                    ...value,
                    options: move(value.options, optionIndex, offset),
                  }))
                }
                onDelete={() =>
                  updateQuestion(question._key, (value) => ({
                    ...value,
                    options: value.options.filter(
                      (item) => item._key !== option._key,
                    ),
                  }))
                }
              />
            </div>
          );
        })}
      </RadioGroup>
    </section>
  );
}

function TrueFalseEditor({
  question,
  questionIndex,
  readOnly,
  fieldError,
  updateQuestion,
}) {
  const field = `questions[${questionIndex}].correctBoolean`;
  return (
    <section
      className="space-y-2 border-t pt-4"
      data-field-path={field}
      tabIndex={-1}
    >
      <Label id={`${fieldId(field)}-label`}>标准答案</Label>
      <RadioGroup
        className="flex flex-wrap gap-3"
        value={
          question.correctBoolean === null
            ? ""
            : String(question.correctBoolean)
        }
        onValueChange={(value) =>
          updateQuestion(question._key, (current) => ({
            ...current,
            correctBoolean: value === "true",
          }))
        }
        aria-labelledby={`${fieldId(field)}-label`}
      >
        <label className="flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
          <RadioGroupItem value="true" disabled={readOnly} />
          正确
        </label>
        <label className="flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
          <RadioGroupItem value="false" disabled={readOnly} />
          错误
        </label>
      </RadioGroup>
      {fieldError(field) ? (
        <p className="text-xs text-destructive" role="alert">
          {fieldError(field)}
        </p>
      ) : (
        <p className="text-xs text-muted-foreground">
          草稿可以暂不选择，发布前必须指定判断结果。
        </p>
      )}
    </section>
  );
}

function FillBlankEditor({
  question,
  questionIndex,
  readOnly,
  fieldError,
  updateQuestion,
}) {
  const prefix = `questions[${questionIndex}]`;
  return (
    <section
      className="space-y-3 border-t pt-4"
      data-field-path={`${prefix}.acceptedAnswers`}
      tabIndex={-1}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h4 className="text-sm font-medium">可接受答案</h4>
          <p className="text-xs text-muted-foreground">
            这些答案对应同一个空，不是多个填空。
          </p>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <Switch
            checked={question.fillBlankCaseSensitive}
            disabled={readOnly}
            onCheckedChange={(checked) =>
              updateQuestion(question._key, (value) => ({
                ...value,
                fillBlankCaseSensitive: Boolean(checked),
              }))
            }
          />
          区分大小写
        </label>
      </div>
      {fieldError(`${prefix}.acceptedAnswers`) ? (
        <p className="text-xs text-destructive" role="alert">
          {fieldError(`${prefix}.acceptedAnswers`)}
        </p>
      ) : null}
      <div className="space-y-3">
        {question.acceptedAnswers.map((answer, answerIndex) => {
          const field = `${prefix}.acceptedAnswers[${answerIndex}].text`;
          return (
            <div
              key={answer._key}
              className="grid gap-2 border-t pt-3 first:border-t-0 first:pt-0 md:grid-cols-[1fr_auto]"
            >
              <Field
                label={`答案 ${answerIndex + 1}`}
                required
                error={fieldError(field)}
                path={field}
              >
                <Input
                  value={answer.text}
                  maxLength={1000}
                  disabled={readOnly}
                  onChange={(event) =>
                    updateQuestion(question._key, (value) => ({
                      ...value,
                      acceptedAnswers: value.acceptedAnswers.map((item) =>
                        item._key === answer._key
                          ? { ...item, text: event.target.value }
                          : item,
                      ),
                    }))
                  }
                />
              </Field>
              <OrderButtons
                label={`答案 ${answerIndex + 1}`}
                index={answerIndex}
                count={question.acceptedAnswers.length}
                disabled={readOnly}
                onMove={(offset) =>
                  updateQuestion(question._key, (value) => ({
                    ...value,
                    acceptedAnswers: move(
                      value.acceptedAnswers,
                      answerIndex,
                      offset,
                    ),
                  }))
                }
                onDelete={() =>
                  updateQuestion(question._key, (value) => ({
                    ...value,
                    acceptedAnswers: value.acceptedAnswers.filter(
                      (item) => item._key !== answer._key,
                    ),
                  }))
                }
              />
            </div>
          );
        })}
      </div>
      <Button
        type="button"
        size="sm"
        variant="outline"
        disabled={readOnly || question.acceptedAnswers.length >= 20}
        onClick={() =>
          updateQuestion(question._key, (value) => ({
            ...value,
            acceptedAnswers: [...value.acceptedAnswers, createAnswer()],
          }))
        }
      >
        <Plus aria-hidden="true" />
        添加答案
      </Button>
    </section>
  );
}

function PaperPreview({ form }) {
  return (
    <section className="space-y-5">
      <div className="border-y py-5">
        <p className="text-sm text-muted-foreground">
          {form.languageTag || "未设置语言"}
        </p>
        <h2 className="mt-1 text-xl font-semibold">
          {form.title || "未命名试卷"}
        </h2>
        {form.description ? (
          <p className="mt-2 whitespace-pre-wrap text-sm">{form.description}</p>
        ) : null}
        {form.instructions ? (
          <p className="mt-3 whitespace-pre-wrap text-sm text-muted-foreground">
            {form.instructions}
          </p>
        ) : null}
        <p className="mt-3 text-sm text-muted-foreground">
          共 {form.questions.length} 题，总分 {totalScore(form)}，及格分{" "}
          {form.passingScore}
        </p>
      </div>
      {form.questions.length === 0 ? (
        <p className="border-y py-8 text-center text-sm text-muted-foreground">
          暂无题目可预览。
        </p>
      ) : (
        form.questions.map((question, index) => (
          <section
            key={question._key}
            className="space-y-3 rounded-lg border p-4"
          >
            <div className="flex items-center justify-between gap-3">
              <h3 className="font-medium">第 {index + 1} 题</h3>
              <span className="text-sm text-muted-foreground">
                {getPaperQuestionTypeLabel(question.type)} · {question.points}{" "}
                分
              </span>
            </div>
            <p className="whitespace-pre-wrap text-sm">
              {question.prompt || "未填写题干"}
            </p>
            {question.type === "SingleChoice" ? (
              <RadioGroup value="" aria-label={`第 ${index + 1} 题选项预览`}>
                {question.options.map((option, optionIndex) => (
                  <label
                    key={option._key}
                    className="flex items-start gap-2 rounded-lg border px-3 py-2 text-sm"
                  >
                    <RadioGroupItem
                      value={option._key}
                      disabled
                      aria-label={`选项 ${optionIndex + 1}`}
                    />
                    <span>{option.text || `选项 ${optionIndex + 1}`}</span>
                  </label>
                ))}
              </RadioGroup>
            ) : null}
            {question.type === "TrueFalse" ? (
              <RadioGroup
                value=""
                className="flex flex-wrap gap-3"
                aria-label={`第 ${index + 1} 题判断预览`}
              >
                <label className="flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
                  <RadioGroupItem value="true" disabled />
                  正确
                </label>
                <label className="flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
                  <RadioGroupItem value="false" disabled />
                  错误
                </label>
              </RadioGroup>
            ) : null}
            {question.type === "FillBlank" ? (
              <Input disabled aria-label={`第 ${index + 1} 题填空预览`} />
            ) : null}
            {question.explanation ? (
              <p className="whitespace-pre-wrap text-xs text-muted-foreground">
                解析说明：{question.explanation}
              </p>
            ) : null}
          </section>
        ))
      )}
    </section>
  );
}

function PublishDialog({ form, argument, onClose, onSaved, onError }) {
  const [publishPaper, state] = usePublishPaperMutation();
  const [error, setError] = useState(null);
  const handleSubmit = async () => {
    if (state.isLoading) return;
    setError(null);
    try {
      const saved = await publishPaper(argument).unwrap();
      onSaved(saved);
      onClose();
    } catch (requestError) {
      setError(requestError);
      onError(requestError);
    }
  };
  return (
    <AlertDialog
      open
      onOpenChange={(open) => !open && !state.isLoading && onClose()}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>确认发布试卷？</AlertDialogTitle>
          <AlertDialogDescription>
            “{form.title || "未命名试卷"}”发布后用户可以开始测验。
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-1 text-sm">
          <p>题目数：{form.questions.length}</p>
          <p>总分：{totalScore(form)}</p>
          <p>及格分：{form.passingScore}</p>
        </div>
        {error ? (
          <Alert variant="destructive">
            <AlertDescription>
              {error.errorCode === "PaperPublishRequirementsNotMet"
                ? "发布要求已变化，请重新执行发布检查。"
                : getErrorMessage(error)}
            </AlertDescription>
          </Alert>
        ) : null}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={state.isLoading}>取消</AlertDialogCancel>
          <Button disabled={state.isLoading} onClick={handleSubmit}>
            {state.isLoading ? "正在发布" : "确认发布"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function TypeChangeDialog({ nextType, onClose, onConfirm }) {
  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>切换题型？</AlertDialogTitle>
          <AlertDialogDescription>
            {TYPE_RESET_COPY[nextType]} 当前未保存内容仍可通过取消保留。
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel type="button" onClick={onClose}>
            取消
          </AlertDialogCancel>
          <Button type="button" variant="destructive" onClick={onConfirm}>
            确认切换
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

function Field({ id, label, required, error, path, children }) {
  const generatedId = useId();
  const controlId = id ?? (path ? fieldId(path) : generatedId);
  const errorId = `${controlId}-error`;
  return (
    <div className="space-y-1.5">
      <Label htmlFor={controlId}>
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </Label>
      {cloneElement(children, {
        id: controlId,
        "data-field-path": path,
        "aria-invalid": Boolean(error),
        "aria-describedby": error ? errorId : undefined,
      })}
      {error ? (
        <p id={errorId} className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function OrderButtons({ label, index, count, disabled, onMove, onDelete }) {
  return (
    <div className="flex gap-1">
      <Button
        type="button"
        variant="ghost"
        size="icon"
        aria-label={`上移${label}`}
        disabled={disabled || index === 0}
        onClick={() => onMove(-1)}
      >
        <ArrowUp aria-hidden="true" />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        aria-label={`下移${label}`}
        disabled={disabled || index === count - 1}
        onClick={() => onMove(1)}
      >
        <ArrowDown aria-hidden="true" />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        aria-label={`删除${label}`}
        disabled={disabled}
        onClick={onDelete}
      >
        <Trash2 aria-hidden="true" />
      </Button>
    </div>
  );
}

function Info({ label, value }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 truncate" title={value || "无"}>
        {value || "无"}
      </p>
    </div>
  );
}

export default PaperEditor;
