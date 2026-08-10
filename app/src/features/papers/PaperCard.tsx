import { CalendarDays, ListCheck } from "lucide-react";
import { Link } from "react-router-dom";
import type { PaperCatalogItem } from "@/features/papers/paperTypes";
import {
  // formatLanguageTag,
  formatPaperDate,
} from "@/features/papers/paperUtils";

interface PaperCardProps {
  paper: PaperCatalogItem;
  listPath: string;
}

export default function PaperCard({ paper, listPath }: PaperCardProps) {
  return (
    <article className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-base-300 bg-base-100">
      <Link
        aria-label={`查看《${paper.title}》`}
        state={{ from: listPath }}
        to={`/papers/${paper.id}`}
      >
        <div className="grid aspect-16/7 place-items-center bg-primary/10 text-primary">
          <ListCheck aria-hidden="true" className="size-12" />
        </div>
      </Link>
      <div className="flex flex-1 flex-col p-4 sm:p-5">
        <div className="flex min-h-7 flex-wrap gap-1.5">
          {/* <span className="badge badge-outline max-w-full truncate">
            {formatLanguageTag(paper.languageTag)}
          </span> */}
          {paper.tags.length > 0 ? (
            <div className="contents" aria-label="试卷标签">
              {paper.tags.map((tag) => (
                <span
                  key={tag}
                  className="badge badge-outline hover:bg-base-200"
                >
                  {tag}
                </span>
              ))}
            </div>
          ) : null}
          <span className="badge badge-ghost">客观题</span>
        </div>
        <h2 className="mt-3 text-lg font-bold leading-7">
          <Link
            className="wrap-break-word hover:underline"
            state={{ from: listPath }}
            to={`/papers/${paper.id}`}
          >
            {paper.title}
          </Link>
        </h2>
        <p className="mt-2 min-h-18 line-clamp-3 text-sm leading-6 text-base-content/70">
          {paper.description?.trim() || "这份试卷暂未提供说明。"}
        </p>
        <div className="mt-5 grid grid-cols-3 gap-2 border-t border-base-300 pt-4 text-center text-xs text-base-content/70">
          <span>{paper.questionCount} 题</span>
          <span>总分 {paper.totalScore}</span>
          <span>及格 {paper.passingScore}</span>
        </div>
        <p className="mt-3 flex items-center gap-1.5 text-xs text-base-content/60">
          <CalendarDays aria-hidden="true" className="size-3.5" />
          <time dateTime={paper.publishedAt}>
            发布于 {formatPaperDate(paper.publishedAt)}
          </time>
        </p>
      </div>
    </article>
  );
}
