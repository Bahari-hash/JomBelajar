import { BookOpenText } from "lucide-react";
import ModulePlaceholder from "@/components/ModulePlaceholder";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function ArticlesPage() {
  useDocumentTitle("文章");
  return (
    <ModulePlaceholder
      title="文章"
      description="阅读适合当前阶段的外语文章，在上下文中积累词汇和表达。"
      icon={BookOpenText}
    />
  );
}
