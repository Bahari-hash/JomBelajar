import { MoreHorizontal } from "lucide-react";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu.jsx";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table.jsx";
import { formatDateTime } from "@/lib/dateTime.js";

/** Presents administrator category data with explicit edit and delete commands. */
export function CategoryTable({ categories, onEdit, onDelete }) {
  return (
    <div className="rounded-lg border">
      <Table className="min-w-[48rem] table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead className="w-[22%] pl-4">名称</TableHead>
            <TableHead className="w-[22%]">Slug</TableHead>
            <TableHead>描述</TableHead>
            <TableHead className="w-24">状态</TableHead>
            <TableHead className="w-24 text-right">文章数</TableHead>
            <TableHead className="w-40">创建时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {categories.map((category) => (
            <TableRow key={category.id}>
              <TableCell
                className="truncate pl-4 font-medium"
                title={category.name}
              >
                {category.name}
              </TableCell>
              <TableCell
                className="truncate font-mono text-xs"
                title={category.slug}
              >
                {category.slug}
              </TableCell>
              <TableCell
                className="truncate text-muted-foreground"
                title={category.description ?? ""}
              >
                {category.description ?? "无描述"}
              </TableCell>
              <TableCell>
                <Badge variant={category.isActive ? "outline" : "secondary"}>
                  {category.isActive ? "启用" : "停用"}
                </Badge>
              </TableCell>
              <TableCell className="text-right tabular-nums">
                {category.articleCount}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {formatDateTime(category.createdAt)}
              </TableCell>
              <TableCell className="pr-4 text-right">
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`管理分类 ${category.name}`}
                    >
                      <MoreHorizontal aria-hidden="true" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuLabel>分类操作</DropdownMenuLabel>
                    <DropdownMenuItem onSelect={() => onEdit(category)}>
                      编辑与{category.isActive ? "停用" : "启用"}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      onSelect={() => onDelete(category)}
                    >
                      删除
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
