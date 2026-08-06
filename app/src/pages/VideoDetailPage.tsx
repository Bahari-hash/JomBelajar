import { ArrowLeft, CalendarDays, Clock3, RefreshCw } from "lucide-react";
import { Link, useLocation, useParams } from "react-router-dom";
import UserAvatar from "@/components/UserAvatar";
import VideoPlayer from "@/features/videos/VideoPlayer";
import { useGetVideoQuery } from "@/features/videos/videoApi";
import { isVideoGuid } from "@/features/videos/videoSearchParams";
import {
  formatVideoDate,
  formatVideoDuration,
  getVideoErrorMessage,
  isVideoNotFoundError,
} from "@/features/videos/videoUtils";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";
import styles from "@/pages/VideoDetailPage.module.css";

function getSafeReturnPath(state: unknown) {
  if (typeof state !== "object" || state === null || !("from" in state))
    return "/videos";
  const from = state.from;
  return typeof from === "string" && /^\/videos(?:\?[^#]*)?$/.test(from)
    ? from
    : "/videos";
}

function Skeleton() {
  return (
    <div
      aria-label="视频详情加载中"
      className="mx-auto max-w-5xl space-y-6"
      role="status"
    >
      <div className="skeleton h-5 w-28" />
      <div className="skeleton aspect-video w-full" />
      <div className="skeleton h-10 w-4/5" />
      <div className="skeleton h-5 w-full max-w-2xl" />
    </div>
  );
}

/** Presents authenticated video metadata and requests playback only on this detail route. */
export default function VideoDetailPage() {
  const { videoId } = useParams();
  const location = useLocation();
  const validId = isVideoGuid(videoId) ? videoId : null;
  const query = useGetVideoQuery(validId ?? "", { skip: !validId });
  const notFound = !validId || isVideoNotFoundError(query.error);
  const returnPath = getSafeReturnPath(location.state);
  useDocumentTitle(query.data?.title ?? (notFound ? "视频不存在" : "视频详情"));
  if (notFound)
    return (
      <section className="mx-auto max-w-xl py-14 text-center">
        <p className="text-sm font-semibold text-error">404</p>
        <h1 className="mt-2 text-2xl font-bold">视频不存在或已下架</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          该视频当前无法访问，你可以返回视频列表继续浏览。
        </p>
        <Link className="btn btn-primary mt-6" to={returnPath}>
          <ArrowLeft aria-hidden="true" className="size-4" />
          返回视频列表
        </Link>
      </section>
    );
  if (query.isLoading) return <Skeleton />;
  if (query.isError || !query.data)
    return (
      <section className="mx-auto max-w-xl py-14 text-center" role="alert">
        <h1 className="text-2xl font-bold">视频加载失败</h1>
        <p className="mt-3 leading-7 text-base-content/65">
          {getVideoErrorMessage(query.error)}
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => query.refetch()}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重新加载
          </button>
          <Link className="btn btn-ghost" to={returnPath}>
            返回视频列表
          </Link>
        </div>
      </section>
    );
  const video = query.data;
  const authorName = video.author.nickname?.trim() || "TinyLang 编辑";
  return (
    <div className="mx-auto max-w-5xl">
      <Link className="btn btn-ghost btn-sm -ml-3" to={returnPath}>
        <ArrowLeft aria-hidden="true" className="size-4" />
        返回视频列表
      </Link>
      <article className={styles.videoCard}>
        <header className="flex flex-col gap-4">
          <div className="flex flex-wrap gap-2">
            {video.categories.map((category) => (
              <Link
                key={category.id}
                className="badge badge-outline hover:bg-base-200"
                to={`/videos?categoryId=${category.id}`}
              >
                {category.name}
              </Link>
            ))}
          </div>
          <h1 className="text-3xl font-bold leading-tight sm:text-4xl">
            {video.title}
          </h1>
          {video.description?.trim() ? (
            <p className="max-w-3xl text-lg leading-8 text-base-content/70">
              {video.description}
            </p>
          ) : null}
        </header>
        <div className="mt-6 flex flex-wrap items-center gap-x-5 gap-y-3 border-y border-base-300 py-4 text-sm">
          <div className="flex items-center gap-2.5">
            <UserAvatar
              className="size-9"
              name={authorName}
              url={video.author.avatarUrl}
            />
            <span className="font-medium">{authorName}</span>
          </div>
          <span className="inline-flex items-center gap-1.5">
            <CalendarDays aria-hidden="true" className="size-4" />
            <time dateTime={video.publishedAt}>
              发布于 {formatVideoDate(video.publishedAt)}
            </time>
          </span>
          <span className="inline-flex items-center gap-1.5 text-base-content/65">
            <Clock3 aria-hidden="true" className="size-4" />
            {formatVideoDuration(video.durationSeconds)}
          </span>
          <span className="text-base-content/65">
            {video.originalLanguage || "语言未知"}
          </span>
        </div>
        <section
          aria-label="视频播放区域"
          className="mt-8 overflow-hidden rounded-lg border border-base-300 bg-black"
        >
          <VideoPlayer video={video} videoId={video.id} />
        </section>
      </article>
    </div>
  );
}
