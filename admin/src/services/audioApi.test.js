import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const MEDIA_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const SESSION_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

function audioListItem(overrides = {}) {
  return {
    id: AUDIO_ID,
    name: "lesson.mp3",
    status: "Ready",
    durationSeconds: 42.5,
    lastFailureCode: null,
    updatedAt: "2026-08-16T08:00:00Z",
    ...overrides,
  };
}

function audioDetails(overrides = {}) {
  return {
    ...audioListItem(),
    sampleRate: 44100,
    channels: 2,
    containerFormat: "mp3",
    sourceCodec: "mp3",
    currentOutputVersion: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
    concurrencyStamp: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
    createdAt: "2026-08-16T07:00:00Z",
    ...overrides,
  };
}

function capability() {
  return {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [
      { extension: ".mp3", contentTypes: ["audio/mpeg"] },
      { extension: ".wav", contentTypes: ["audio/wav", "audio/x-wav"] },
    ],
    multipartThresholdBytes: 8 * 1024 * 1024,
    partSizeBytes: 5 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
  };
}

function initialization(overrides = {}) {
  return {
    audioResourceId: AUDIO_ID,
    mediaResourceId: MEDIA_ID,
    presignedUrl: "https://storage.example.test/lesson.mp3",
    multipartSessionId: null,
    partSize: null,
    partCount: null,
    expiresAt: "2026-08-16T08:15:00Z",
    ...overrides,
  };
}

function multipartStatus() {
  return {
    resourceId: MEDIA_ID,
    sessionId: SESSION_ID,
    status: "Completed",
    partSize: 5242880,
    partCount: 1,
    expiresAt: "2026-08-16T09:00:00Z",
    uploadedParts: [{ partNumber: 1, eTag: "etag-1", size: 4 }],
  };
}

describe("audio contracts", () => {
  it("normalizes the five resource states and paged list metadata", async () => {
    const { AUDIO_RESOURCE_STATUSES, normalizeAudioResourcePage } =
      await import("@/services/audioContracts.js");

    expect(AUDIO_RESOURCE_STATUSES).toEqual([
      "Uploading",
      "Queued",
      "Processing",
      "Ready",
      "Failed",
    ]);
    expect(
      normalizeAudioResourcePage({
        items: AUDIO_RESOURCE_STATUSES.map((status, index) =>
          audioListItem({
            id: AUDIO_ID.replace(/^./, String(index + 1)),
            status,
          }),
        ),
        page: 2,
        pageSize: 20,
        totalCount: 21,
        totalPages: 2,
      }),
    ).toMatchObject({
      items: AUDIO_RESOURCE_STATUSES.map((status) => ({ status })),
      page: 2,
      pageSize: 20,
      totalCount: 21,
      totalPages: 2,
    });
  });

  it("normalizes upload initialization, multipart status and playback", async () => {
    const {
      normalizeAudioPlayback,
      normalizeAudioUploadInitialization,
      normalizeMultipartStatus,
    } = await import("@/services/audioContracts.js");

    expect(
      normalizeAudioUploadInitialization({
        audioResourceId: AUDIO_ID,
        mediaResourceId: MEDIA_ID,
        presignedUrl: "https://storage.example.test/lesson.mp3",
        multipartSessionId: null,
        partSize: null,
        partCount: null,
        expiresAt: "2026-08-16T08:15:00Z",
      }),
    ).toMatchObject({
      audioResourceId: AUDIO_ID,
      mediaResourceId: MEDIA_ID,
      presignedUrl: "https://storage.example.test/lesson.mp3",
    });
    expect(
      normalizeMultipartStatus({
        resourceId: MEDIA_ID,
        sessionId: SESSION_ID,
        status: "Initiated",
        partSize: 5242880,
        partCount: 2,
        expiresAt: "2026-08-16T09:00:00Z",
        uploadedParts: [{ partNumber: 1, eTag: "etag-1", size: 5242880 }],
      }),
    ).toMatchObject({
      resourceId: MEDIA_ID,
      sessionId: SESSION_ID,
      uploadedParts: [{ partNumber: 1, eTag: "etag-1" }],
    });
    expect(
      normalizeAudioPlayback({
        url: "https://media.example.test/lesson.mp3",
        expiresAt: null,
        durationSeconds: 42.5,
      }),
    ).toEqual({
      url: "https://media.example.test/lesson.mp3",
      expiresAt: null,
      durationSeconds: 42.5,
    });
  });

  it("rejects unknown resource states", async () => {
    const { normalizeAudioResource } =
      await import("@/services/audioContracts.js");

    expect(() =>
      normalizeAudioResource(audioListItem({ status: "Published" })),
    ).toThrow("API returned invalid audio resource status.");
  });
});

describe("audioApi", () => {
  it("encodes administrator list filters and normalizes the response", async () => {
    const { audioApi } = await import("@/services/audioApi.js");
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [audioListItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      ),
    );
    const store = createAppStore();
    const request = store.dispatch(
      audioApi.endpoints.getAdminAudioResources.initiate({
        page: 2,
        pageSize: 20,
        keyword: "lesson",
        status: "Ready",
      }),
    );

    await expect(request.unwrap()).resolves.toMatchObject({
      items: [{ name: "lesson.mp3", status: "Ready" }],
      page: 2,
      totalPages: 2,
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/audio?page=2&pageSize=20&keyword=lesson&status=Ready",
    );
    request.unsubscribe();
  });

  it("uses the exact audio management, upload and playback contracts", async () => {
    const { audioApi } = await import("@/services/audioApi.js");
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/uploads/simple"))
        return Promise.resolve(axiosResponse(initialization(), 201));
      if (config.url.endsWith("/uploads/multipart"))
        return Promise.resolve(
          axiosResponse(
            initialization({
              presignedUrl: null,
              multipartSessionId: SESSION_ID,
              partSize: 5242880,
              partCount: 1,
            }),
            201,
          ),
        );
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse([
            {
              partNumber: 1,
              presignedUrl: "https://storage.example.test/part-1",
              contentLength: 4,
              expiresAt: "2026-08-16T08:15:00Z",
            },
          ]),
        );
      if (config.url.includes(`/multipart/${SESSION_ID}`))
        return Promise.resolve(
          axiosResponse(
            config.method === "DELETE" ? null : multipartStatus(),
            config.method === "DELETE" ? 204 : 200,
          ),
        );
      if (config.url.endsWith("/retry-upload"))
        return Promise.resolve(axiosResponse(initialization()));
      if (config.url.endsWith("/playback"))
        return Promise.resolve(
          axiosResponse({
            url: "https://media.example.test/lesson.mp3",
            expiresAt: null,
            durationSeconds: 42.5,
          }),
        );
      if (config.url.endsWith("/upload/confirm") || config.method === "DELETE")
        return Promise.resolve(axiosResponse(null, 204));
      return Promise.resolve(axiosResponse(audioDetails()));
    });
    const store = createAppStore();
    const file = new File(["wave"], "lesson.wav", { type: "audio/wav" });

    await store
      .dispatch(audioApi.endpoints.getAudioUploadCapability.initiate())
      .unwrap();
    const detailRequest = store.dispatch(
      audioApi.endpoints.getAdminAudioResource.initiate(AUDIO_ID),
    );
    await detailRequest.unwrap();
    detailRequest.unsubscribe();
    await store
      .dispatch(audioApi.endpoints.initializeSimpleAudioUpload.initiate(file))
      .unwrap();
    await store
      .dispatch(
        audioApi.endpoints.initializeMultipartAudioUpload.initiate(file),
      )
      .unwrap();
    await store
      .dispatch(audioApi.endpoints.confirmAudioUpload.initiate(AUDIO_ID))
      .unwrap();
    await store
      .dispatch(
        audioApi.endpoints.presignAudioMultipartParts.initiate({
          sessionId: SESSION_ID,
          partNumbers: [1],
        }),
      )
      .unwrap();
    const multipartRequest = store.dispatch(
      audioApi.endpoints.getAudioMultipartStatus.initiate(SESSION_ID),
    );
    await multipartRequest.unwrap();
    multipartRequest.unsubscribe();
    await store
      .dispatch(
        audioApi.endpoints.completeAudioMultipart.initiate({
          sessionId: SESSION_ID,
          parts: [{ partNumber: 1, eTag: "etag-1" }],
        }),
      )
      .unwrap();
    await store
      .dispatch(audioApi.endpoints.abortAudioMultipart.initiate(SESSION_ID))
      .unwrap();
    await store
      .dispatch(
        audioApi.endpoints.renameAudioResource.initiate({
          audioResourceId: AUDIO_ID,
          name: "renamed.mp3",
        }),
      )
      .unwrap();
    await store
      .dispatch(
        audioApi.endpoints.retryAudioUpload.initiate({
          audioResourceId: AUDIO_ID,
          file,
        }),
      )
      .unwrap();
    await store
      .dispatch(audioApi.endpoints.reprocessAudioResource.initiate(AUDIO_ID))
      .unwrap();
    await store
      .dispatch(audioApi.endpoints.deleteAudioResource.initiate(AUDIO_ID))
      .unwrap();
    await store
      .dispatch(audioApi.endpoints.getAudioPlayback.initiate(AUDIO_ID))
      .unwrap();

    const metadata = {
      originalName: "lesson.wav",
      extension: ".wav",
      contentType: "audio/wav",
      size: 4,
    };
    expect(
      requestMock.mock.calls.map(([config]) => ({
        url: config.url,
        method: config.method,
        data: config.data,
      })),
    ).toEqual([
      {
        url: "/uploads/admin/media/capabilities?module=Audio",
        method: "GET",
        data: undefined,
      },
      {
        url: `/admin/audio/${AUDIO_ID}`,
        method: "GET",
        data: undefined,
      },
      {
        url: "/admin/audio/uploads/simple",
        method: "POST",
        data: metadata,
      },
      {
        url: "/admin/audio/uploads/multipart",
        method: "POST",
        data: metadata,
      },
      {
        url: `/admin/audio/${AUDIO_ID}/upload/confirm`,
        method: "PUT",
        data: undefined,
      },
      {
        url: `/admin/audio/multipart/${SESSION_ID}/parts/presign`,
        method: "POST",
        data: { partNumbers: [1] },
      },
      {
        url: `/admin/audio/multipart/${SESSION_ID}`,
        method: "GET",
        data: undefined,
      },
      {
        url: `/admin/audio/multipart/${SESSION_ID}/complete`,
        method: "POST",
        data: { parts: [{ partNumber: 1, eTag: "etag-1" }] },
      },
      {
        url: `/admin/audio/multipart/${SESSION_ID}`,
        method: "DELETE",
        data: undefined,
      },
      {
        url: `/admin/audio/${AUDIO_ID}/name`,
        method: "PATCH",
        data: { name: "renamed.mp3" },
      },
      {
        url: `/admin/audio/${AUDIO_ID}/retry-upload`,
        method: "POST",
        data: metadata,
      },
      {
        url: `/admin/audio/${AUDIO_ID}/reprocess`,
        method: "POST",
        data: undefined,
      },
      {
        url: `/admin/audio/${AUDIO_ID}`,
        method: "DELETE",
        data: undefined,
      },
      {
        url: `/audio/${AUDIO_ID}/playback`,
        method: "POST",
        data: undefined,
      },
    ]);
  });
});
