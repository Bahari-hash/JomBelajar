import { Link } from "react-router-dom";
import type { WordStudySessionState } from "@/features/wordStudy/wordStudyTypes";

export default function WordStudyCompletion({
  session,
}: {
  session: WordStudySessionState;
}) {
  return (
    <section className="py-12 text-center">
      <h2 className="text-2xl font-bold">本组已完成</h2>
      <p className="mt-3 text-base-content/65">
        完成 {session.completedCount} 个，排除 {session.excludedCount} 个，跳过{" "}
        {session.skippedCount} 个
      </p>
      <Link className="btn btn-primary mt-7" to="/words">
        返回单词首页
      </Link>
    </section>
  );
}
