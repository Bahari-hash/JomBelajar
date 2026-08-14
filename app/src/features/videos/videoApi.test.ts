import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { setSession, clearSession } from "@/features/auth/sessionStore";
import { updateVideoProgress, videoApi } from "@/features/videos/videoApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
const VIDEO_ID = "11111111-2222-3333-4444-555555555555";

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
  clearSession();
});

describe("videoApi", () => {
  it("uses authenticated catalog contracts and does not request playback", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      return {
        data: {
          items: [
            {
              id: VIDEO_ID,
              title: "Listening lesson",
              description: "Practice in context.",
              durationSeconds: 90,
              author: {
                id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                nickname: "Video Publisher",
                avatarUrl: "https://media.example.test/avatar.jpg",
              },
              publishedAt: "2026-08-06T08:00:00Z",
              categories: [],
            },
          ],
          page: 1,
          pageSize: 12,
          totalCount: 0,
          totalPages: 0,
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
      user: { id: "user", email: "user@test", role: "User" },
    });
    const store = createAppStore();
    const response = await store
      .dispatch(
        videoApi.endpoints.getVideos.initiate({
          page: 2,
          pageSize: 12,
          keyword: "listen",
          categoryId: VIDEO_ID,
        }),
      )
      .unwrap();
    expect(response.items[0]?.author).toEqual({
      id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      nickname: "Video Publisher",
      avatarUrl: "https://media.example.test/avatar.jpg",
    });
    expect(requests[0]).toMatchObject({
      url: "/videos",
      method: "get",
      params: {
        page: 2,
        pageSize: 12,
        keyword: "listen",
        categoryId: VIDEO_ID,
      },
    });
    expect(requests[0]?.headers.Authorization).toBe("Bearer access");
    expect(requests.some((request) => request.url?.includes("/playback"))).toBe(
      false,
    );
  });

  it("sends progress with the exact body and authenticated client", async () => {
    let request: InternalAxiosRequestConfig | undefined;
    httpClient.defaults.adapter = (async (config) => {
      request = config;
      return {
        data: undefined,
        status: 204,
        statusText: "No Content",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    setSession({
      token: "access",
      expiresIn: 300,
      user: { id: "user", email: "user@test", role: "User" },
    });
    await updateVideoProgress(VIDEO_ID, 42.5);
    expect(request).toMatchObject({
      url: `/videos/${VIDEO_ID}/progress`,
      method: "put",
      data: JSON.stringify({ positionSeconds: 42.5 }),
    });
    expect(request?.headers.Authorization).toBe("Bearer access");
  });
});
