import { useEffect, useState } from "react";
import { RotateCcw, Search } from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import { Input } from "@/components/ui/input.jsx";
import { Label } from "@/components/ui/label.jsx";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select.jsx";
import {
  PROCESSING_STATUS_OPTIONS,
  PUBLICATION_STATUS_OPTIONS,
} from "@/constants/videoStatus.js";

const ALL = "all";
const STANDARD_PAGE_SIZES = [20, 50, 100];

/** Edits video filters while committed list state remains in the URL. */
export function VideoFilters({
  filters,
  categories,
  creators,
  onApply,
  onReset,
}) {
  const [draft, setDraft] = useState(filters);
  useEffect(
    () =>
      setDraft({
        page: filters.page,
        pageSize: filters.pageSize,
        keyword: filters.keyword,
        processingStatus: filters.processingStatus,
        publicationStatus: filters.publicationStatus,
        categoryId: filters.categoryId,
        createdById: filters.createdById,
      }),
    [
      filters.categoryId,
      filters.createdById,
      filters.keyword,
      filters.page,
      filters.pageSize,
      filters.processingStatus,
      filters.publicationStatus,
    ],
  );

  const update = (key, value) =>
    setDraft((current) => ({ ...current, [key]: value }));
  const handleSubmit = (event) => {
    event.preventDefault();
    onApply({
      ...draft,
      keyword: draft.keyword.trim(),
      processingStatus:
        draft.processingStatus === ALL ? "" : draft.processingStatus,
      publicationStatus:
        draft.publicationStatus === ALL ? "" : draft.publicationStatus,
      categoryId: draft.categoryId === ALL ? "" : draft.categoryId,
      createdById: draft.createdById === ALL ? "" : draft.createdById,
      pageSize: Number(draft.pageSize),
    });
  };

  return (
    <form onSubmit={handleSubmit} className="border-y py-4">
      <div className="grid gap-y-3 gap-x-4 xl:grid-cols-[16rem_repeat(4,8rem)_6rem_auto] items-start">
        <div className="space-y-1.5">
          <Label htmlFor="video-keyword">关键词</Label>
          <Input
            id="video-keyword"
            value={draft.keyword}
            maxLength={200}
            placeholder="视频标题"
            onChange={(event) => update("keyword", event.target.value)}
          />
        </div>
        <FilterSelect
          label="处理状态"
          value={draft.processingStatus || ALL}
          onChange={(value) => update("processingStatus", value)}
          options={PROCESSING_STATUS_OPTIONS}
          allLabel="全部处理状态"
        />
        <FilterSelect
          label="发布状态"
          value={draft.publicationStatus || ALL}
          onChange={(value) => update("publicationStatus", value)}
          options={PUBLICATION_STATUS_OPTIONS}
          allLabel="全部发布状态"
        />
        <FilterSelect
          label="分类"
          value={draft.categoryId || ALL}
          onChange={(value) => update("categoryId", value)}
          options={categories.map((item) => ({
            value: item.id,
            label: item.name,
          }))}
          allLabel="全部分类"
        />
        <FilterSelect
          label="创建者"
          value={draft.createdById || ALL}
          onChange={(value) => update("createdById", value)}
          options={creators.map((item) => ({
            value: item.id,
            label: item.nickname || item.email,
          }))}
          allLabel="全部创建者"
        />
        <FilterSelect
          label="每页"
          value={String(draft.pageSize)}
          onChange={(value) => update("pageSize", value)}
          options={STANDARD_PAGE_SIZES.map((value) => ({
            value: String(value),
            label: `${value} 条`,
          }))}
        />
        <div className="space-y-1.5">
          <Label className="invisible hidden md:block" aria-hidden="true">
            操作
          </Label>
          <div className="flex gap-2">
            <Button type="submit">
              <Search aria-hidden="true" />
              应用
            </Button>
            <Button
              type="button"
              variant="outline"
              aria-label="重置筛选"
              onClick={onReset}
            >
              <RotateCcw aria-hidden="true" />
            </Button>
          </div>
        </div>
      </div>
    </form>
  );
}

function FilterSelect({ label, value, onChange, options, allLabel }) {
  const id = `video-${label}`;
  return (
    <div className="space-y-1.5 min-w-0">
      <Label id={`${id}-label`}>{label}</Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger className="w-full" aria-labelledby={`${id}-label`}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {allLabel ? <SelectItem value={ALL}>{allLabel}</SelectItem> : null}
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
