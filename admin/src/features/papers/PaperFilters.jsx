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
import { PAPER_STATUS_OPTIONS } from "@/constants/paperStatus.js";

const ALL = "all";
const PAGE_SIZES = [20, 50, 100];

/** Edits paper list filters before applying them to shareable URL state. */
export function PaperFilters({ filters, onApply, onReset }) {
  const [draft, setDraft] = useState(filters);
  const { keyword, language, page, pageSize, status } = filters;
  useEffect(
    () => setDraft({ keyword, language, page, pageSize, status }),
    [keyword, language, page, pageSize, status],
  );
  const update = (key, value) =>
    setDraft((current) => ({ ...current, [key]: value }));

  const handleSubmit = (event) => {
    event.preventDefault();
    onApply({
      ...draft,
      keyword: draft.keyword.trim(),
      language: draft.language.trim(),
      status: draft.status === ALL ? "" : draft.status,
      pageSize: Number(draft.pageSize),
    });
  };

  const handleReset = () => {
    setDraft({ page: 1, pageSize: 20, keyword: "", language: "", status: "" });
    onReset();
  };

  return (
    <form onSubmit={handleSubmit} className="border-y py-4">
      <div className="grid items-start gap-x-4 gap-y-3 md:grid-cols-2 xl:grid-cols-[16rem_repeat(2,8rem)_6rem_auto]">
        <TextFilter
          id="paper-keyword"
          label="关键词"
          value={draft.keyword}
          maxLength={200}
          placeholder="试卷标题"
          onChange={(value) => update("keyword", value)}
        />
        <TextFilter
          id="paper-language"
          label="语言"
          value={draft.language}
          maxLength={35}
          placeholder="例如 en"
          onChange={(value) => update("language", value)}
        />
        <FilterSelect
          label="状态"
          value={draft.status || ALL}
          options={PAPER_STATUS_OPTIONS}
          allLabel="全部状态"
          onChange={(value) => update("status", value)}
        />
        <FilterSelect
          label="每页"
          value={String(draft.pageSize)}
          options={PAGE_SIZES.map((value) => ({
            value: String(value),
            label: `${value} 条`,
          }))}
          onChange={(value) => update("pageSize", value)}
        />
        <div className="space-y-1.5">
          <Label className="invisible hidden xl:block" aria-hidden="true">
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
              onClick={handleReset}
            >
              <RotateCcw aria-hidden="true" />
            </Button>
          </div>
        </div>
      </div>
    </form>
  );
}

function TextFilter({ id, label, value, onChange, ...props }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        {...props}
      />
    </div>
  );
}

function FilterSelect({ label, value, options, allLabel, onChange }) {
  const id = `paper-${label}`;
  return (
    <div className="min-w-0 space-y-1.5">
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
