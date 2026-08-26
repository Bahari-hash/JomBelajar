import { ArrowLeft, CalendarDays, RefreshCw } from "lucide-react";
import { Link, useLocation, useParams } from "react-router-dom";
import UserAvatar from "@/components/UserAvatar";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import ArticleContent from "@/features/articles/ArticleContent";
// import ArticleCover from "@/features/articles/ArticleCover";
import styles from "@/pages/ArticleDetailPage.module.css";
import { useGetArticleQuery } from "@/features/articles/articleApi";
import { isGuid } from "@/features/articles/articleSearchParams";
import {
  formatArticleDate,
  getArticleErrorMessage,
  isArticleNotFoundError,
} from "@/features/articles/articleUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

function getSafeReturnPath(state: unknown) {
  if (typeof state !== "object" || state === null || !("from" in state)) {
    return "/articles";
  }
  const from = state.from;
  return typeof from === "string" && /^\/articles(?:\?[^#]*)?$/.test(from)
    ? from
    : "/articles";
}

function ArticleDetailSkeleton() {
  return (
    <div
      aria-label="文章详情加载中"
      className="mx-auto max-w-4xl space-y-6"
      role="status"
    >
      <div className="skeleton h-5 w-28" />
      <div className="skeleton h-12 w-4/5" />
      <div className="skeleton h-6 w-full max-w-2xl" />
      <div className="skeleton aspect-video w-full" />
      <div className="space-y-3 pt-5">
        <div className="skeleton h-5 w-full" />
        <div className="skeleton h-5 w-full" />
        <div className="skeleton h-5 w-3/4" />
      </div>
    </div>
  );
}

export default function ArticleDetailPage() {
  const { articleId } = useParams();
  const location = useLocation();
  const validArticleId = isGuid(articleId) ? articleId : null;
  const articleQuery = useGetArticleQuery(validArticleId ?? "", {
    skip: !validArticleId,
  });
  const notFound =
    !validArticleId || isArticleNotFoundError(articleQuery.error);
  useDocumentTitle(
    articleQuery.data?.title ?? (notFound ? "文章不存在" : "文章详情"),
  );
  const returnPath = getSafeReturnPath(location.state);

  if (!validArticleId || (articleQuery.isError && notFound)) {
    return (
      <section className="mx-auto max-w-xl py-14 text-center">
        <p className="text-sm font-semibold text-error">404</p>
        <h1 className="mt-2 text-2xl font-bold">文章不存在或已下架</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          该文章当前无法访问，你可以返回文章列表继续阅读。
        </p>
        <Link className="btn btn-primary mt-6" to={returnPath}>
          <ArrowLeft aria-hidden="true" className="size-4" />
          返回文章列表
        </Link>
      </section>
    );
  }

  if (articleQuery.isLoading) {
    return <ArticleDetailSkeleton />;
  }

  if (articleQuery.isError || !articleQuery.data) {
    return (
      <section className="mx-auto max-w-xl py-14 text-center" role="alert">
        <h1 className="text-2xl font-bold">文章加载失败</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          {getArticleErrorMessage(articleQuery.error)}
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => articleQuery.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重新加载
          </button>
          <Link className="btn btn-ghost" to={returnPath}>
            返回文章列表
          </Link>
        </div>
      </section>
    );
  }

  const article = articleQuery.data;
  const authorName = article.author.nickname?.trim() || "JomBelajar 编辑";

  return (
    <div className="mx-auto max-w-4xl">
      <Link className="btn btn-ghost btn-sm -ml-3" to={returnPath}>
        <ArrowLeft aria-hidden="true" className="size-4" />
        返回文章列表
      </Link>
      <article className={styles.articleCard}>
        <header className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2">
            {article.categories.map((category) => (
              <Link
                key={category.id}
                className="badge badge-outline hover:bg-base-200"
                to={`/articles?categoryId=${category.id}&page=1`}
              >
                {category.name}
              </Link>
            ))}
          </div>
          <h1 className="text-3xl font-bold leading-tight sm:text-4xl">
            {article.title}
          </h1>
          {article.summary?.trim() ? (
            <p className="max-w-3xl text-lg leading-8 text-base-content/70">
              {article.summary}
            </p>
          ) : null}
          {article.readingAudioResourceId ? (
            <AudioPlaybackButton
              audioResourceId={article.readingAudioResourceId}
              label="文章朗读"
              variant="player"
            />
          ) : null}
          <div className="mt-2 flex flex-wrap items-center gap-x-5 gap-y-3 border-y border-base-300 py-4 text-sm">
            <div className="flex items-center gap-2.5">
              <UserAvatar
                className="size-9"
                name={authorName}
                url={article.author.avatarUrl}
              />
              <span className="font-medium">{authorName}</span>
            </div>
            <span className="flex items-center gap-1.5 text-base-content/65">
              <CalendarDays aria-hidden="true" className="size-4" />
              <time dateTime={article.publishedAt ?? undefined}>
                发布于 {formatArticleDate(article.publishedAt)}
              </time>
            </span>
            {article.updatedAt !== article.publishedAt ? (
              <span className="text-base-content/60">
                更新于 {formatArticleDate(article.updatedAt)}
              </span>
            ) : null}
          </div>
        </header>

        {/* {article.coverUrl ? (
        <ArticleCover
          eager
          className="mt-8 rounded-lg border border-base-300"
          url={article.coverUrl}
          alt={`${article.title}封面`}
        />
        ) : null} */}

        <section aria-label="文章正文" className="mt-10">
          <ArticleContent contentHtml={article.contentHtml} />
        </section>
      </article>
    </div>
  );
}
