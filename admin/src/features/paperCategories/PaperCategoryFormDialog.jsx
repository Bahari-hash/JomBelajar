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
  useCreatePaperCategoryMutation,
  useUpdatePaperCategoryMutation,
} from "@/services/paperCategoriesApi.js";

function suggestSlug(name) {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function PaperCategoryFormDialog({ category, onClose, onDone }) {
  const editing = Boolean(category);
  const [form, setForm] = useState({
    name: category?.name ?? "",
    slug: category?.slug ?? "",
    description: category?.description ?? "",
    isActive: category?.isActive ?? true,
  });
  const [error, setError] = useState(null);
  const [createCategory, createState] = useCreatePaperCategoryMutation();
  const [updateCategory, updateState] = useUpdatePaperCategoryMutation();
  const pending = createState.isLoading || updateState.isLoading;
  const fieldError = (name) => error?.fieldErrors?.[name]?.[0];
  const update = (name, value) =>
    setForm((current) => ({ ...current, [name]: value }));
  const submit = async (event) => {
    event.preventDefault();
    const local = {};
    if (!form.name.trim()) local.name = ["请输入分类名称。"];
    else if (form.name.length > 100)
      local.name = ["分类名称不能超过 100 个字符。"];
    if (!form.slug.trim()) local.slug = ["请输入 slug。"];
    else if (
      form.slug.length > 120 ||
      !/^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/.test(form.slug.trim())
    )
      local.slug = [
        "slug 只能包含字母、数字和中划线，且不能以中划线开头或结尾。",
      ];
    if (form.description.length > 500)
      local.description = ["描述不能超过 500 个字符。"];
    if (Object.keys(local).length) {
      setError({ detail: "请修正表单字段。", fieldErrors: local });
      return;
    }
    setError(null);
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
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>
              {editing ? "编辑试卷分类" : "新建试卷分类"}
            </DialogTitle>
            <DialogDescription>
              分类用于组织试卷目录，可在试卷编辑器中多选。
            </DialogDescription>
          </DialogHeader>
          <div className="mt-4 space-y-4">
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>{getErrorMessage(error)}</AlertDescription>
              </Alert>
            ) : null}
            <div className="space-y-1.5">
              <Label htmlFor="paper-category-name">名称 *</Label>
              <Input
                id="paper-category-name"
                value={form.name}
                maxLength={100}
                aria-invalid={Boolean(fieldError("name"))}
                onChange={(event) => {
                  const name = event.target.value;
                  setForm((current) => ({
                    ...current,
                    name,
                    slug:
                      editing || current.slug
                        ? current.slug
                        : suggestSlug(name),
                  }));
                }}
              />
              {fieldError("name") ? (
                <p className="text-sm text-destructive">{fieldError("name")}</p>
              ) : null}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="paper-category-slug">Slug *</Label>
              <Input
                id="paper-category-slug"
                value={form.slug}
                maxLength={120}
                aria-invalid={Boolean(fieldError("slug"))}
                onChange={(event) => update("slug", event.target.value)}
              />
              {fieldError("slug") ? (
                <p className="text-sm text-destructive">{fieldError("slug")}</p>
              ) : null}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="paper-category-description">描述</Label>
              <Textarea
                id="paper-category-description"
                value={form.description}
                maxLength={500}
                rows={3}
                onChange={(event) => update("description", event.target.value)}
              />
              {fieldError("description") ? (
                <p className="text-sm text-destructive">
                  {fieldError("description")}
                </p>
              ) : null}
            </div>
            {editing ? (
              <div className="flex items-center justify-between border-y py-3">
                <div>
                  <Label htmlFor="paper-category-active">启用分类</Label>
                  <p className="text-sm text-muted-foreground">
                    停用后不能再关联到试卷。
                  </p>
                </div>
                <Switch
                  id="paper-category-active"
                  checked={form.isActive}
                  onCheckedChange={(value) => update("isActive", value)}
                />
              </div>
            ) : null}
          </div>
          <DialogFooter className="mt-5">
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
