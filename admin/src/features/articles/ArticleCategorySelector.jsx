import { Badge } from "@/components/ui/badge.jsx";
import { Checkbox } from "@/components/ui/checkbox.jsx";

/** Selects up to ten categories while preserving visible inactive relationships. */
export function ArticleCategorySelector({
  categories,
  selectedIds,
  onChange,
  disabled,
  error,
}) {
  const selected = new Set(selectedIds);
  const handleChecked = (category, checked) => {
    if (checked && selectedIds.length >= 10) return;
    onChange(
      checked
        ? [...selectedIds, category.id]
        : selectedIds.filter((id) => id !== category.id),
    );
  };
  return (
    <fieldset className="space-y-2" disabled={disabled}>
      <legend className="text-sm font-medium">文章分类</legend>
      <p className="text-xs text-muted-foreground">
        最多选择 10 个分类。停用的历史分类必须移除或重新启用后才能保存。
      </p>
      <div className="grid max-h-44 gap-2 overflow-y-auto rounded-lg border p-3 sm:grid-cols-2">
        {categories.length ? (
          categories.map((category) => {
            const checked = selected.has(category.id);
            return (
              <label
                key={category.id}
                className="flex min-w-0 items-center gap-2 text-sm"
              >
                <Checkbox
                  checked={checked}
                  disabled={disabled || (!category.isActive && !checked)}
                  onCheckedChange={(value) =>
                    handleChecked(category, value === true)
                  }
                />
                <span className="truncate">{category.name}</span>
                {!category.isActive ? (
                  <Badge variant="secondary">已停用</Badge>
                ) : null}
              </label>
            );
          })
        ) : (
          <p className="text-sm text-muted-foreground">暂无可用分类</p>
        )}
      </div>
      {error ? (
        <p className="text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : (
        <p className="text-xs text-muted-foreground">
          已选择 {selectedIds.length}/10
        </p>
      )}
    </fieldset>
  );
}
