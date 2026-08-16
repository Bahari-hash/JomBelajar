import { cloneElement, useEffect, useId, useRef, useState } from "react";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  Plus,
  Save,
  Trash2,
  Volume2,
  Send,
  ChevronDown,
  ChevronUp,
} from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
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
import { Button } from "@/components/ui/button.jsx";
import { Checkbox } from "@/components/ui/checkbox.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import {
  PART_OF_SPEECH_OPTIONS,
  getWordStatusLabel,
} from "@/constants/wordStatus.js";
import { WordActionDialog } from "@/features/words/WordActionDialog.jsx";
import { WordAudioPicker } from "@/features/words/WordAudioPicker.jsx";
import { WordUnsavedChangesDialog } from "@/features/words/WordUnsavedChangesDialog.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { useUnsavedChanges } from "@/hooks/useUnsavedChanges.js";
import { formatDateTime } from "@/lib/dateTime.js";
import NotFound from "@/pages/NotFound.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useCreateWordMutation,
  useGetAdminWordQuery,
  useUpdateWordMutation,
} from "@/services/wordsApi.js";

let draftSequence = 0;
const GUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const draftKey = (type) => `${type}-draft-${Date.now()}-${draftSequence++}`;
const emptyForm = () => ({
  headword: "",
  senses: [],
  pronunciations: [],
});
const createSense = () => ({
  _key: draftKey("sense"),
  id: null,
  partOfSpeech: "Noun",
  definition: "",
  usageNote: "",
  examples: [],
});
const createExample = () => ({
  _key: draftKey("example"),
  id: null,
  sentence: "",
  translation: "",
  audioClipId: null,
});
const createPronunciation = () => ({
  _key: draftKey("pronunciation"),
  id: null,
  audioClipId: null,
  accentTag: "",
  ipa: "",
  isDefault: false,
});

function formFromWord(word) {
  return {
    headword: word.headword,
    senses: word.senses.map((sense) => ({
      ...sense,
      _key: sense.id,
      usageNote: sense.usageNote ?? "",
      examples: sense.examples.map((example) => ({
        ...example,
        _key: example.id,
      })),
    })),
    pronunciations: word.pronunciations.map((item) => ({
      ...item,
      _key: item.id,
      accentTag: item.accentTag ?? "",
      ipa: item.ipa ?? "",
    })),
  };
}

function toPayload(form, concurrencyStamp) {
  const payload = {
    headword: form.headword.trim(),
    senses: form.senses.map((sense, senseIndex) => ({
      ...(sense.id ? { id: sense.id } : {}),
      partOfSpeech: sense.partOfSpeech,
      definition: sense.definition.trim(),
      usageNote: sense.usageNote.trim() || null,
      sortOrder: senseIndex,
      examples: sense.examples.map((example, exampleIndex) => ({
        ...(example.id ? { id: example.id } : {}),
        sentence: example.sentence.trim(),
        translation: example.translation.trim(),
        audioClipId: example.audioClipId,
        sortOrder: exampleIndex,
      })),
    })),
    pronunciations: form.pronunciations.map((item, index) => ({
      ...(item.id ? { id: item.id } : {}),
      audioClipId: item.audioClipId,
      accentTag: item.accentTag.trim() || null,
      ipa: item.ipa.trim() || null,
      isDefault: item.isDefault,
      sortOrder: index,
    })),
  };
  return concurrencyStamp ? { ...payload, concurrencyStamp } : payload;
}

function validateForm(form) {
  const errors = {};
  if (!form.headword.trim()) errors.headword = ["请输入词头。"];
  form.senses.forEach((sense, senseIndex) => {
    if (!sense.definition.trim())
      errors[`senses[${senseIndex}].definition`] = ["请输入释义。"];
    sense.examples.forEach((example, exampleIndex) => {
      const prefix = `senses[${senseIndex}].examples[${exampleIndex}]`;
      if (!example.sentence.trim())
        errors[`${prefix}.sentence`] = ["请输入例句。"];
      if (!example.translation.trim())
        errors[`${prefix}.translation`] = ["请输入译文。"];
    });
  });
  form.pronunciations.forEach((item, index) => {
    if (!item.audioClipId)
      errors[`pronunciations[${index}].audioClipId`] = ["请选择发音音频。"];
  });
  return errors;
}

function move(items, index, offset) {
  const next = [...items];
  const target = index + offset;
  if (target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

function WordEditor() {
  const { wordId } = useParams();
  const isNew = !wordId;
  const hasValidWordId = isNew || GUID_PATTERN.test(wordId);
  useAdminPage(isNew ? "新建单词" : "单词详情");
  const navigate = useNavigate();
  const allowNavigationRef = useRef(false);
  const [form, setForm] = useState(emptyForm);
  const [baseline, setBaseline] = useState(() =>
    JSON.stringify(toPayload(emptyForm())),
  );
  const [word, setWord] = useState(null);
  const [initialized, setInitialized] = useState(isNew);
  const [fieldErrors, setFieldErrors] = useState({});
  const [formError, setFormError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [audioTarget, setAudioTarget] = useState(null);
  const [statusAction, setStatusAction] = useState(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const { data, error, isLoading, refetch } = useGetAdminWordQuery(wordId, {
    skip: isNew || !hasValidWordId,
  });
  const [createWord, createState] = useCreateWordMutation();
  const [updateWord, updateState] = useUpdateWordMutation();
  const pending = createState.isLoading || updateState.isLoading;
  const payload = toPayload(form, word?.concurrencyStamp);
  const dirty = JSON.stringify(payload) !== baseline;
  const readOnly = word?.status === "Published" || word?.status === "Archived";
  const blocker = useUnsavedChanges(dirty, allowNavigationRef);

  const [collapsedKeys, setCollapsedKeys] = useState({});
  const senseCardRefs = useRef(new Map());
  const exampleCardRefs = useRef(new Map());

  const toggleCollapse = (key) => {
    setCollapsedKeys((prev) => ({
      ...prev,
      [key]: !prev[key],
    }));
  };

  const addSense = (afterSenseKey = null) => {
    const sense = createSense();
    const senses = [...form.senses];
    const currentIndex = afterSenseKey
      ? senses.findIndex((value) => value._key === afterSenseKey)
      : senses.length - 1;
    senses.splice(currentIndex + 1, 0, sense);
    setForm({ ...form, senses });
    setCollapsedKeys((current) => ({
      ...current,
      ...Object.fromEntries(form.senses.map((value) => [value._key, true])),
      [sense._key]: false,
    }));
    window.setTimeout(() => {
      const card = senseCardRefs.current.get(sense._key);
      card?.focus?.();
      card?.scrollIntoView?.({ block: "center", behavior: "smooth" });
    }, 0);
  };

  const addExample = (senseKey, afterExampleKey = null) => {
    const example = createExample();
    const sense = form.senses.find((value) => value._key === senseKey);
    if (!sense) return;
    const examples = [...sense.examples];
    const currentIndex = afterExampleKey
      ? examples.findIndex((value) => value._key === afterExampleKey)
      : examples.length - 1;
    examples.splice(currentIndex + 1, 0, example);
    setForm({
      ...form,
      senses: form.senses.map((value) =>
        value._key === senseKey ? { ...value, examples } : value,
      ),
    });
    setCollapsedKeys((current) => ({
      ...current,
      ...Object.fromEntries(sense.examples.map((value) => [value._key, true])),
      [example._key]: false,
    }));
    window.setTimeout(() => {
      const card = exampleCardRefs.current.get(example._key);
      card?.focus?.();
      card?.scrollIntoView?.({ block: "center", behavior: "smooth" });
    }, 0);
  };
  useEffect(() => {
    allowNavigationRef.current = false;
  }, [wordId]);

  const applyWord = (saved) => {
    const nextForm = formFromWord(saved);
    setWord(saved);
    setForm(nextForm);
    setCollapsedKeys(
      Object.fromEntries(
        nextForm.senses.map((sense, index) => [sense._key, index !== 0]),
      ),
    );
    setBaseline(JSON.stringify(toPayload(nextForm, saved.concurrencyStamp)));
    setFieldErrors({});
    setFormError(null);
    setInitialized(true);
  };
  useEffect(() => {
    if (data && !initialized) applyWord(data);
  }, [data, initialized]);

  if (!hasValidWordId) return <NotFound />;

  const updateSense = (senseKey, updater) =>
    setForm((current) => ({
      ...current,
      senses: current.senses.map((sense) =>
        sense._key === senseKey ? updater(sense) : sense,
      ),
    }));
  const updatePronunciation = (itemKey, updater) =>
    setForm((current) => ({
      ...current,
      pronunciations: current.pronunciations.map((item) =>
        item._key === itemKey ? updater(item) : item,
      ),
    }));
  const fieldError = (path) =>
    Object.entries(fieldErrors).find(
      ([key]) => key.toLowerCase() === path.toLowerCase(),
    )?.[1]?.[0] ?? null;

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (pending || readOnly) return;
    const clientErrors = validateForm(form);
    setFieldErrors(clientErrors);
    setFormError(null);
    setNotice(null);
    if (Object.keys(clientErrors).length) {
      setFormError("请先修正标记的字段。");
      return;
    }
    try {
      const saved = isNew
        ? await createWord(toPayload(form)).unwrap()
        : await updateWord({
            wordId,
            ...toPayload(form, word.concurrencyStamp),
          }).unwrap();
      applyWord(saved);
      setNotice(isNew ? "单词草稿已创建。" : "单词修改已保存。");
      if (isNew) {
        allowNavigationRef.current = true;
        await navigate(`/words/${saved.id}`, { replace: true });
      }
    } catch (requestError) {
      setFieldErrors(requestError.fieldErrors ?? {});
      setFormError(getErrorMessage(requestError));
      if (requestError.errorCode === "WordConcurrencyConflict")
        setConcurrencyConflict(true);
    }
  };

  if (!isNew && isLoading)
    return (
      <div className="space-y-3" role="status" aria-label="正在加载单词详情">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  if (!isNew && error)
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {error.status === 404
            ? "单词不存在"
            : error.status === 403
              ? "无权查看单词"
              : "单词加载失败"}
        </AlertTitle>
        <AlertDescription className="mt-2 flex items-center justify-between gap-3">
          <span>
            {error.status === 404
              ? "该单词可能已被删除。"
              : getErrorMessage(error)}
          </span>
          <Button type="button" variant="outline" onClick={refetch}>
            重试
          </Button>
        </AlertDescription>
      </Alert>
    );

  return (
    <form className="space-y-6" onSubmit={handleSubmit}>
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">
            {isNew ? "新建单词" : (word?.headword ?? "单词详情")}
          </h1>
          {word ? (
            <p className="mt-1 text-sm text-muted-foreground">
              {getWordStatusLabel(word.status)} · 更新于{" "}
              {formatDateTime(word.updatedAt)}
            </p>
          ) : (
            <p className="mt-1 text-sm text-muted-foreground">
              创建后默认为草稿。
            </p>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild type="button" variant="outline">
            <Link to="/words">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
          {word ? (
            <>
              {word.status === "Published" ? (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setStatusAction("unpublish")}
                >
                  下架后编辑
                </Button>
              ) : null}
              {word.status === "Draft" || word.status === "Unpublished" ? (
                <Button
                  type="button"
                  variant="outline"
                  disabled={dirty}
                  onClick={() => setStatusAction("publish")}
                >
                  <Send aria-hidden="true" />
                  {dirty ? "先保存再发布" : "发布"}
                </Button>
              ) : null}
            </>
          ) : null}
          <Button type="submit" disabled={pending || readOnly}>
            <Save aria-hidden="true" />
            {pending ? "正在保存" : "保存"}
          </Button>
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      {formError ? (
        <Alert variant="destructive">
          <AlertDescription>{formError}</AlertDescription>
        </Alert>
      ) : null}
      {readOnly ? (
        <Alert>
          <AlertTitle>当前内容为只读</AlertTitle>
          <AlertDescription>
            {word.status === "Published"
              ? "已发布单词需要先下架才能编辑。"
              : "已归档单词不能编辑或恢复。"}
          </AlertDescription>
        </Alert>
      ) : null}
      <section className="grid gap-4 border-y py-5 sm:grid-cols-2">
        <Field
          id="word-headword"
          label="词头"
          required
          error={fieldError("headword")}
        >
          <Input
            id="word-headword"
            value={form.headword}
            maxLength={200}
            disabled={readOnly}
            aria-invalid={Boolean(fieldError("headword"))}
            onChange={(event) =>
              setForm({ ...form, headword: event.target.value })
            }
          />
        </Field>
      </section>
      <section className="space-y-4">
        <div className="flex flex-col items-stretch gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-base font-semibold">释义与例句</h2>
            <p className="text-sm text-muted-foreground">
              最多 20 个释义，每个释义最多 20 条例句。
            </p>
          </div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={readOnly || form.senses.length >= 20}
            onClick={() => addSense()}
          >
            <Plus aria-hidden="true" />
            添加释义
          </Button>
        </div>
        {form.senses.length === 0 ? (
          <p className="border-y py-8 text-center text-sm text-muted-foreground">
            尚未添加释义。草稿可以保存，但发布时服务端会校验内容完整性。
          </p>
        ) : (
          form.senses.map((sense, senseIndex) => (
            <div
              key={sense._key}
              ref={(node) => {
                if (node) senseCardRefs.current.set(sense._key, node);
                else senseCardRefs.current.delete(sense._key);
              }}
              className="overflow-hidden rounded-lg border bg-card outline-none focus-visible:ring-2 focus-visible:ring-ring"
              tabIndex={-1}
            >
              <div className="flex items-start justify-between gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <h3 className="font-medium">{`{ 释义 ${senseIndex + 1} }`}</h3>
                    <span
                      className="mt-1 truncate text-sm text-muted-foreground"
                      title={sense.definition || "未填写释义"}
                    >
                      {sense.definition || "未填写释义"}
                    </span>
                  </div>
                </div>
                <OrderButtons
                  label={`释义 ${senseIndex + 1}`}
                  index={senseIndex}
                  count={form.senses.length}
                  disabled={readOnly}
                  onMove={(offset) =>
                    setForm({
                      ...form,
                      senses: move(form.senses, senseIndex, offset),
                    })
                  }
                  onDelete={() =>
                    setForm({
                      ...form,
                      senses: form.senses.filter(
                        (item) => item._key !== sense._key,
                      ),
                    })
                  }
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={
                    collapsedKeys[sense._key]
                      ? `展开释义 ${senseIndex + 1}`
                      : `折叠释义 ${senseIndex + 1}`
                  }
                  aria-expanded={!collapsedKeys[sense._key]}
                  onClick={() => toggleCollapse(sense._key)}
                >
                  {collapsedKeys[sense._key] ? (
                    <ChevronDown aria-hidden="true" />
                  ) : (
                    <ChevronUp aria-hidden="true" />
                  )}
                </Button>
              </div>
              {!collapsedKeys[sense._key] && (
                <div className="space-y-4 border-t p-4">
                  <div className="grid gap-4 md:grid-cols-[10rem_1fr_10rem]">
                    <Field label="词性" required>
                      <Select
                        value={sense.partOfSpeech}
                        disabled={readOnly}
                        onValueChange={(value) =>
                          updateSense(sense._key, (item) => ({
                            ...item,
                            partOfSpeech: value,
                          }))
                        }
                      >
                        <SelectTrigger className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {PART_OF_SPEECH_OPTIONS.map((option) => (
                            <SelectItem key={option.value} value={option.value}>
                              {option.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </Field>
                    <Field
                      label="释义"
                      required
                      error={fieldError(`senses[${senseIndex}].definition`)}
                    >
                      <Input
                        value={sense.definition}
                        maxLength={2000}
                        disabled={readOnly}
                        aria-invalid={Boolean(
                          fieldError(`senses[${senseIndex}].definition`),
                        )}
                        onChange={(event) =>
                          updateSense(sense._key, (item) => ({
                            ...item,
                            definition: event.target.value,
                          }))
                        }
                      />
                    </Field>
                  </div>
                  <Field
                    label="用法说明"
                    error={fieldError(`senses[${senseIndex}].usageNote`)}
                  >
                    <Textarea
                      value={sense.usageNote}
                      maxLength={1000}
                      disabled={readOnly}
                      className="min-h-20"
                      onChange={(event) =>
                        updateSense(sense._key, (item) => ({
                          ...item,
                          usageNote: event.target.value,
                        }))
                      }
                    />
                  </Field>
                  <div className="space-y-3 border-t pt-4">
                    <div className="flex items-center justify-between gap-3">
                      <h4 className="text-sm font-medium">例句</h4>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        disabled={readOnly || sense.examples.length >= 20}
                        onClick={() => addExample(sense._key)}
                      >
                        <Plus aria-hidden="true" />
                        添加例句
                      </Button>
                    </div>
                    {sense.examples.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        暂无例句。
                      </p>
                    ) : (
                      sense.examples.map((example, exampleIndex) => {
                        const prefix = `senses[${senseIndex}].examples[${exampleIndex}]`;
                        return (
                          <div
                            key={example._key}
                            ref={(node) => {
                              if (node)
                                exampleCardRefs.current.set(example._key, node);
                              else exampleCardRefs.current.delete(example._key);
                            }}
                            className="overflow-hidden rounded-lg border bg-card outline-none focus-visible:ring-2 focus-visible:ring-ring"
                            tabIndex={-1}
                          >
                            <div className="flex items-center justify-between gap-3 px-3 py-2">
                              <div className="min-w-0 flex-1">
                                <div className="flex items-center gap-2">
                                  <h4 className="text-sm font-medium whitespace-nowrap">
                                    [ 例句 {exampleIndex + 1} ]
                                  </h4>

                                  {collapsedKeys[example._key] && (
                                    <span className="text-sm text-muted-foreground truncate max-w-37.5 sm:max-w-75">
                                      {example.sentence || "（未填写例句原文）"}
                                    </span>
                                  )}
                                </div>
                              </div>

                              <OrderButtons
                                label={`例句 ${exampleIndex + 1}`}
                                index={exampleIndex}
                                count={sense.examples.length}
                                disabled={readOnly}
                                onMove={(offset) =>
                                  updateSense(sense._key, (item) => ({
                                    ...item,
                                    examples: move(
                                      item.examples,
                                      exampleIndex,
                                      offset,
                                    ),
                                  }))
                                }
                                onDelete={() =>
                                  updateSense(sense._key, (item) => ({
                                    ...item,
                                    examples: item.examples.filter(
                                      (value) => value._key !== example._key,
                                    ),
                                  }))
                                }
                              />
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                aria-label={
                                  collapsedKeys[example._key]
                                    ? `展开例句 ${exampleIndex + 1}`
                                    : `折叠例句 ${exampleIndex + 1}`
                                }
                                aria-expanded={!collapsedKeys[example._key]}
                                onClick={() => toggleCollapse(example._key)}
                              >
                                {collapsedKeys[example._key] ? (
                                  <ChevronDown className="size-4" />
                                ) : (
                                  <ChevronUp className="size-4" />
                                )}
                              </Button>
                            </div>
                            {!collapsedKeys[example._key] && (
                              <div className="space-y-3 border-t p-3">
                                <Field
                                  label="例句原文"
                                  required
                                  error={fieldError(`${prefix}.sentence`)}
                                >
                                  <Textarea
                                    value={example.sentence}
                                    maxLength={2000}
                                    disabled={readOnly}
                                    className="min-h-18"
                                    onChange={(event) =>
                                      updateSense(sense._key, (item) => ({
                                        ...item,
                                        examples: item.examples.map((value) =>
                                          value._key === example._key
                                            ? {
                                                ...value,
                                                sentence: event.target.value,
                                              }
                                            : value,
                                        ),
                                      }))
                                    }
                                  />
                                </Field>
                                <Field
                                  label="译文"
                                  required
                                  error={fieldError(`${prefix}.translation`)}
                                >
                                  <Textarea
                                    value={example.translation}
                                    maxLength={2000}
                                    disabled={readOnly}
                                    className="min-h-18"
                                    onChange={(event) =>
                                      updateSense(sense._key, (item) => ({
                                        ...item,
                                        examples: item.examples.map((value) =>
                                          value._key === example._key
                                            ? {
                                                ...value,
                                                translation: event.target.value,
                                              }
                                            : value,
                                        ),
                                      }))
                                    }
                                  />
                                </Field>
                                <AudioReference
                                  value={example.audioClipId}
                                  label="例句音频"
                                  disabled={readOnly}
                                  error={fieldError(`${prefix}.audioClipId`)}
                                  onChoose={() =>
                                    setAudioTarget({
                                      type: "example",
                                      senseKey: sense._key,
                                      itemKey: example._key,
                                      selectedId: example.audioClipId,
                                    })
                                  }
                                  onClear={() =>
                                    updateSense(sense._key, (item) => ({
                                      ...item,
                                      examples: item.examples.map((value) =>
                                        value._key === example._key
                                          ? { ...value, audioClipId: null }
                                          : value,
                                      ),
                                    }))
                                  }
                                />
                                <div className="flex justify-end border-t pt-3">
                                  <Button
                                    type="button"
                                    size="sm"
                                    // variant="outline"
                                    disabled={
                                      readOnly || sense.examples.length >= 20
                                    }
                                    onClick={() =>
                                      addExample(sense._key, example._key)
                                    }
                                  >
                                    <Plus aria-hidden="true" />
                                    收起并添加下一条例句
                                  </Button>
                                </div>
                              </div>
                            )}
                          </div>
                        );
                      })
                    )}
                  </div>
                  <div className="flex justify-end border-t pt-4">
                    <Button
                      type="button"
                      size="sm"
                      // variant="outline"
                      disabled={readOnly || form.senses.length >= 20}
                      onClick={() => addSense(sense._key)}
                    >
                      <Plus aria-hidden="true" />
                      收起并添加下一条释义
                    </Button>
                  </div>
                </div>
              )}
            </div>
          ))
        )}
      </section>
      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold">发音</h2>
            <p className="text-sm text-muted-foreground">
              发音必须引用已处理并发布的单词发音音频。
            </p>
          </div>
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={readOnly || form.pronunciations.length >= 20}
            onClick={() =>
              setForm({
                ...form,
                pronunciations: [...form.pronunciations, createPronunciation()],
              })
            }
          >
            <Plus aria-hidden="true" />
            添加发音
          </Button>
        </div>
        {form.pronunciations.length === 0 ? (
          <p className="border-y py-8 text-center text-sm text-muted-foreground">
            尚未添加发音。
          </p>
        ) : (
          form.pronunciations.map((item, index) => (
            <div key={item._key} className="space-y-3 rounded-lg border p-4">
              <div className="flex items-center justify-between gap-3">
                <h3 className="font-medium">发音 {index + 1}</h3>
                <OrderButtons
                  label={`发音 ${index + 1}`}
                  index={index}
                  count={form.pronunciations.length}
                  disabled={readOnly}
                  onMove={(offset) =>
                    setForm({
                      ...form,
                      pronunciations: move(form.pronunciations, index, offset),
                    })
                  }
                  onDelete={() =>
                    setForm({
                      ...form,
                      pronunciations: form.pronunciations.filter(
                        (value) => value._key !== item._key,
                      ),
                    })
                  }
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label="口音标签">
                  <Input
                    value={item.accentTag}
                    maxLength={100}
                    disabled={readOnly}
                    onChange={(event) =>
                      updatePronunciation(item._key, (value) => ({
                        ...value,
                        accentTag: event.target.value,
                      }))
                    }
                  />
                </Field>
                <Field label="国际音标（IPA）">
                  <Input
                    value={item.ipa}
                    maxLength={200}
                    disabled={readOnly}
                    onChange={(event) =>
                      updatePronunciation(item._key, (value) => ({
                        ...value,
                        ipa: event.target.value,
                      }))
                    }
                  />
                </Field>
              </div>
              <AudioReference
                value={item.audioClipId}
                label="发音音频"
                required
                disabled={readOnly}
                error={fieldError(`pronunciations[${index}].audioClipId`)}
                onChoose={() =>
                  setAudioTarget({
                    type: "pronunciation",
                    itemKey: item._key,
                    selectedId: item.audioClipId,
                  })
                }
                onClear={() =>
                  updatePronunciation(item._key, (value) => ({
                    ...value,
                    audioClipId: null,
                  }))
                }
              />
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={item.isDefault}
                  disabled={readOnly}
                  onCheckedChange={(checked) =>
                    setForm((current) => ({
                      ...current,
                      pronunciations: current.pronunciations.map((value) => ({
                        ...value,
                        isDefault:
                          value._key === item._key
                            ? Boolean(checked)
                            : checked
                              ? false
                              : value.isDefault,
                      })),
                    }))
                  }
                />
                设为默认发音
              </label>
            </div>
          ))
        )}
      </section>
      {word ? (
        <section className="grid gap-3 border-y py-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <Info
            label="创建者"
            value={word.createdBy.nickname ?? word.createdBy.id}
          />
          <Info
            label="最后修改"
            value={word.lastEditor.nickname ?? word.lastEditor.id}
          />
          <Info label="创建时间" value={formatDateTime(word.createdAt)} />
          <Info label="并发标识" value={word.concurrencyStamp} />
        </section>
      ) : null}
      <WordUnsavedChangesDialog blocker={blocker} />
      {audioTarget ? (
        <WordAudioPicker
          kind={
            audioTarget.type === "pronunciation"
              ? "WordPronunciation"
              : "ExampleSentence"
          }
          language={audioTarget.language}
          selectedId={audioTarget.selectedId}
          onClose={() => setAudioTarget(null)}
          onSelect={(audio) => {
            if (audioTarget.type === "pronunciation")
              updatePronunciation(audioTarget.itemKey, (value) => ({
                ...value,
                audioClipId: audio.id,
              }));
            else
              updateSense(audioTarget.senseKey, (sense) => ({
                ...sense,
                examples: sense.examples.map((value) =>
                  value._key === audioTarget.itemKey
                    ? { ...value, audioClipId: audio.id }
                    : value,
                ),
              }));
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
                  if (result.data) applyWord(result.data);
                  setConcurrencyConflict(false);
                }}
              >
                丢弃并重新加载
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      ) : null}
      {statusAction && word ? (
        <WordActionDialog
          action={statusAction}
          word={word}
          onClose={() => setStatusAction(null)}
          onDone={(message, saved) => {
            setNotice(message);
            if (saved) applyWord(saved);
          }}
          onConflict={async () => {
            const result = await refetch();
            if (result.data && !dirty) applyWord(result.data);
          }}
        />
      ) : null}
    </form>
  );
}

function Field({ id, label, required, error, children }) {
  const generatedId = useId();
  const controlId = id ?? generatedId;
  const errorId = `${controlId}-error`;
  return (
    <div className="space-y-1.5">
      <Label htmlFor={controlId}>
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </Label>
      {cloneElement(children, {
        id: controlId,
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
function AudioReference({
  value,
  label,
  required,
  disabled,
  error,
  onChoose,
  onClear,
}) {
  return (
    <div className="space-y-1.5">
      <Label>
        {label}
        {required ? <span aria-hidden="true"> *</span> : null}
      </Label>
      <div
        className="flex flex-wrap items-center gap-2"
        role="group"
        aria-label={label}
      >
        <code className="min-w-0 flex-1 truncate rounded bg-muted px-2 py-2 text-xs">
          {value ?? "未选择音频"}
        </code>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={disabled}
          onClick={onChoose}
        >
          <Volume2 aria-hidden="true" />
          {value ? "更换" : "选择"}
        </Button>
        {value ? (
          <Button
            type="button"
            size="sm"
            variant="ghost"
            disabled={disabled}
            onClick={onClear}
          >
            清除
          </Button>
        ) : null}
      </div>
      {error ? (
        <p className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
function Info({ label, value }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="mt-1 truncate" title={value}>
        {value}
      </p>
    </div>
  );
}

export default WordEditor;
