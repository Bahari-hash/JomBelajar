import { Pause, Play, Volume2 } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { getErrorMessage } from "@/features/auth/authErrors";
import { requestWordAudio } from "@/features/wordStudy/wordStudyApi";

interface WordStudyAudioButtonProps {
  audioClipId: string;
  label: string;
}

/** Plays one short-lived word-study audio grant and cleans it up on unmount. */
export default function WordStudyAudioButton({
  audioClipId,
  label,
}: WordStudyAudioButtonProps) {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const requestControllerRef = useRef<AbortController | null>(null);
  const [status, setStatus] = useState<"idle" | "loading" | "playing">("idle");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      requestControllerRef.current?.abort();
      audioRef.current?.pause();
      audioRef.current = null;
    };
  }, [audioClipId]);

  const handleToggle = async () => {
    if (audioRef.current && status === "playing") {
      audioRef.current.pause();
      setStatus("idle");
      return;
    }

    setStatus("loading");
    setError(null);
    try {
      if (!audioRef.current) {
        const controller = new AbortController();
        requestControllerRef.current = controller;
        const playback = await requestWordAudio(audioClipId, controller.signal);
        const audio = new Audio(playback.url);
        audio.addEventListener("ended", () => setStatus("idle"));
        audio.addEventListener("error", () => {
          setStatus("idle");
          setError("音频播放失败，请重试。");
        });
        audioRef.current = audio;
      }
      await audioRef.current.play();
      setStatus("playing");
    } catch (requestError) {
      if (requestControllerRef.current?.signal.aborted) {
        return;
      }
      setStatus("idle");
      setError(getErrorMessage(requestError, "音频加载失败，请重试。"));
    }
  };

  return (
    <span className="inline-flex items-center gap-2">
      <button
        aria-label={status === "playing" ? `暂停${label}` : `播放${label}`}
        className="btn btn-ghost btn-sm btn-square"
        disabled={status === "loading"}
        title={status === "playing" ? `暂停${label}` : `播放${label}`}
        type="button"
        onClick={() => void handleToggle()}
      >
        {status === "loading" ? (
          <span className="loading loading-spinner loading-sm" />
        ) : status === "playing" ? (
          <Pause aria-hidden="true" className="size-4" />
        ) : label === "发音" ? (
          <Volume2 aria-hidden="true" className="size-4" />
        ) : (
          <Play aria-hidden="true" className="size-4" />
        )}
      </button>
      {error ? (
        <span className="text-xs text-error" role="status">
          {error}
        </span>
      ) : null}
    </span>
  );
}
