import { Link } from "react-router-dom";
import WrongQuestionsPanel from "@/features/papers/WrongQuestionsPanel";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function WrongQuestionsPage() {
  useDocumentTitle("错题本");
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <WrongQuestionsPanel />
      <Link className="btn btn-ghost" to="/papers">
        返回在线测试
      </Link>
    </div>
  );
}
