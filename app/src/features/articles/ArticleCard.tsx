import { CalendarDays } from "lucide-react";
import { Link } from "react-router-dom";
import UserAvatar from "@/components/UserAvatar";
import ArticleCover from "@/features/articles/ArticleCover";
import type { ArticleListItem } from "@/features/articles/articleTypes";
import { formatArticleDate } from "@/features/articles/articleUtils";

interface ArticleCardProps {
  article: ArticleListItem;
  listPath: string;
}

/** Presents one public article without inventing engagement metadata. */
export default function ArticleCard({ article, listPath }: ArticleCardProps) {
  const authorName = article.author.nickname?.trim() || "TinyLang 编辑";
  const visibleCategories = article.categories.slice(0, 3);
  const remainingCategoryCount = article.categories.length - 3;

  return (
    <article className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-base-300 bg-base-100">
      <Link
        aria-label={`阅读《${article.title}》`}
        state={{ from: listPath }}
        to={`/articles/${article.id}`}
      >
        <ArticleCover url={article.coverUrl} alt={`${article.title}封面`} />
      </Link>
      <div className="flex flex-1 flex-col p-4 sm:p-5">
        <div className="flex min-h-7 flex-wrap gap-1.5">
          {visibleCategories.map((category) => (
            <Link
              key={category.id}
              className="badge badge-outline max-w-full truncate hover:bg-base-200"
              title={category.name}
              to={`/articles?categoryId=${category.id}`}
            >
              {category.name}
            </Link>
          ))}
          {remainingCategoryCount > 0 ? (
            <span
              className="badge badge-ghost"
              title={article.categories
                .slice(3)
                .map((category) => category.name)
                .join("、")}
            >
              +{remainingCategoryCount} 个
            </span>
          ) : null}
        </div>
        <h2 className="mt-3 text-lg font-bold leading-7">
          <Link
            className="hover:underline"
            state={{ from: listPath }}
            to={`/articles/${article.id}`}
          >
            {article.title}
          </Link>
        </h2>
        <p className="mt-2 line-clamp-3 min-h-18 text-sm leading-6 text-base-content/70">
          {article.summary?.trim() || "这篇文章暂未提供摘要。"}
        </p>
        <div className="mt-5 flex items-center gap-3 border-t border-base-300 pt-4">
          <UserAvatar
            className="size-9"
            name={authorName}
            url={article.author.avatarUrl}
          />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{authorName}</p>
            <p className="mt-0.5 flex items-center gap-1.5 text-xs text-base-content/60">
              <CalendarDays aria-hidden="true" className="size-3.5" />
              <time dateTime={article.publishedAt ?? undefined}>
                {formatArticleDate(article.publishedAt)}
              </time>
            </p>
          </div>
        </div>
      </div>
    </article>
  );
}
