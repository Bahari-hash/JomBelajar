import { httpClient } from "@/services/httpClient";

export interface AudioPlayback {
  url: string;
  expiresAt: string | null;
  durationSeconds: number;
}

/** Requests one short-lived shared audio playback grant. */
export async function requestAudioPlayback(
  audioResourceId: string,
  signal?: AbortSignal,
) {
  const response = await httpClient.post<AudioPlayback>(
    `/audio/${audioResourceId}/playback`,
    undefined,
    { signal },
  );
  return response.data;
}
