import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import { Switch } from "@/components/ui/switch.jsx";
import { Textarea } from "@/components/ui/textarea.jsx";
import { getErrorMessage } from "@/services/problemDetails.js";
import {
  useCreateVideoCategoryMutation,
  useUpdateVideoCategoryMutation,
} from "@/services/videoCategoriesApi.js";

function suggestSlug(name) {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

/** Creates or edits one video category using the backend validation contract. */
export function VideoCategoryFormDialog({ category, onClose, onDone }) {
  const editing = Boolean(category);
  const [form, setForm] = useState({
    name: category?.name ?? "",
    slug: category?.slug ?? "",
    description: category?.description ?? "",
    isActive: category?.isActive ?? true,
  });
  const [error, setError] = useState(null);
  const [createCategory, createState] = useCreateVideoCategoryMutation();
  const [updateCategory, updateState] = useUpdateVideoCategoryMutation();
  const pending = createState.isLoading || updateState.isLoading;
  const fieldError = (field) => error?.fieldErrors?.[field]?.[0];
  const handleSubmit = async (event) => {
    event.preventDefault();
    if (pending) return;
    const local = {};
    if (!form.name.trim()) local.name = ["请输入分类名称。"];
    else if (form.name.length > 100)
      local.name = ["分类名称不能超过 100 个字符。"];
    if (
      !/^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/.test(form.slug.trim()) ||
      form.slug.length > 120
    )
      local.slug = ["Slug 只能包含字母、数字和单个中划线。"];
    if (form.description.length > 500)
      local.description = ["描述不能超过 500 个字符。"];
    if (Object.keys(local).length) {
      setError({ detail: "请修正表单字段。", fieldErrors: local });
      return;
    }
    try {
      const body = {
        name: form.name,
        slug: form.slug,
        description: form.description.trim() || null,
      };
      const saved = editing
        ? await updateCategory({
            categoryId: category.id,
            ...body,
            isActive: form.isActive,
          }).unwrap()
        : await createCategory(body).unwrap();
      onDone(`分类“${saved.name}”已${editing ? "更新" : "创建"}。`);
      onClose();
    } catch (requestError) {
      setError(requestError);
    }
  };
  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent className="max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>
              {editing ? "编辑视频分类" : "新建视频分类"}
            </DialogTitle>
            <DialogDescription>
              分类用于组织视频管理与学习目录。
            </DialogDescription>
          </DialogHeader>
          <div className="mt-4 space-y-4">
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>
                  {getErrorMessage(error, "请修正表单后重试。")}
                </AlertDescription>
              </Alert>
            ) : null}
            <Field
              label="名称"
              required
              error={fieldError("name")}
              count={`${form.name.length}/100`}
            >
              <Input
                id="video-category-name"
                value={form.name}
                maxLength={100}
                aria-invalid={Boolean(fieldError("name"))}
                onChange={(event) =>
                  setForm({ ...form, name: event.target.value })
                }
              />
            </Field>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between gap-2">
                <Label htmlFor="video-category-slug">
                  Slug <span aria-hidden="true">*</span>
                </Label>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={!suggestSlug(form.name)}
                  onClick={() =>
                    setForm({ ...form, slug: suggestSlug(form.name) })
                  }
                >
                  根据名称建议
                </Button>
              </div>
              <Input
                id="video-category-slug"
                value={form.slug}
                maxLength={120}
                aria-invalid={Boolean(fieldError("slug"))}
                onChange={(event) =>
                  setForm({ ...form, slug: event.target.value })
                }
              />
              <p className="text-xs text-muted-foreground">
                {fieldError("slug") ?? "服务端保存时统一转为小写。"}
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="video-category-description">描述</Label>
              <Textarea
                id="video-category-description"
                value={form.description}
                maxLength={500}
                rows={4}
                aria-invalid={Boolean(fieldError("description"))}
                onChange={(event) =>
                  setForm({ ...form, description: event.target.value })
                }
              />
              <p className="text-xs text-muted-foreground">
                {fieldError("description") ?? `${form.description.length}/500`}
              </p>
            </div>
            {editing ? (
              <div className="flex items-center justify-between gap-4 rounded-lg border p-3">
                <div>
                  <Label htmlFor="video-category-active">启用分类</Label>
                  <p className="text-xs text-muted-foreground">
                    停用后不能用于新的视频关联。
                  </p>
                </div>
                <Switch
                  id="video-category-active"
                  checked={form.isActive}
                  onCheckedChange={(isActive) => setForm({ ...form, isActive })}
                />
              </div>
            ) : null}
          </div>
          <DialogFooter className="mt-4">
            <Button
              type="button"
              variant="outline"
              disabled={pending}
              onClick={onClose}
            >
              取消
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? "正在保存" : "保存"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function Field({ label, required, error, count, children }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor="video-category-name">
        {label} {required ? <span aria-hidden="true">*</span> : null}
      </Label>
      {children}
      <div className="flex justify-between text-xs text-muted-foreground">
        <span>{error ?? "最多 100 个字符。"}</span>
        <span>{count}</span>
      </div>
    </div>
  );
}
