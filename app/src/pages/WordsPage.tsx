import { Languages } from "lucide-react";
import ModulePlaceholder from "@/components/ModulePlaceholder";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WordsPage() {
  useDocumentTitle("单词");
  return (
    <ModulePlaceholder
      title="单词"
      description="系统学习词义、发音与用法，并通过复习逐步巩固记忆。"
      icon={Languages}
    />
  );
}
