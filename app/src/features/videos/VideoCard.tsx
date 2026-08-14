import { useState } from "react";
import { CalendarDays, Play } from "lucide-react";
import { Link } from "react-router-dom";
import UserAvatar from "@/components/UserAvatar";
import type { VideoCatalogItem } from "@/features/videos/videoTypes";
import {
  formatVideoDate,
  formatVideoDuration,
  isSafeVideoUrl,
} from "@/features/videos/videoUtils";

interface VideoCardProps {
  video: VideoCatalogItem;
  listPath: string;
}

/** Presents catalog metadata without requesting a playback grant. */
export default function VideoCard({ video, listPath }: VideoCardProps) {
  const [coverFailed, setCoverFailed] = useState(false);
  const coverUrl =
    !coverFailed && isSafeVideoUrl(video.coverUrl) ? video.coverUrl : null;
  const authorName = video.author.nickname?.trim() || "TinyLang 编辑";
  const visibleCategories = video.categories.slice(0, 3);
  const remaining = Math.max(
    0,
    video.categories.length - visibleCategories.length,
  );

  return (
    <article className="flex min-w-0 flex-col overflow-hidden rounded-lg border border-base-300 bg-base-100">
      <Link
        aria-label={`观看《${video.title}》`}
        className="group relative block aspect-video bg-base-300"
        state={{ from: listPath }}
        to={`/videos/${video.id}`}
      >
        {coverUrl ? (
          <img
            src={coverUrl}
            alt={`《${video.title}》封面`}
            className="absolute inset-0 h-full w-full object-cover"
            onError={() => setCoverFailed(true)}
          />
        ) : null}
        <div
          className={`absolute inset-0 grid place-items-center text-base-content/70 transition-colors ${coverUrl ? "bg-black/15 group-hover:bg-black/25 group-focus-visible:bg-black/25" : "bg-base-300 group-hover:bg-base-200 group-focus-visible:bg-base-200"}`}
        >
          <Play aria-hidden="true" className="size-12 fill-current" />
        </div>
        <span className="absolute bottom-2 right-2 rounded bg-neutral px-2 py-1 text-xs text-neutral-content">
          {formatVideoDuration(video.durationSeconds)}
        </span>
      </Link>
      <div className="flex flex-1 flex-col p-4 sm:p-5">
        <div className="flex min-h-7 flex-wrap gap-1.5">
          {visibleCategories.map((category) => (
            <Link
              key={category.id}
              className="badge badge-outline max-w-full truncate hover:bg-base-200"
              title={category.name}
              to={`/videos?categoryId=${category.id}`}
            >
              {category.name}
            </Link>
          ))}
          {remaining ? (
            <span className="badge badge-ghost">+{remaining} 个</span>
          ) : null}
        </div>
        <h2 className="mt-3 text-lg font-bold leading-7">
          <Link
            className="hover:underline"
            state={{ from: listPath }}
            to={`/videos/${video.id}`}
          >
            {video.title}
          </Link>
        </h2>
        <p className="mt-2 line-clamp-3 min-h-18 text-sm leading-6 text-base-content/70">
          {video.description?.trim() || "这个视频暂未提供简介。"}
        </p>
        <div className="mt-5 flex items-center gap-3 border-t border-base-300 pt-4">
          <UserAvatar
            className="size-9"
            name={authorName}
            url={video.author.avatarUrl}
          />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{authorName}</p>
            <p className="mt-0.5 flex items-center gap-1.5 text-xs text-base-content/60">
              <CalendarDays aria-hidden="true" className="size-3.5" />
              <time dateTime={video.publishedAt}>
                {formatVideoDate(video.publishedAt)}
              </time>
            </p>
          </div>
        </div>
      </div>
    </article>
  );
}
