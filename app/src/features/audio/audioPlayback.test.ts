import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { requestAudioPlayback } from "@/features/audio/audioPlayback";
import { setSession } from "@/features/auth/sessionStore";
import { httpClient } from "@/services/httpClient";

const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("requestAudioPlayback", () => {
  it("requests the shared playback endpoint without caching the URL", async () => {
    let request: InternalAxiosRequestConfig | undefined;
    httpClient.defaults.adapter = (async (config) => {
      request = config;
      return {
        data: {
          url: "https://media.example/audio.mp3",
          expiresAt: null,
          durationSeconds: 12.5,
        },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    setSession({
      token: "access",
      expiresIn: 300,
      user: { id: "user-1", email: "user@example.test", role: "User" },
    });

    const playback = await requestAudioPlayback(AUDIO_ID);

    expect(request).toMatchObject({
      url: `/audio/${AUDIO_ID}/playback`,
      method: "post",
    });
    expect(request?.headers.Authorization).toBe("Bearer access");
    expect(playback).toEqual({
      url: "https://media.example/audio.mp3",
      expiresAt: null,
      durationSeconds: 12.5,
    });
  });
});
