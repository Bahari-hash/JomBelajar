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
import { USER_STATUS_OPTIONS } from "@/lib/userFilters.js";
import { ROLE_OPTIONS } from "@/services/roles.js";

const ALL = "all";
const STANDARD_PAGE_SIZES = [20, 50, 100];

/** Edits server-supported user filters while the URL remains the committed state owner. */
export function UserFilters({ filters, onApply, onReset }) {
  const [keyword, setKeyword] = useState(filters.keyword);
  const [role, setRole] = useState(filters.role || ALL);
  const [status, setStatus] = useState(filters.status || ALL);
  const [pageSize, setPageSize] = useState(String(filters.pageSize));

  useEffect(() => {
    setKeyword(filters.keyword);
    setRole(filters.role || ALL);
    setStatus(filters.status || ALL);
    setPageSize(String(filters.pageSize));
  }, [filters.keyword, filters.pageSize, filters.role, filters.status]);

  const handleSubmit = (event) => {
    event.preventDefault();
    onApply({
      keyword: keyword.trim(),
      role: role === ALL ? "" : role,
      status: status === ALL ? "" : status,
      pageSize: Number(pageSize),
    });
  };

  const pageSizes = STANDARD_PAGE_SIZES.includes(filters.pageSize)
    ? STANDARD_PAGE_SIZES
    : [filters.pageSize, ...STANDARD_PAGE_SIZES].sort(
        (left, right) => left - right,
      );

  return (
    <form onSubmit={handleSubmit} className="border-y py-4">
      <div className="grid gap-3 md:grid-cols-[16rem_10rem_10rem_8rem_auto] items-start">
        <div className="space-y-1.5">
          <Label htmlFor="user-keyword">关键词</Label>
          <Input
            id="user-keyword"
            value={keyword}
            maxLength={200}
            onChange={(event) => setKeyword(event.target.value)}
            placeholder="邮箱、用户名、昵称或用户 ID"
          />
        </div>

        <div className="space-y-1.5">
          <Label id="user-role-label">角色</Label>
          <Select value={role} onValueChange={setRole}>
            <SelectTrigger className="w-full" aria-labelledby="user-role-label">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>全部角色</SelectItem>
              {ROLE_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label id="user-status-label">账户状态</Label>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger
              className="w-full"
              aria-labelledby="user-status-label"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>全部状态</SelectItem>
              {USER_STATUS_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label id="user-page-size-label">每页</Label>
          <Select value={pageSize} onValueChange={setPageSize}>
            <SelectTrigger
              className="w-full"
              aria-labelledby="user-page-size-label"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {pageSizes.map((size) => (
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
              onClick={onReset}
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
