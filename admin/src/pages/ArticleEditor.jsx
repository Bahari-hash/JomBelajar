import { useEffect, useRef, useState } from "react";
import { ArrowLeft, Eye, ImageOff, Save, Send, Trash2 } from "lucide-react";
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
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import { ArticleActionDialog } from "@/features/articles/ArticleActionDialog.jsx";
import { ArticleCategorySelector } from "@/features/articles/ArticleCategorySelector.jsx";
import { ArticleHtmlPreview } from "@/features/articles/ArticleHtmlPreview.jsx";
import { ImageUploadControl } from "@/features/articles/ImageUploadControl.jsx";
import { MarkdownToolbar } from "@/features/articles/MarkdownToolbar.jsx";
import { UnsavedChangesDialog } from "@/features/articles/UnsavedChangesDialog.jsx";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { useUnsavedChanges } from "@/hooks/useUnsavedChanges.js";
import {
  getBodyMediaResourceIds,
  removeMarkdownImage,
} from "@/lib/markdownImages.js";
import { useGetAllArticleCategoryOptionsQuery } from "@/services/articleCategoriesApi.js";
import {
  useCreateArticleMutation,
  useGetEditorArticleQuery,
  usePreviewArticleMutation,
  useUpdateArticleMutation,
} from "@/services/articlesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

const EMPTY_FORM = {
  title: "",
  summary: "",
  contentMarkdown: "",
  categoryIds: [],
  coverMedia: null,
  bodyMedia: [],
  concurrencyStamp: null,
  status: "Draft",
};

function formFromArticle(article) {
  return {
    title: article.title,
    summary: article.summary ?? "",
    contentMarkdown: article.contentMarkdown,
    categoryIds: article.categories.map(({ id }) => id),
    coverMedia: article.coverMedia,
    bodyMedia: article.bodyMedia,
    concurrencyStamp: article.concurrencyStamp,
    status: article.status,
  };
}

function serializeForm(form) {
  return JSON.stringify({
    title: form.title,
    summary: form.summary.trim() || null,
    contentMarkdown: form.contentMarkdown,
    categoryIds: [...new Set(form.categoryIds)].sort(),
    coverMediaResourceId: form.coverMedia?.id ?? null,
    bodyMediaResourceIds: [
      ...new Set(form.bodyMedia.map(({ id }) => id)),
    ].sort(),
  });
}

function ArticleEditor() {
  const { articleId } = useParams();
  const isNew = !articleId;
  const navigate = useNavigate();
  const textareaRef = useRef(null);
  const initializedRef = useRef(isNew);
  const allowNavigationRef = useRef(false);
  const baselineRef = useRef(serializeForm(EMPTY_FORM));
  const previewRequestRef = useRef(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [viewMode, setViewMode] = useState("edit");
  const [previewedMarkdown, setPreviewedMarkdown] = useState(null);
  const [notice, setNotice] = useState(null);
  const [formError, setFormError] = useState(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const [statusAction, setStatusAction] = useState(null);
  const {
    data: article,
    error: loadError,
    isLoading,
    refetch,
  } = useGetEditorArticleQuery(articleId, { skip: isNew });
  const { data: categories = [], error: categoryError } =
    useGetAllArticleCategoryOptionsQuery();
  const [createArticle, createState] = useCreateArticleMutation();
  const [updateArticle, updateState] = useUpdateArticleMutation();
  const [previewArticle, previewState] = usePreviewArticleMutation();
  const pending = createState.isLoading || updateState.isLoading;
  const readOnly = form.status !== "Draft";
  const dirty = serializeForm(form) !== baselineRef.current;
  const blocker = useUnsavedChanges(dirty || pending, allowNavigationRef);
  const pageTitle = isNew ? "新建文章" : (article?.title ?? "文章编辑");
  useAdminPage(pageTitle, pageTitle);

  const applyArticle = (value) => {
    const next = formFromArticle(value);
    setForm(next);
    baselineRef.current = serializeForm(next);
    initializedRef.current = true;
    setPreviewedMarkdown(value.contentMarkdown);
    setFormError(null);
  };

  useEffect(() => {
    if (article && !initializedRef.current) applyArticle(article);
  }, [article]);
  useEffect(() => () => previewRequestRef.current?.abort?.(), []);

  const requestPreview = async () => {
    if (!form.contentMarkdown.trim()) {
      setFormError({
        detail: "请输入 Markdown 正文。",
        fieldErrors: { contentMarkdown: ["请输入 Markdown 正文。"] },
      });
      return;
    }
    previewRequestRef.current?.abort?.();
    const request = previewArticle(form.contentMarkdown);
    previewRequestRef.current = request;
    try {
      await request.unwrap();
      setPreviewedMarkdown(form.contentMarkdown);
    } catch (error) {
      if (error.kind !== "aborted") setFormError(error);
    }
  };

  const handleModeChange = (mode) => {
    setViewMode(mode);
    if (mode !== "edit" && previewedMarkdown !== form.contentMarkdown)
      requestPreview();
  };

  const insertBodyImage = (media) => {
    setForm((current) => ({
      ...current,
      bodyMedia: current.bodyMedia.some(({ id }) => id === media.id)
        ? current.bodyMedia
        : [...current.bodyMedia, { id: media.id, url: media.url }],
    }));
    const textarea = textareaRef.current;
    const start = textarea?.selectionStart ?? form.contentMarkdown.length;
    const alt = (media.name ?? "文章图片").replace(/\.[^.]+$/, "");
    const snippet = `\n![${alt}](${media.url})\n`;
    setForm((current) => ({
      ...current,
      contentMarkdown:
        current.contentMarkdown.slice(0, start) +
        snippet +
        current.contentMarkdown.slice(start),
    }));
    window.requestAnimationFrame(() => textareaRef.current?.focus());
  };

  const validate = () => {
    const errors = {};
    if (!form.title.trim()) errors.title = ["请输入文章标题。"];
    else if (form.title.length > 200)
      errors.title = ["标题不能超过 200 个字符。"];
    if (form.summary.length > 500)
      errors.summary = ["摘要不能超过 500 个字符。"];
    if (!form.contentMarkdown.trim())
      errors.contentMarkdown = ["请输入 Markdown 正文。"];
    else if (form.contentMarkdown.length > 1_000_000)
      errors.contentMarkdown = ["正文不能超过 1,000,000 个字符。"];
    if (form.categoryIds.length > 10)
      errors.categoryIds = ["最多选择 10 个分类。"];
    const inactive = categories.filter(
      (category) =>
        form.categoryIds.includes(category.id) && !category.isActive,
    );
    if (inactive.length)
      errors.categoryIds = [
        `请移除或重新启用停用分类：${inactive.map(({ name }) => name).join("、")}`,
      ];
    let bodyMediaResourceIds = [];
    try {
      bodyMediaResourceIds = getBodyMediaResourceIds(
        form.contentMarkdown,
        form.bodyMedia,
      );
    } catch (error) {
      errors.bodyMediaResourceIds = [error.message];
    }
    if (Object.keys(errors).length) {
      setFormError({ detail: "请修正表单字段。", fieldErrors: errors });
      return null;
    }
    return {
      title: form.title,
      summary: form.summary.trim() || null,
      contentMarkdown: form.contentMarkdown,
      categoryIds: form.categoryIds,
      coverMediaResourceId: form.coverMedia?.id ?? null,
      bodyMediaResourceIds,
    };
  };

  const handleSave = async (event) => {
    event.preventDefault();
    if (event.currentTarget.querySelector('[data-uploading="true"]')) {
      setFormError({
        detail: "请等待图片上传并确认完成后再保存。",
        fieldErrors: {},
      });
      return;
    }
    if (pending || readOnly) return;
    const payload = validate();
    if (!payload) return;
    setFormError(null);
    setNotice(null);
    try {
      const saved = isNew
        ? await createArticle(payload).unwrap()
        : await updateArticle({
            articleId,
            ...payload,
            concurrencyStamp: form.concurrencyStamp,
          }).unwrap();
      applyArticle(saved);
      setNotice("文章已保存。");
      if (isNew) {
        allowNavigationRef.current = true;
        navigate(`/articles/${saved.id}/edit`, { replace: true });
      }
    } catch (error) {
      setFormError(error);
      if (error.errorCode === "ArticleConcurrencyConflict")
        setConcurrencyConflict(true);
      if (
        error.errorCode === "ArticleCategoryInactive" ||
        error.errorCode === "ArticleCategoryNotFound"
      )
        refetch();
    }
  };

  if (isLoading)
    return (
      <div className="space-y-4" role="status" aria-label="正在加载文章">
        <Skeleton className="h-9 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  if (loadError)
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {loadError.status === 404
            ? "文章不存在"
            : loadError.status === 403
              ? "无权编辑文章"
              : "文章加载失败"}
        </AlertTitle>
        <AlertDescription className="mt-2 flex flex-wrap items-center justify-between gap-3">
          <span>{getErrorMessage(loadError)}</span>
          <Button asChild variant="outline" size="sm">
            <Link to="/articles">
              <ArrowLeft aria-hidden="true" />
              返回文章列表
            </Link>
          </Button>
        </AlertDescription>
      </Alert>
    );

  const fieldError = (field) => formError?.fieldErrors?.[field]?.[0];
  const previewHtml =
    previewState.data?.contentHtml ?? article?.contentHtml ?? "";
  return (
    <form onSubmit={handleSave} className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-muted-foreground">
            {isNew
              ? "创建草稿"
              : form.status === "Draft"
                ? "草稿编辑"
                : "只读文章"}
          </p>
          <h1 className="mt-1 max-w-3xl truncate text-2xl font-semibold">
            {pageTitle}
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild type="button" variant="outline">
            <Link to="/articles">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
          {!isNew ? (
            <Button asChild type="button" variant="outline">
              <Link to={`/articles/${articleId}/preview`}>
                <Eye aria-hidden="true" />
                完整预览
              </Link>
            </Button>
          ) : null}
          {!isNew && form.status === "Draft" ? (
            <Button
              type="button"
              variant="outline"
              disabled={dirty || pending}
              onClick={() => setStatusAction("publish")}
            >
              <Send aria-hidden="true" />
              发布
            </Button>
          ) : null}
          <Button type="submit" disabled={pending || readOnly || categoryError}>
            <Save aria-hidden="true" />
            {pending ? "正在保存" : "保存"}
          </Button>
        </div>
      </header>
      {readOnly ? (
        <Alert>
          <AlertTitle>
            {form.status === "Published" ? "文章已发布" : "文章已归档"}
          </AlertTitle>
          <AlertDescription>
            {form.status === "Published"
              ? "请先在文章列表或完整预览中下架，再继续编辑。"
              : "归档文章是终态，只能查看预览。"}
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
            {formError.errorCode === "ArticleConcurrencyConflict"
              ? "文章已被其他编辑者修改。你的输入仍然保留，请选择是否重新加载。"
              : getErrorMessage(formError, "请修正表单后重试。")}
          </AlertDescription>
        </Alert>
      ) : null}
      <section className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="article-title">
            标题 <span aria-hidden="true">*</span>
          </Label>
          <Input
            id="article-title"
            value={form.title}
            maxLength={200}
            disabled={readOnly}
            aria-invalid={Boolean(fieldError("title"))}
            onChange={(event) => {
              setForm({ ...form, title: event.target.value });
              setFormError(null);
            }}
          />
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>{fieldError("title") ?? "最多 200 个字符。"}</span>
            <span>{form.title.length}/200</span>
          </div>
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="article-summary">摘要</Label>
          <Textarea
            id="article-summary"
            value={form.summary}
            maxLength={500}
            rows={3}
            disabled={readOnly}
            aria-invalid={Boolean(fieldError("summary"))}
            onChange={(event) => {
              setForm({ ...form, summary: event.target.value });
              setFormError(null);
            }}
          />
          <div className="flex justify-between text-xs text-muted-foreground">
            <span>{fieldError("summary") ?? "可选，最多 500 个字符。"}</span>
            <span>{form.summary.length}/500</span>
          </div>
        </div>
      </section>
      <ArticleCategorySelector
        categories={categories}
        selectedIds={form.categoryIds}
        disabled={readOnly || Boolean(categoryError)}
        error={
          fieldError("categoryIds") ??
          (categoryError ? "分类选项加载失败，请刷新后重试。" : null)
        }
        onChange={(categoryIds) => {
          setForm({ ...form, categoryIds });
          setFormError(null);
        }}
      />
      <section className="space-y-3">
        <div>
          <h2 className="text-sm font-medium">封面图片</h2>
          <p className="text-xs text-muted-foreground">
            PNG、JPEG、GIF 或 WebP，不超过 5 MB。
          </p>
        </div>
        {form.coverMedia ? (
          <div className="flex flex-wrap items-center gap-3 rounded-lg border p-3">
            <img
              className="size-20 rounded-lg object-cover"
              src={form.coverMedia.url}
              alt="文章封面预览"
            />
            <div className="min-w-0 flex-1">
              <p className="truncate text-xs text-muted-foreground">
                {form.coverMedia.url}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={readOnly}
                  onClick={() => insertBodyImage(form.coverMedia)}
                >
                  插入正文
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  disabled={readOnly}
                  onClick={() => setForm({ ...form, coverMedia: null })}
                >
                  <ImageOff aria-hidden="true" />
                  移除封面
                </Button>
              </div>
            </div>
          </div>
        ) : null}
        <ImageUploadControl
          label={form.coverMedia ? "替换封面" : "上传封面"}
          disabled={readOnly}
          onUploaded={(media) =>
            setForm({
              ...form,
              coverMedia: { id: media.id, url: media.url, name: media.name },
            })
          }
        />
      </section>
      <section className="space-y-2">
        <div className="flex flex-wrap items-end justify-between gap-2">
          <div>
            <Label htmlFor="article-markdown">
              Markdown 正文 <span aria-hidden="true">*</span>
            </Label>
            <p className="text-xs text-muted-foreground">
              支持标题、强调、引用、列表、代码、绝对链接和已确认图片。
            </p>
          </div>
          <ImageUploadControl
            label="上传并插入正文图片"
            disabled={readOnly}
            onUploaded={insertBodyImage}
          />
        </div>
        <div className="overflow-hidden rounded-lg border">
          <MarkdownToolbar
            textareaRef={textareaRef}
            value={form.contentMarkdown}
            disabled={readOnly}
            onChange={(contentMarkdown) => {
              setForm({ ...form, contentMarkdown });
              setFormError(null);
            }}
          />
          <Textarea
            ref={textareaRef}
            id="article-markdown"
            value={form.contentMarkdown}
            maxLength={1_000_000}
            disabled={readOnly}
            aria-invalid={Boolean(
              fieldError("contentMarkdown") ||
              fieldError("bodyMediaResourceIds"),
            )}
            className="min-h-[32rem] resize-y rounded-none border-0 font-mono text-sm field-sizing-fixed focus-visible:ring-0"
            onChange={(event) => {
              setForm({ ...form, contentMarkdown: event.target.value });
              setFormError(null);
            }}
          />
        </div>
        <div className="flex justify-between gap-4 text-xs text-muted-foreground">
          <span>
            {fieldError("contentMarkdown") ??
              fieldError("bodyMediaResourceIds") ??
              `${form.bodyMedia.length} 个已确认正文图片资源`}
          </span>
          <span className="shrink-0 tabular-nums">
            {form.contentMarkdown.length}/1,000,000
          </span>
        </div>
        {form.bodyMedia.length ? (
          <div className="grid gap-2 sm:grid-cols-2">
            {form.bodyMedia.map((media, index) => (
              <div
                key={media.id}
                className="flex items-center gap-2 rounded-lg border p-2"
              >
                <img
                  src={media.url}
                  alt=""
                  className="size-12 rounded object-cover"
                />
                <div className="min-w-0 flex-1">
                  <p className="text-sm">正文图片 {index + 1}</p>
                  <p className="truncate text-xs text-muted-foreground">
                    {media.url}
                  </p>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={`移除正文图片 ${index + 1}`}
                  disabled={readOnly}
                  onClick={() =>
                    setForm((current) => ({
                      ...current,
                      contentMarkdown: removeMarkdownImage(
                        current.contentMarkdown,
                        media.url,
                      ),
                      bodyMedia: current.bodyMedia.filter(
                        ({ id }) => id !== media.id,
                      ),
                      coverMedia:
                        current.coverMedia?.id === media.id
                          ? current.coverMedia
                          : current.coverMedia,
                    }))
                  }
                >
                  <Trash2 aria-hidden="true" />
                </Button>
              </div>
            ))}
          </div>
        ) : null}
      </section>
      <section className="space-y-3">
        <Tabs value={viewMode} onValueChange={handleModeChange}>
          <TabsList>
            <TabsTrigger value="edit">编辑</TabsTrigger>
            <TabsTrigger value="preview">预览</TabsTrigger>
            <TabsTrigger value="split" className="hidden xl:inline-flex">
              分栏
            </TabsTrigger>
          </TabsList>
        </Tabs>
        {viewMode !== "edit" ? (
          <div
            className={
              viewMode === "split"
                ? "grid min-h-80 gap-4 xl:grid-cols-2"
                : "min-h-80"
            }
          >
            {viewMode === "split" ? (
              <pre className="max-h-[36rem] overflow-auto whitespace-pre-wrap break-words rounded-lg border p-4 text-sm">
                {form.contentMarkdown}
              </pre>
            ) : null}
            <div className="rounded-lg border p-4">
              {previewState.isLoading ? (
                <p className="text-sm text-muted-foreground" role="status">
                  正在更新规范预览...
                </p>
              ) : null}
              {previewHtml ? (
                <ArticleHtmlPreview canonicalHtml={previewHtml} />
              ) : !previewState.isLoading ? (
                <p className="text-sm text-muted-foreground">
                  保存或请求预览后显示正文。
                </p>
              ) : null}
            </div>
          </div>
        ) : null}
      </section>
      <div className="sticky bottom-3 flex justify-end">
        <Button type="submit" disabled={pending || readOnly || categoryError}>
          <Save aria-hidden="true" />
          {pending ? "正在保存" : dirty ? "保存修改" : "已保存"}
        </Button>
      </div>
      <UnsavedChangesDialog blocker={blocker} />
      {concurrencyConflict ? (
        <AlertDialog
          open
          onOpenChange={(open) => !open && setConcurrencyConflict(false)}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>重新加载服务端版本？</AlertDialogTitle>
              <AlertDialogDescription>
                重新加载会永久丢弃当前未保存内容。取消可继续保留本地输入。
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>保留本地内容</AlertDialogCancel>
              <Button
                variant="destructive"
                onClick={async () => {
                  const result = await refetch();
                  if (result.data) applyArticle(result.data);
                  setConcurrencyConflict(false);
                }}
              >
                丢弃并重新加载
              </Button>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      ) : null}
      {statusAction && article ? (
        <ArticleActionDialog
          action={statusAction}
          article={article}
          onClose={() => setStatusAction(null)}
          onDone={(message) => {
            setNotice(message);
            refetch().then(
              (result) => result.data && applyArticle(result.data),
            );
          }}
          onConflict={refetch}
        />
      ) : null}
    </form>
  );
}

export default ArticleEditor;
