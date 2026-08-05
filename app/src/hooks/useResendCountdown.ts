import { useEffect, useState } from "react";

/** Provides a cancellable one-second countdown for verification-code resend controls. */
export function useResendCountdown(durationSeconds = 60) {
  const [seconds, setSeconds] = useState(0);

  useEffect(() => {
    if (seconds <= 0) {
      return;
    }
    const timer = window.setInterval(
      () => setSeconds((current) => Math.max(0, current - 1)),
      1000,
    );
    return () => window.clearInterval(timer);
  }, [seconds]);

  return {
    seconds,
    start: () => setSeconds(durationSeconds),
  };
}
