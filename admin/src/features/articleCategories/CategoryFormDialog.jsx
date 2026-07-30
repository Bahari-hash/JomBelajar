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
import {
  useCreateArticleCategoryMutation,
  useUpdateArticleCategoryMutation,
} from "@/services/articleCategoriesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

function suggestSlug(name) {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

/** Creates or edits one category using the backend's exact validation contract. */
export function CategoryFormDialog({ category, onClose, onDone }) {
  const editing = Boolean(category);
  const [name, setName] = useState(category?.name ?? "");
  const [slug, setSlug] = useState(category?.slug ?? "");
  const [description, setDescription] = useState(category?.description ?? "");
  const [isActive, setIsActive] = useState(category?.isActive ?? true);
  const [error, setError] = useState(null);
  const [createCategory, createState] = useCreateArticleCategoryMutation();
  const [updateCategory, updateState] = useUpdateArticleCategoryMutation();
  const pending = createState.isLoading || updateState.isLoading;
  const fieldError = (field) => error?.fieldErrors?.[field]?.[0];

  const handleSubmit = async (event) => {
    event.preventDefault();
    if (pending) return;
    const localErrors = {};
    if (!name.trim()) localErrors.name = ["请输入分类名称。"];
    if (name.length > 100) localErrors.name = ["分类名称不能超过 100 个字符。"];
    if (!slug.trim()) localErrors.slug = ["请输入 slug。"];
    if (
      slug.length > 120 ||
      !/^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/.test(slug.trim())
    )
      localErrors.slug = [
        "slug 只能包含字母、数字和中划线，且不能以中划线开头或结尾。",
      ];
    if (description.length > 500)
      localErrors.description = ["描述不能超过 500 个字符。"];
    if (Object.keys(localErrors).length) {
      setError({ detail: "请修正表单字段。", fieldErrors: localErrors });
      return;
    }
    setError(null);
    const body = { name, slug, description: description.trim() || null };
    try {
      const saved = editing
        ? await updateCategory({
            categoryId: category.id,
            ...body,
            isActive,
          }).unwrap()
        : await createCategory(body).unwrap();
      onDone(`${saved.name} 已${editing ? "更新" : "创建"}。`);
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
              {editing ? "编辑文章分类" : "新建文章分类"}
            </DialogTitle>
            <DialogDescription>
              分类用于组织文章列表和编辑筛选。
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
            <div className="space-y-1.5">
              <Label htmlFor="category-name">
                名称 <span aria-hidden="true">*</span>
              </Label>
              <Input
                id="category-name"
                value={name}
                maxLength={100}
                onChange={(event) => {
                  setName(event.target.value);
                  setError(null);
                }}
                aria-invalid={Boolean(fieldError("name"))}
              />
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{fieldError("name") ?? "最多 100 个字符。"}</span>
                <span>{name.length}/100</span>
              </div>
            </div>
            <div className="space-y-1.5">
              <div className="flex items-center justify-between gap-2">
                <Label htmlFor="category-slug">
                  Slug <span aria-hidden="true">*</span>
                </Label>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => setSlug(suggestSlug(name))}
                  disabled={!suggestSlug(name)}
                >
                  根据名称建议
                </Button>
              </div>
              <Input
                id="category-slug"
                value={slug}
                maxLength={120}
                onChange={(event) => {
                  setSlug(event.target.value);
                  setError(null);
                }}
                aria-invalid={Boolean(fieldError("slug"))}
              />
              <p className="text-xs text-muted-foreground">
                {fieldError("slug") ?? "保存后由服务端统一转为小写。"}
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="category-description">描述</Label>
              <Textarea
                id="category-description"
                value={description}
                maxLength={500}
                rows={4}
                onChange={(event) => {
                  setDescription(event.target.value);
                  setError(null);
                }}
                aria-invalid={Boolean(fieldError("description"))}
              />
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>{fieldError("description") ?? "可选。"}</span>
                <span>{description.length}/500</span>
              </div>
            </div>
            {editing ? (
              <div className="flex items-center justify-between gap-4 rounded-lg border p-3">
                <div>
                  <Label htmlFor="category-active">启用分类</Label>
                  <p className="text-xs text-muted-foreground">
                    停用后不能用于文章更新或发布。
                  </p>
                </div>
                <Switch
                  id="category-active"
                  checked={isActive}
                  onCheckedChange={setIsActive}
                />
              </div>
            ) : null}
          </div>
          <DialogFooter className="mt-4">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={pending}
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
