import { Checkbox } from "@/components/ui/checkbox.jsx";

/** Accessible multi-select for assigning active video categories. */
export function VideoCategorySelector({
  categories,
  selectedIds,
  disabled,
  error,
  onChange,
  maxSelections = 10,
}) {
  return (
    <fieldset className="space-y-2" disabled={disabled}>
      <legend className="text-sm font-medium">视频分类</legend>
      <p className="text-xs text-muted-foreground">
        最多选择后端允许的分类数量；停用分类仅在已有视频上保留展示。
      </p>
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
      <div className="grid gap-2 border-y py-3 sm:grid-cols-2 lg:grid-cols-3">
        {categories
          .filter((item) => item.isActive || selectedIds.includes(item.id))
          .map((category) => {
            const checked = selectedIds.includes(category.id);
            return (
              <label
                key={category.id}
                className="flex min-w-0 cursor-pointer items-center gap-2 text-sm"
              >
                <Checkbox
                  checked={checked}
                  disabled={!checked && selectedIds.length >= maxSelections}
                  onCheckedChange={(next) =>
                    onChange(
                      next
                        ? [...selectedIds, category.id]
                        : selectedIds.filter((id) => id !== category.id),
                    )
                  }
                />
                <span className="truncate">
                  {category.name}
                  {category.isActive ? "" : "（已停用）"}
                </span>
              </label>
            );
          })}
        {categories.length === 0 ? (
          <p className="text-sm text-muted-foreground">暂无可用分类。</p>
        ) : null}
      </div>
    </fieldset>
  );
}
