import { useState } from "react";
import { Archive, ArrowLeft, Edit3, Send, Undo2 } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert.jsx";
import { Badge } from "@/components/ui/badge.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Skeleton } from "@/components/ui/skeleton.jsx";
import { ArticleActionDialog } from "@/features/articles/ArticleActionDialog.jsx";
import { ArticleHtmlPreview } from "@/features/articles/ArticleHtmlPreview.jsx";
import {
  getArticleActions,
  getArticleStatusLabel,
} from "@/constants/articleStatus.js";
import { useAdminPage } from "@/hooks/useAdminPage.js";
import { formatDateTime } from "@/lib/dateTime.js";
import { useGetAdminArticleQuery } from "@/services/articlesApi.js";
import { getErrorMessage } from "@/services/problemDetails.js";

function ArticlePreview() {
  const { articleId } = useParams();
  const [pendingAction, setPendingAction] = useState(null);
  const [notice, setNotice] = useState(null);
  const {
    data: article,
    error,
    isLoading,
    refetch,
  } = useGetAdminArticleQuery(articleId);
  useAdminPage(article?.title ?? "文章预览", article?.title ?? "文章预览");

  if (isLoading)
    return (
      <div className="space-y-4" role="status" aria-label="正在加载文章预览">
        <Skeleton className="h-9 w-2/3" />
        <Skeleton className="h-80 w-full" />
      </div>
    );
  if (error)
    return (
      <Alert variant="destructive">
        <AlertTitle>
          {error.status === 404
            ? "文章不存在"
            : error.status === 403
              ? "无权预览文章"
              : "文章预览加载失败"}
        </AlertTitle>
        <AlertDescription className="mt-2 flex items-center justify-between gap-3">
          <span>{getErrorMessage(error)}</span>
          <Button asChild variant="outline" size="sm">
            <Link to="/articles">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
        </AlertDescription>
      </Alert>
    );
  if (!article) return null;
  const actions = getArticleActions(article.status);
  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Badge
              variant={article.status === "Published" ? "default" : "outline"}
            >
              {getArticleStatusLabel(article.status)}
            </Badge>
            <span className="text-xs text-muted-foreground">
              更新于 {formatDateTime(article.updatedAt)}
            </span>
          </div>
          <h1 className="mt-2 max-w-4xl wrap-break-word text-2xl font-semibold">
            {article.title}
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link to="/articles">
              <ArrowLeft aria-hidden="true" />
              返回列表
            </Link>
          </Button>
          {actions.includes("edit") ? (
            <Button asChild variant="outline">
              <Link to={`/articles/${article.id}/edit`}>
                <Edit3 aria-hidden="true" />
                编辑
              </Link>
            </Button>
          ) : null}
          {actions.includes("publish") ? (
            <Button onClick={() => setPendingAction("publish")}>
              <Send aria-hidden="true" />
              发布
            </Button>
          ) : null}
          {actions.includes("unpublish") ? (
            <Button
              variant="outline"
              onClick={() => setPendingAction("unpublish")}
            >
              <Undo2 aria-hidden="true" />
              下架
            </Button>
          ) : null}
          {actions.includes("archive") ? (
            <Button
              variant="destructive"
              onClick={() => setPendingAction("archive")}
            >
              <Archive aria-hidden="true" />
              归档
            </Button>
          ) : null}
        </div>
      </header>
      {notice ? (
        <Alert role="status">
          <AlertDescription>{notice}</AlertDescription>
        </Alert>
      ) : null}
      <article className="mx-auto w-full max-w-4xl space-y-5">
        {article.summary ? (
          <p className="text-base leading-7 text-muted-foreground">
            {article.summary}
          </p>
        ) : null}
        <div className="flex flex-wrap gap-2">
          {article.categories.length ? (
            article.categories.map((category) => (
              <Badge key={category.id} variant="secondary">
                {category.name}
              </Badge>
            ))
          ) : (
            <Badge variant="outline">未分类</Badge>
          )}
        </div>
        <div className="flex flex-wrap gap-x-5 gap-y-1 border-y py-3 text-sm text-muted-foreground">
          <span>作者：{article.author.nickname ?? "未设置昵称"}</span>
          <span>最后编辑：{article.lastEditor.nickname ?? "未设置昵称"}</span>
          <span>发布：{formatDateTime(article.publishedAt)}</span>
        </div>
        {article.coverMedia ? (
          <img
            className="max-h-120 w-full rounded-lg object-contain"
            src={article.coverMedia.url}
            alt={`${article.title} 封面`}
          />
        ) : null}
        <ArticleHtmlPreview canonicalHtml={article.contentHtml} />
      </article>
      {pendingAction ? (
        <ArticleActionDialog
          action={pendingAction}
          article={article}
          onClose={() => setPendingAction(null)}
          onDone={(message) => {
            setNotice(message);
            refetch();
          }}
          onConflict={refetch}
        />
      ) : null}
    </div>
  );
}

export default ArticlePreview;
