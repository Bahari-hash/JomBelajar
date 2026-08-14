import { describe, expect, it } from "vitest";
import {
  normalizeAdminVideo,
  normalizeVideoPage,
} from "@/services/videoContracts.js";
import { videosApi } from "@/services/videosApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import {
  adminVideo,
  axiosResponse,
  mockHttpClient,
  videoListItem,
} from "@/test/http.js";

describe("videosApi", () => {
  it("encodes all administrator list filters", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [videoListItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      ),
    );
    const store = createAppStore();
    const request = store.dispatch(
      videosApi.endpoints.getAdminVideos.initiate({
        page: 2,
        pageSize: 20,
        keyword: "French",
        processingStatus: "Ready",
        publicationStatus: "Draft",
        categoryId: "77777777-7777-4777-8777-777777777777",
        createdById: "11111111-1111-4111-8111-111111111111",
      }),
    );
    await expect(request.unwrap()).resolves.toMatchObject({
      items: [{ processingStatus: "Ready", publicationStatus: "Draft" }],
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/videos?page=2&pageSize=20&keyword=French&processingStatus=Ready&publicationStatus=Draft&categoryId=77777777-7777-4777-8777-777777777777&createdById=11111111-1111-4111-8111-111111111111",
    );
    request.unsubscribe();
  });

  it("uses exact create, update, state mutation and playback contracts", async () => {
    tokenVault.install("access", "refresh");
    const response = adminVideo();
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/playback")
            ? {
                masterPlaylistUrl: "https://media.example.test/master.m3u8",
                posterUrl: null,
                expiresAt: null,
                durationSeconds: 42.5,
                positionSeconds: 0,
                isCompleted: false,
              }
            : response,
          config.method === "POST" && config.url === "/admin/videos"
            ? 201
            : 200,
        ),
      ),
    );
    const store = createAppStore();
    const videoId = response.id;
    const metadata = {
      title: "French greetings",
      description: "Basic phrases",
      categoryIds: ["77777777-7777-4777-8777-777777777777"],
    };
    await store
      .dispatch(
        videosApi.endpoints.createVideo.initiate({
          sourceMediaResourceId: response.sourceMediaResourceId,
          coverMediaResourceId: null,
          ...metadata,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videosApi.endpoints.updateVideo.initiate({
          videoId,
          ...metadata,
          coverAction: "Keep",
          coverMediaResourceId: null,
          concurrencyStamp: response.concurrencyStamp,
        }),
      )
      .unwrap();
    for (const endpoint of [
      "publishVideo",
      "unpublishVideo",
      "retryVideo",
      "archiveVideo",
    ]) {
      await store
        .dispatch(
          videosApi.endpoints[endpoint].initiate({
            videoId,
            concurrencyStamp: response.concurrencyStamp,
          }),
        )
        .unwrap();
    }
    await store
      .dispatch(videosApi.endpoints.getVideoPlayback.initiate(videoId))
      .unwrap();

    expect(
      requestMock.mock.calls.map(([config]) => ({
        url: config.url,
        method: config.method,
        data: config.data,
      })),
    ).toEqual([
      {
        url: "/admin/videos",
        method: "POST",
        data: {
          sourceMediaResourceId: response.sourceMediaResourceId,
          coverMediaResourceId: null,
          ...metadata,
        },
      },
      {
        url: `/admin/videos/${videoId}`,
        method: "PUT",
        data: {
          ...metadata,
          coverAction: "Keep",
          coverMediaResourceId: null,
          concurrencyStamp: response.concurrencyStamp,
        },
      },
      ...["publish", "unpublish", "retry", "archive"].map((action) => ({
        url: `/admin/videos/${videoId}/${action}`,
        method: "POST",
        data: { concurrencyStamp: response.concurrencyStamp },
      })),
      {
        url: `/admin/videos/${videoId}/playback`,
        method: "POST",
        data: undefined,
      },
    ]);
  });

  it("accepts every canonical ASP.NET Guid value in list contracts", () => {
    const response = {
      items: [
        videoListItem({
          id: "0198b9f2-01a2-7def-8123-0123456789ab",
          concurrencyStamp: "00000000-0000-0000-0000-000000000000",
        }),
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    };

    expect(normalizeVideoPage(response)).toMatchObject({
      items: [
        {
          id: "0198b9f2-01a2-7def-8123-0123456789ab",
          concurrencyStamp: "00000000-0000-0000-0000-000000000000",
        },
      ],
    });
  });

  it("rejects obsolete subtitle and unknown enum contracts without RTK noise", async () => {
    expect(() =>
      normalizeAdminVideo(adminVideo({ processingStatus: "Transcoding" })),
    ).toThrow("API returned invalid video processing status.");
    expect(() => normalizeAdminVideo(adminVideo({ subtitles: [] }))).toThrow(
      "API returned invalid video details contract.",
    );

    tokenVault.install("access", "refresh");
    const store = createAppStore();
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(adminVideo({ processingStatus: "Transcoding" })),
      ),
    );
    await expect(
      store
        .dispatch(
          videosApi.endpoints.getAdminVideo.initiate(
            "88888888-8888-4888-8888-888888888888",
          ),
        )
        .unwrap(),
    ).rejects.toMatchObject({
      status: "CUSTOM_ERROR",
      kind: "contract",
    });
  });
});
