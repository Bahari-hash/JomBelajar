import { LoaderCircle, Pause, Volume2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toApiRequestError } from "@/features/auth/authErrors";
import { requestAudioPlayback } from "@/features/audio/audioPlayback";

interface AudioPlaybackButtonProps {
  audioResourceId: string;
  label?: string;
  variant?: "default" | "icon";
}

type PlaybackStatus = "idle" | "loading" | "playing";

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
  const accessibleLabel = label?.trim() || "朗读";

  useEffect(() => {
    operationRef.current += 1;
    setStatus("idle");
    setError(null);
    return () => {
      operationRef.current += 1;
      requestControllerRef.current?.abort();
      requestControllerRef.current = null;
      const audio = audioRef.current;
      if (audio) {
        audio.pause();
        audio.onended = null;
        audio.onerror = null;
        audio.src = "";
        audioRef.current = null;
      }
    };
  }, [audioResourceId]);

  const releaseAudio = (audio: HTMLAudioElement) => {
    audio.pause();
    audio.onended = null;
    audio.onerror = null;
    audio.src = "";
    if (audioRef.current === audio) audioRef.current = null;
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
        createdAudio.onended = () => {
          if (audioRef.current === createdAudio) setStatus("idle");
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
  return (
    <div className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        className={
          iconOnly
            ? "btn btn-ghost btn-sm btn-square"
            : "btn btn-outline btn-sm"
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
      {error ? (
        <p className="text-sm text-error" role="status">
          {error}
        </p>
      ) : null}
    </div>
  );
}
