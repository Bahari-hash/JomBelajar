import { LoaderCircle, Pause, Volume2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toApiRequestError } from "@/features/auth/authErrors";
import { requestAudioPlayback } from "@/features/audio/audioPlayback";

interface AudioPlaybackButtonProps {
  audioResourceId: string;
  label?: string;
  variant?: "default" | "icon" | "player";
}

type PlaybackStatus = "idle" | "loading" | "playing";

function validDuration(value: number) {
  return Number.isFinite(value) && value > 0 ? value : 0;
}

function validCurrentTime(value: number, duration: number) {
  if (!Number.isFinite(value) || value < 0) return 0;
  return duration > 0 ? Math.min(value, duration) : value;
}

function formatTime(value: number) {
  const seconds = Math.floor(Math.max(0, Number.isFinite(value) ? value : 0));
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

/** Plays one short-lived shared audio grant without caching its URL. */
export default function AudioPlaybackButton({
  audioResourceId,
  label,
  variant = "default",
}: AudioPlaybackButtonProps) {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const requestControllerRef = useRef<AbortController | null>(null);
  const operationRef = useRef(0);
  const [status, setStatus] = useState<PlaybackStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const accessibleLabel = label?.trim() || "朗读";
  const playerMode = variant === "player";

  useEffect(() => {
    operationRef.current += 1;
    setStatus("idle");
    setError(null);
    setCurrentTime(0);
    setDuration(0);
    return () => {
      operationRef.current += 1;
      requestControllerRef.current?.abort();
      requestControllerRef.current = null;
      const audio = audioRef.current;
      if (audio) {
        audio.pause();
        audio.onloadedmetadata = null;
        audio.ondurationchange = null;
        audio.ontimeupdate = null;
        audio.onended = null;
        audio.onerror = null;
        audio.src = "";
        audioRef.current = null;
      }
    };
  }, [audioResourceId]);

  const releaseAudio = (audio: HTMLAudioElement) => {
    audio.pause();
    audio.onloadedmetadata = null;
    audio.ondurationchange = null;
    audio.ontimeupdate = null;
    audio.onended = null;
    audio.onerror = null;
    audio.src = "";
    if (audioRef.current === audio) audioRef.current = null;
    setCurrentTime(0);
    setDuration(0);
  };

  const handleToggle = async () => {
    if (status === "loading") return;
    const currentAudio = audioRef.current;
    if (currentAudio && status === "playing") {
      currentAudio.pause();
      setStatus("idle");
      return;
    }

    const operation = operationRef.current + 1;
    operationRef.current = operation;
    setStatus("loading");
    setError(null);
    let controller: AbortController | null = null;
    try {
      let audio = audioRef.current;
      if (!audio) {
        controller = new AbortController();
        requestControllerRef.current = controller;
        const playback = await requestAudioPlayback(
          audioResourceId,
          controller.signal,
        );
        if (controller.signal.aborted || operationRef.current !== operation)
          return;
        const createdAudio = new Audio(playback.url);
        const fallbackDuration = validDuration(playback.durationSeconds);
        setDuration(fallbackDuration);
        const syncDuration = () => {
          if (audioRef.current !== createdAudio) return;
          const nextDuration =
            validDuration(createdAudio.duration) || fallbackDuration;
          setDuration(nextDuration);
          setCurrentTime((value) => validCurrentTime(value, nextDuration));
        };
        createdAudio.onloadedmetadata = syncDuration;
        createdAudio.ondurationchange = syncDuration;
        createdAudio.ontimeupdate = () => {
          if (audioRef.current !== createdAudio) return;
          const nextDuration =
            validDuration(createdAudio.duration) || fallbackDuration;
          setCurrentTime(
            validCurrentTime(createdAudio.currentTime, nextDuration),
          );
        };
        createdAudio.onended = () => {
          if (audioRef.current !== createdAudio) return;
          createdAudio.currentTime = 0;
          setCurrentTime(0);
          setStatus("idle");
        };
        createdAudio.onerror = () => {
          if (audioRef.current !== createdAudio) return;
          releaseAudio(createdAudio);
          setStatus("idle");
          setError("音频播放失败，请重试。");
        };
        audioRef.current = createdAudio;
        audio = createdAudio;
      }

      await audio.play();
      if (operationRef.current === operation) setStatus("playing");
    } catch (requestError) {
      if (controller?.signal.aborted || operationRef.current !== operation)
        return;
      if (audioRef.current) releaseAudio(audioRef.current);
      const apiError = toApiRequestError(
        requestError,
        "音频加载失败，请重试。",
      );
      setStatus("idle");
      setError(
        apiError.code === "AudioNotReady"
          ? "音频暂时不可用，请稍后再试。"
          : apiError.message,
      );
    } finally {
      if (requestControllerRef.current === controller)
        requestControllerRef.current = null;
    }
  };

  const playing = status === "playing";
  const actionLabel = `${playing ? "暂停" : "播放"}${accessibleLabel}`;
  const iconOnly = variant === "icon";
  const handleSeek = (value: string) => {
    const audio = audioRef.current;
    if (!audio || duration <= 0) return;
    const nextTime = validCurrentTime(Number(value), duration);
    audio.currentTime = nextTime;
    setCurrentTime(nextTime);
  };
  return (
    <div
      className={
        playerMode
          ? "flex w-full flex-col gap-2 sm:flex-row sm:items-center sm:gap-3"
          : "flex flex-wrap items-center gap-3"
      }
    >
      <button
        type="button"
        className={
          iconOnly
            ? "btn btn-ghost btn-sm btn-square"
            : playerMode
              ? "btn btn-outline btn-sm shrink-0"
              : "btn btn-outline btn-sm mt-4"
        }
        aria-label={actionLabel}
        title={iconOnly ? actionLabel : undefined}
        disabled={status === "loading"}
        onClick={() => void handleToggle()}
      >
        {status === "loading" ? (
          <LoaderCircle aria-hidden="true" className="size-4 animate-spin" />
        ) : playing ? (
          <Pause aria-hidden="true" className="size-4" />
        ) : (
          <Volume2 aria-hidden="true" className="size-4" />
        )}
        {iconOnly ? null : playing ? "暂停朗读" : "播放朗读"}
      </button>
      {playerMode ? (
        <div className="flex min-w-0 flex-1 items-center gap-3">
          <input
            type="range"
            className="range range-primary range-xs min-w-0 flex-1"
            aria-label={`${accessibleLabel}进度`}
            min="0"
            max={duration || 0}
            step="0.1"
            value={validCurrentTime(currentTime, duration)}
            disabled={status === "loading" || duration <= 0}
            onChange={(event) => handleSeek(event.currentTarget.value)}
          />
          <span className="w-24 shrink-0 text-right text-xs tabular-nums text-base-content/65">
            {formatTime(currentTime)} / {formatTime(duration)}
          </span>
        </div>
      ) : null}
      {error ? (
        <p className="text-sm text-error" role="status">
          {error}
        </p>
      ) : null}
    </div>
  );
}
