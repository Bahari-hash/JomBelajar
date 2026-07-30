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
import { ARTICLE_STATUS_OPTIONS } from "@/constants/articleStatus.js";

const ALL = "all";
const STANDARD_PAGE_SIZES = [20, 50, 100];

/** Edits server-supported article filters while the URL owns committed state. */
export function ArticleFilters({ filters, categories = [], onApply, onReset }) {
  const [keyword, setKeyword] = useState(filters.keyword);
  const [status, setStatus] = useState(filters.status || ALL);
  const [categoryId, setCategoryId] = useState(filters.categoryId || ALL);
  const [pageSize, setPageSize] = useState(String(filters.pageSize));

  useEffect(() => {
    setKeyword(filters.keyword);
    setStatus(filters.status || ALL);
    setCategoryId(filters.categoryId || ALL);
    setPageSize(String(filters.pageSize));
  }, [filters.categoryId, filters.keyword, filters.pageSize, filters.status]);

  const handleReset = () => {
    setKeyword("");
    setStatus(ALL);
    setCategoryId(ALL);
    setPageSize(STANDARD_PAGE_SIZES[0]);
    if (onReset) {
      onReset();
    }
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    onApply({
      keyword: keyword.trim(),
      status: status === ALL ? "" : status,
      categoryId: categoryId === ALL ? "" : categoryId,
      pageSize: Number(pageSize),
    });
  };

  return (
    <form onSubmit={handleSubmit} className="border-y py-4">
      <div className="grid gap-y-3 gap-x-4 md:grid-cols-[16rem_10rem_10rem_8rem_auto] items-start">
        <div className="space-y-1.5">
          <Label htmlFor="article-keyword">关键词</Label>
          <Input
            id="article-keyword"
            value={keyword}
            maxLength={200}
            onChange={(event) => setKeyword(event.target.value)}
            placeholder="文章标题或摘要"
          />
        </div>
        <div className="space-y-1.5">
          <Label id="article-category-label">分类</Label>
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger
              className="w-full"
              aria-labelledby="article-category-label"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>全部分类</SelectItem>
              {categories.map((category) => (
                <SelectItem key={category.id} value={category.id}>
                  {category.name}
                  {category.isActive ? "" : "（停用）"}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label id="article-status-label">状态</Label>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger
              className="w-full"
              aria-labelledby="article-status-label"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>全部状态</SelectItem>
              {ARTICLE_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label id="article-page-size-label">每页</Label>
          <Select value={pageSize} onValueChange={setPageSize}>
            <SelectTrigger
              className="w-full"
              aria-labelledby="article-page-size-label"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {[20, 50, 100].map((size) => (
                <SelectItem key={size} value={String(size)}>
                  {size} 条
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label className="invisible hidden md:block" aria-hidden="true">
            操作
          </Label>
          <div className="flex gap-2">
            <Button type="submit" className="flex-1 md:flex-none">
              <Search aria-hidden="true" />
              应用
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={handleReset}
              aria-label="重置筛选"
            >
              <RotateCcw aria-hidden="true" />
            </Button>
          </div>
        </div>
      </div>
    </form>
  );
}
