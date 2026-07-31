import { useEffect, useRef, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert.jsx";
import { Button } from "@/components/ui/button.jsx";
import { AxiosHlsLoader } from "@/services/axiosHlsLoader.js";

/** Native-HLS-first video preview with a lazy hls.js fallback. */
export function VideoPlayer({ playback, onRefresh }) {
  const videoRef = useRef(null);
  const [error, setError] = useState(null);
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return undefined;
    let disposed = false;
    let hls;
    setError(null);
    if (playback.posterUrl) video.poster = playback.posterUrl;
    if (video.canPlayType("application/vnd.apple.mpegurl")) {
      video.src = playback.masterPlaylistUrl;
    } else {
      import("hls.js/dist/hls.light.min.js")
        .then(({ default: Hls }) => {
          if (disposed) return;
          if (!Hls.isSupported()) {
            setError("当前浏览器不支持 HLS/MSE 视频预览。");
            return;
          }
          hls = new Hls({ loader: AxiosHlsLoader, enableWorker: true });
          hls.on(Hls.Events.ERROR, (_event, data) => {
            if (data.fatal)
              setError(
                data.type === Hls.ErrorTypes.NETWORK_ERROR
                  ? "视频网络请求失败，请检查媒体 CORS 后重试。"
                  : "视频无法解码或播放。",
              );
          });
          hls.loadSource(playback.masterPlaylistUrl);
          hls.attachMedia(video);
        })
        .catch(() => setError("播放器加载失败，请重试。"));
    }
    return () => {
      disposed = true;
      hls?.destroy();
      video.pause();
      video.removeAttribute("src");
      video.load();
    };
  }, [playback.masterPlaylistUrl, playback.posterUrl]);

  useEffect(() => {
    if (!playback.expiresAt) return undefined;
    const refreshAt = new Date(playback.expiresAt).getTime() - 30_000;
    const timeout = Math.max(0, refreshAt - Date.now());
    const timer = window.setTimeout(() => {
      if (videoRef.current && !videoRef.current.paused) onRefresh();
    }, timeout);
    return () => window.clearTimeout(timer);
  }, [onRefresh, playback.expiresAt]);

  return (
    <div className="space-y-3">
      <video
        ref={videoRef}
        controls
        crossOrigin="anonymous"
        preload="metadata"
        className="aspect-video w-full max-w-4xl bg-black"
        onError={() => setError("视频加载失败，请检查格式、网络或跨域配置。")}
      >
        当前浏览器不支持视频播放。
      </video>
      {error ? (
        <Alert variant="destructive">
          <AlertDescription className="flex items-center justify-between gap-3">
            <span>{error}</span>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onRefresh}
            >
              重试
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}
    </div>
  );
}
