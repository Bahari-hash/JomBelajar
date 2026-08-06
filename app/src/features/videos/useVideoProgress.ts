import { useEffect, useRef } from "react";
import { ApiRequestError } from "@/features/auth/authErrors";
import { updateVideoProgress } from "@/features/videos/videoApi";

const REPORT_INTERVAL_MS = 15000;
const MIN_POSITION_DELTA = 5;

/** Coordinates bounded, latest-position-wins progress writes for one video. */
export function useVideoProgress(
  videoId: string,
  initialPosition: number,
  enabled: boolean,
) {
  const stateRef = useRef({
    lastReported: initialPosition,
    pending: null as number | null,
    inFlight: false,
    timer: null as ReturnType<typeof setTimeout> | null,
    retryUsed: false,
    controller: null as AbortController | null,
  });
  const flushRef = useRef<(force?: boolean) => void>(() => undefined);
  const flushPositionRef = useRef<(position: number) => void>(() => undefined);
  const flush = (force = false) => {
    const state = stateRef.current;
    const position = state.pending;
    if (
      position === null ||
      state.inFlight ||
      (!force && Math.abs(position - state.lastReported) < MIN_POSITION_DELTA)
    )
      return;
    state.pending = null;
    state.inFlight = true;
    state.controller = new AbortController();
    updateVideoProgress(videoId, position, state.controller.signal)
      .then(() => {
        state.lastReported = position;
        state.retryUsed = false;
      })
      .catch((error: unknown) => {
        if (
          error instanceof ApiRequestError &&
          error.status === 429 &&
          !state.retryUsed
        ) {
          state.retryUsed = true;
          state.pending = position;
          const delay = Math.max(1000, (error.retryAfterSeconds ?? 5) * 1000);
          state.timer = setTimeout(() => {
            state.timer = null;
            flush(true);
          }, delay);
        } else if (!(
          error instanceof DOMException && error.name === "AbortError"
        )) {
          state.pending = position;
        }
      })
      .finally(() => {
        state.inFlight = false;
        state.controller = null;
        if (state.pending !== null) flush(false);
      });
  };
  const record = (position: number, force = false) => {
    if (!Number.isFinite(position) || position < 0) return;
    const state = stateRef.current;
    state.pending = position;
    if (force) flush(true);
    else if (!state.timer)
      state.timer = setTimeout(() => {
        state.timer = null;
        flush(false);
      }, REPORT_INTERVAL_MS);
  };
  flushRef.current = flush;
  flushPositionRef.current = (position) => record(position, true);
  useEffect(() => {
    const state = stateRef.current;
    state.lastReported =
      Number.isFinite(initialPosition) && initialPosition >= 0
        ? initialPosition
        : 0;
    state.pending = null;
    state.retryUsed = false;
    return () => {
      if (state.timer) clearTimeout(state.timer);
      state.timer = null;
      if (state.pending !== null && !state.inFlight) {
        flushRef.current(true);
      } else if (state.pending === null) {
        state.controller?.abort();
      }
    };
  }, [videoId, initialPosition, enabled]);
  return {
    recordPosition: (position: number) => record(position),
    flushPosition: (position: number) => record(position, true),
    flushPositionRef,
  };
}
