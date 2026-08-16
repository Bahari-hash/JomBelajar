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
  PART_OF_SPEECH_OPTIONS,
  WORD_STATUS_OPTIONS,
} from "@/constants/wordStatus.js";

const ALL = "all";
const PAGE_SIZES = [20, 50, 100];

/** Edits word filters while the applied state remains shareable in the URL. */
export function WordFilters({ filters, onApply, onReset }) {
  const [draft, setDraft] = useState(filters);
  const {
    definition,
    keyword,
    language,
    page,
    pageSize,
    partOfSpeech,
    status,
  } = filters;
  useEffect(
    () =>
      setDraft({
        definition,
        keyword,
        language,
        page,
        pageSize,
        partOfSpeech,
        status,
      }),
    [definition, keyword, language, page, pageSize, partOfSpeech, status],
  );
  const update = (key, value) =>
    setDraft((current) => ({ ...current, [key]: value }));

  const handleSubmit = (event) => {
    event.preventDefault();
    onApply({
      ...draft,
      keyword: draft.keyword.trim(),
      language: draft.language.trim(),
      definition: draft.definition.trim(),
      status: draft.status === ALL ? "" : draft.status,
      partOfSpeech: draft.partOfSpeech === ALL ? "" : draft.partOfSpeech,
      pageSize: Number(draft.pageSize),
    });
  };

  const handleReset = () => {
    setDraft({
      page: 1,
      pageSize: 20,
      keyword: "",
      language: "",
      status: "",
      partOfSpeech: "",
      definition: "",
    });
    onReset();
  };

  return (
    <form onSubmit={handleSubmit} className="border-y py-4">
      <div className="grid items-start gap-x-4 gap-y-3 md:grid-cols-2 xl:grid-cols-[16rem_repeat(2,8rem)_10rem_6rem_auto]">
        <TextFilter
          id="word-keyword"
          label="关键词"
          value={draft.keyword}
          maxLength={200}
          placeholder="词头关键词"
          onChange={(value) => update("keyword", value)}
        />
        <FilterSelect
          label="状态"
          value={draft.status || ALL}
          options={WORD_STATUS_OPTIONS}
          allLabel="全部状态"
          onChange={(value) => update("status", value)}
        />
        <FilterSelect
          label="词性"
          value={draft.partOfSpeech || ALL}
          options={PART_OF_SPEECH_OPTIONS}
          allLabel="全部词性"
          onChange={(value) => update("partOfSpeech", value)}
        />
        <TextFilter
          id="word-definition"
          label="释义"
          value={draft.definition}
          placeholder="释义关键词"
          maxLength={200}
          onChange={(value) => update("definition", value)}
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
  const id = `word-${label}`;
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
