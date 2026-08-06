import { useEffect, useRef, useState } from "react";
import type Hls from "hls.js";
import { RefreshCw } from "lucide-react";
import { requestVideoPlayback } from "@/features/videos/videoApi";
import HlsAxiosLoader from "@/features/videos/hlsAxiosLoader";
import type { VideoDetails } from "@/features/videos/videoTypes";
import {
  getVideoErrorMessage,
  isSafeVideoUrl,
} from "@/features/videos/videoUtils";
import { useVideoProgress } from "@/features/videos/useVideoProgress";

interface VideoPlayerProps {
  videoId: string;
  video: VideoDetails;
}

function refreshDelay(expiresAt: string | null) {
  if (!expiresAt) return null;
  const ttl = new Date(expiresAt).getTime() - Date.now();
  if (!Number.isFinite(ttl) || ttl <= 0) return 0;
  return Math.max(5000, Math.min(30000, Math.floor(ttl / 2)));
}

function getVideoAspectRatio(width: number, height: number) {
  const ratio = width > 0 && height > 0 ? width / height : 16 / 9;
  return Math.min(2.4, Math.max(0.5, Number.isFinite(ratio) ? ratio : 16 / 9));
}

/** Loads a short-lived playback grant and renders native HLS or hls.js playback. */
export default function VideoPlayer({ videoId, video }: VideoPlayerProps) {
  const mediaRef = useRef<HTMLVideoElement>(null);
  const requestRef = useRef<AbortController | null>(null);
  const refreshRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [playback, setPlayback] = useState<Awaited<
    ReturnType<typeof requestVideoPlayback>
  > | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [reloadKey, setReloadKey] = useState(0);
  const { recordPosition, flushPosition, flushPositionRef } = useVideoProgress(
    videoId,
    playback?.positionSeconds ?? 0,
    Boolean(playback),
  );

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    setError(null);
    setPlayback(null);
    requestRef.current?.abort();
    const controller = new AbortController();
    requestRef.current = controller;
    requestVideoPlayback(videoId, controller.signal)
      .then((value) => {
        if (!disposed) setPlayback(value);
      })
      .catch((cause: unknown) => {
        if (
          !disposed &&
          !(cause instanceof DOMException && cause.name === "AbortError")
        )
          setError(
            getVideoErrorMessage(cause, "视频播放地址获取失败，请稍后重试。"),
          );
      })
      .finally(() => {
        if (!disposed) setLoading(false);
      });
    return () => {
      disposed = true;
      controller.abort();
    };
  }, [reloadKey, videoId]);

  useEffect(() => {
    const media = mediaRef.current;
    if (!media || !playback || !isSafeVideoUrl(playback.masterPlaylistUrl)) {
      if (playback && !isSafeVideoUrl(playback.masterPlaylistUrl))
        setError("视频播放地址无效，请稍后重试。");
      return;
    }
    let disposed = false;
    let hls: Hls | null = null;
    const flushLatest = flushPositionRef.current;
    const restorePosition = () => {
      const target = Math.max(0, playback.positionSeconds);
      if (
        Number.isFinite(target) &&
        target > 0 &&
        (!media.duration || target <= media.duration)
      )
        media.currentTime = target;
    };
    const setup = async () => {
      const native = media.canPlayType("application/vnd.apple.mpegurl") !== "";
      if (native) {
        media.src = playback.masterPlaylistUrl;
        media.addEventListener("loadedmetadata", restorePosition, {
          once: true,
        });
        return;
      }
      const module = await import("hls.js");
      if (disposed) return;
      const HlsConstructor = module.default;
      if (!HlsConstructor.isSupported()) {
        setError("当前浏览器不支持视频播放。");
        return;
      }
      hls = new HlsConstructor({ loader: HlsAxiosLoader });
      hls.attachMedia(media);
      hls.on(HlsConstructor.Events.MEDIA_ATTACHED, () =>
        hls?.loadSource(playback.masterPlaylistUrl),
      );
      hls.on(HlsConstructor.Events.MANIFEST_PARSED, restorePosition);
      hls.on(HlsConstructor.Events.ERROR, (_event, data) => {
        if (data.fatal) setError("视频加载失败，请重新加载。");
      });
    };
    void setup();
    return () => {
      disposed = true;
      if (Number.isFinite(media.currentTime)) flushLatest(media.currentTime);
      hls?.destroy();
      media.removeAttribute("src");
      media.load();
    };
  }, [flushPositionRef, playback]);

  useEffect(() => {
    if (!playback) return;
    if (refreshRef.current) clearTimeout(refreshRef.current);
    const delay = refreshDelay(playback.expiresAt);
    if (delay === null) return;
    refreshRef.current = setTimeout(() => {
      requestRef.current?.abort();
      const controller = new AbortController();
      requestRef.current = controller;
      requestVideoPlayback(videoId, controller.signal)
        .then(setPlayback)
        .catch(() => setError("播放地址已失效，请重新加载视频。"));
    }, delay);
    return () => {
      if (refreshRef.current) clearTimeout(refreshRef.current);
    };
  }, [playback, videoId]);

  if (loading)
    return (
      <div
        aria-label="视频加载中"
        className="skeleton aspect-video w-full"
        role="status"
      />
    );
  if (error)
    return (
      <div
        className="grid aspect-video place-items-center border border-base-300 bg-base-200 p-6 text-center"
        role="alert"
      >
        <div>
          <p className="font-semibold">{error}</p>
          <button
            className="btn btn-primary btn-sm mt-4"
            type="button"
            onClick={() => {
              setError(null);
              setReloadKey((value) => value + 1);
            }}
          >
            <RefreshCw aria-hidden="true" className="size-4" />
            重新加载
          </button>
        </div>
      </div>
    );
  if (!playback) return null;
  const poster = isSafeVideoUrl(playback.posterUrl)
    ? (playback.posterUrl ?? undefined)
    : undefined;
  return (
    <video
      ref={mediaRef}
      aria-label={`播放《${video.title}》`}
      className="h-auto w-full bg-black"
      controls
      poster={poster}
      style={{
        aspectRatio: getVideoAspectRatio(
          video.displayWidth,
          video.displayHeight,
        ),
      }}
      onError={() => setError("视频媒体加载失败，请稍后重试。")}
      onLoadedMetadata={(event) => {
        const media = event.currentTarget;
        if (playback.positionSeconds > 0 && Number.isFinite(media.duration))
          media.currentTime = Math.min(
            playback.positionSeconds,
            media.duration,
          );
      }}
      onTimeUpdate={(event) => recordPosition(event.currentTarget.currentTime)}
      onPause={(event) => flushPosition(event.currentTarget.currentTime)}
      onEnded={(event) => flushPosition(event.currentTarget.duration)}
    />
  );
}
