import { CircleHelp } from "lucide-react";
import ModulePlaceholder from "@/components/ModulePlaceholder";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function PapersPage() {
  useDocumentTitle("在线测试");
  return (
    <ModulePlaceholder
      title="在线测试"
      description="完成在线习题，检查知识掌握情况并发现需要加强的部分。"
      icon={CircleHelp}
    />
  );
}
