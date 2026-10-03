import { MoreHorizontal, Volume2, VolumeX } from "lucide-react";
import { Link } from "react-router-dom";
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
import { getPartOfSpeechLabel } from "@/constants/wordOptions.js";
import { formatDateTime } from "@/lib/dateTime.js";

/** Compact administrator table for immediately available word content. */
export function WordTable({ words, onDelete, selected = {}, onSelect, onSelectPage, selectionDisabled = false }) {
  return (
    <div className="max-w-full min-w-0 overflow-x-auto rounded-lg border">
      <Table className="min-w-240 table-fixed">
        <TableHeader>
          <TableRow>
            {onSelect ? <TableHead className="w-12"><input type="checkbox" aria-label="全选本页单词" disabled={selectionDisabled}
              checked={words.length > 0 && words.every(w => selected[w.id])}
              ref={node => { if (node) node.indeterminate = words.some(w => selected[w.id]) && !words.every(w => selected[w.id]); }}
              onChange={e => onSelectPage(e.target.checked)} /></TableHead> : null}
            <TableHead className="w-44 pl-4">词头</TableHead>
            <TableHead className="w-52">词性与释义</TableHead>
            <TableHead className="w-36">内容数量</TableHead>
            <TableHead className="w-28">音频</TableHead>
            <TableHead className="w-40">创建时间</TableHead>
            <TableHead className="w-40">更新时间</TableHead>
            <TableHead className="w-14 pr-4 text-right">操作</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {words.map((word) => (
            <TableRow key={word.id}>
              {onSelect ? <TableCell><input type="checkbox" aria-label={`选择单词 ${word.headword}`} disabled={selectionDisabled}
                checked={Boolean(selected[word.id])} onChange={e => onSelect(word, e.target.checked)} /></TableCell> : null}
              <TableCell className="pl-4">
                <Link
                  className="block truncate font-medium hover:underline"
                  title={word.headword}
                  to={`/words/${word.id}`}
                >
                  {word.headword}
                </Link>
              </TableCell>
              <TableCell>
                <span className="block truncate text-sm">
                  {word.primaryPartOfSpeech
                    ? getPartOfSpeechLabel(word.primaryPartOfSpeech)
                    : "未设置词性"}
                </span>
                <span
                  className="block truncate text-xs text-muted-foreground"
                  title={word.primaryDefinition ?? ""}
                >
                  {word.primaryDefinition ?? "暂无释义"}
                </span>
              </TableCell>
              <TableCell className="text-sm text-muted-foreground">
                {word.senseCount} 释义 · {word.exampleCount} 例句
              </TableCell>
              <TableCell>
                <Badge variant={word.hasAudio ? "outline" : "secondary"}>
                  {word.hasAudio ? (
                    <Volume2 aria-hidden="true" />
                  ) : (
                    <VolumeX aria-hidden="true" />
                  )}
                  {word.hasAudio ? "已关联" : "未关联"}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">
                {formatDateTime(word.createdAt)}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {formatDateTime(word.updatedAt)}
              </TableCell>
              <TableCell className="pr-4 text-right">
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      variant="ghost"
                      size="icon"
                      aria-label={`管理单词 ${word.headword}`}
                    >
                      <MoreHorizontal aria-hidden="true" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuLabel>单词操作</DropdownMenuLabel>
                    <DropdownMenuItem asChild>
                      <Link to={`/words/${word.id}`}>编辑</Link>
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      variant="destructive"
                      onSelect={() => onDelete(word)}
                    >
                      永久删除
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
