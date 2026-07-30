import {
  Bold,
  Braces,
  Code,
  Heading2,
  Italic,
  Link,
  List,
  ListOrdered,
  Quote,
} from "lucide-react";
import { Button } from "@/components/ui/button.jsx";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip.jsx";

const TOOLS = [
  { label: "二级标题", icon: Heading2, before: "## ", after: "", line: true },
  { label: "粗体", icon: Bold, before: "**", after: "**" },
  { label: "斜体", icon: Italic, before: "*", after: "*" },
  { label: "引用", icon: Quote, before: "> ", after: "", line: true },
  { label: "无序列表", icon: List, before: "- ", after: "", line: true },
  {
    label: "有序列表",
    icon: ListOrdered,
    before: "1. ",
    after: "",
    line: true,
  },
  { label: "行内代码", icon: Code, before: "`", after: "`" },
  { label: "代码块", icon: Braces, before: "```\n", after: "\n```" },
  { label: "链接", icon: Link, before: "[", after: "](https://example.com)" },
];

/** Applies supported Markdown syntax while retaining the native textarea selection contract. */
export function MarkdownToolbar({ textareaRef, value, onChange, disabled }) {
  const applyTool = (tool) => {
    const textarea = textareaRef.current;
    if (!textarea) return;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = value.slice(start, end) || (tool.line ? "文本" : "内容");
    const insertionStart = tool.line
      ? value.lastIndexOf("\n", Math.max(0, start - 1)) + 1
      : start;
    const leadingLineContent = tool.line
      ? value.slice(insertionStart, start)
      : "";
    const next =
      value.slice(0, insertionStart) +
      tool.before +
      leadingLineContent +
      selected +
      tool.after +
      value.slice(end);
    onChange(next);
    window.requestAnimationFrame(() => {
      const selectionStart = start + tool.before.length;
      textarea.focus();
      textarea.setSelectionRange(
        selectionStart,
        selectionStart + selected.length,
      );
    });
  };

  return (
    <div
      className="flex min-h-10 flex-wrap items-center gap-1 border-b bg-muted/40 p-1.5"
      role="toolbar"
      aria-label="Markdown 格式工具栏"
    >
      {TOOLS.map((tool) => {
        const Icon = tool.icon;
        return (
          <Tooltip key={tool.label}>
            <TooltipTrigger asChild>
              <Button
                type="button"
                size="icon-sm"
                variant="ghost"
                aria-label={tool.label}
                disabled={disabled}
                onClick={() => applyTool(tool)}
              >
                <Icon aria-hidden="true" />
              </Button>
            </TooltipTrigger>
            <TooltipContent>{tool.label}</TooltipContent>
          </Tooltip>
        );
      })}
    </div>
  );
}
