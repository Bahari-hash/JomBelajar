import { describe, expect, it } from "vitest";
import { wordsApi } from "@/services/wordsApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const IDS = Object.freeze({
  word: "11111111-1111-4111-8111-111111111111",
  user: "22222222-2222-4222-8222-222222222222",
  stamp: "33333333-3333-4333-8333-333333333333",
  sense: "44444444-4444-4444-8444-444444444444",
  example: "55555555-5555-4555-8555-555555555555",
  pronunciation: "66666666-6666-4666-8666-666666666666",
  audio: "77777777-7777-4777-8777-777777777777",
  resource: "99999999-9999-4999-8999-999999999999",
});

function auditUser() {
  return { id: IDS.user, nickname: "管理员", avatarUrl: null };
}

function detail(overrides = {}) {
  return {
    id: IDS.word,
    headword: "bonjour",
    status: "Draft",
    createdBy: auditUser(),
    lastEditor: auditUser(),
    publishedAt: null,
    archivedAt: null,
    concurrencyStamp: IDS.stamp,
    senses: [
      {
        id: IDS.sense,
        partOfSpeech: "Interjection",
        definition: "你好",
        usageNote: null,
        sortOrder: 0,
        examples: [
          {
            id: IDS.example,
            sentence: "Bonjour, Marie!",
            translation: "你好，玛丽！",
            audioClipId: null,
            sortOrder: 0,
          },
        ],
      },
    ],
    pronunciations: [
      {
        id: IDS.pronunciation,
        audioClipId: IDS.audio,
        accentTag: "France",
        ipa: "bɔ̃.ʒuʁ",
        isDefault: true,
        sortOrder: 0,
      },
    ],
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T11:00:00Z",
    ...overrides,
  };
}

function listItem() {
  const value = detail();
  return {
    id: value.id,
    headword: value.headword,
    status: value.status,
    primaryPartOfSpeech: "Interjection",
    primaryDefinition: "你好",
    senseCount: 1,
    exampleCount: 1,
    pronunciationCount: 1,
    createdBy: value.createdBy,
    lastEditor: value.lastEditor,
    publishedAt: null,
    archivedAt: null,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
    concurrencyStamp: value.concurrencyStamp,
  };
}

function audioDetails(overrides = {}) {
  return {
    id: IDS.audio,
    sourceMediaResourceId: IDS.resource,
    title: "bonjour 发音",
    description: null,
    kind: "WordPronunciation",
    processingStatus: "Queued",
    publicationStatus: "Draft",
    durationSeconds: null,
    sampleRate: null,
    channels: null,
    containerFormat: null,
    sourceCodec: null,
    failureCode: null,
    publishedAt: null,
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T10:00:00Z",
    ...overrides,
  };
}

describe("wordsApi", () => {
  it("does not expose word batch validation or import endpoints", () => {
    expect(wordsApi.endpoints.validateWordBatch).toBeUndefined();
    expect(wordsApi.endpoints.importWordBatch).toBeUndefined();
  });

  it("encodes every supported list filter", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [listItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      ),
    );
    const store = createAppStore();
    const request = store.dispatch(
      wordsApi.endpoints.getAdminWords.initiate({
        page: 2,
        pageSize: 20,
        keyword: "bonjour",
        status: "Draft",
        partOfSpeech: "Interjection",
        definition: "你好",
      }),
    );
    await expect(request.unwrap()).resolves.toMatchObject({
      items: [{ headword: "bonjour" }],
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/words?page=2&pageSize=20&keyword=bonjour&status=Draft&partOfSpeech=Interjection&definition=%E4%BD%A0%E5%A5%BD",
    );
    request.unsubscribe();
  });

  it("uses exact aggregate, lifecycle and delete contracts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.method === "DELETE")
        return Promise.resolve(axiosResponse(undefined, 204));
      return Promise.resolve(
        axiosResponse(detail(), config.url === "/admin/words" ? 201 : 200),
      );
    });
    const store = createAppStore();
    const body = {
      headword: "bonjour",
      senses: [],
      pronunciations: [],
    };
    await store.dispatch(wordsApi.endpoints.createWord.initiate(body)).unwrap();
    await store
      .dispatch(
        wordsApi.endpoints.updateWord.initiate({
          wordId: IDS.word,
          ...body,
          concurrencyStamp: IDS.stamp,
        }),
      )
      .unwrap();
    for (const endpoint of ["publishWord", "unpublishWord", "archiveWord"])
      await store
        .dispatch(
          wordsApi.endpoints[endpoint].initiate({
            wordId: IDS.word,
            concurrencyStamp: IDS.stamp,
          }),
        )
        .unwrap();
    await store
      .dispatch(
        wordsApi.endpoints.deleteWord.initiate({
          wordId: IDS.word,
          concurrencyStamp: IDS.stamp,
        }),
      )
      .unwrap();
    expect(
      requestMock.mock.calls.map(([config]) => [
        config.url,
        config.method,
        config.data,
      ]),
    ).toEqual([
      ["/admin/words", "POST", body],
      [
        `/admin/words/${IDS.word}`,
        "PUT",
        { ...body, concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/words/${IDS.word}/publish`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/words/${IDS.word}/unpublish`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/words/${IDS.word}/archive`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [`/admin/words/${IDS.word}`, "DELETE", { concurrencyStamp: IDS.stamp }],
    ]);
  });

  it("lists every processing state for the word workflow and keeps playback separate", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.includes("/playback")
            ? {
                url: "https://media.example.test/audio.mp3",
                expiresAt: null,
                durationSeconds: 1.2,
                audioClipKind: "WordPronunciation",
              }
            : {
                items: [],
                page: 1,
                pageSize: 20,
                totalCount: 0,
                totalPages: 0,
              },
        ),
      ),
    );
    const store = createAppStore();
    await store
      .dispatch(
        wordsApi.endpoints.getWordAudioOptions.initiate({
          page: 1,
          pageSize: 20,
          keyword: "bonjour",
          kind: "WordPronunciation",
        }),
      )
      .unwrap();
    await store
      .dispatch(wordsApi.endpoints.getAudioPlayback.initiate(IDS.audio))
      .unwrap();
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/audio?page=1&pageSize=20&kind=WordPronunciation&keyword=bonjour",
    );
    expect(requestMock.mock.calls[1][0].url).toBe(
      `/audio/${IDS.audio}/playback`,
    );
  });

  it("uses the exact Audio upload, create, publish and retry contracts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(
          axiosResponse({
            module: "Audio",
            maxSizeBytes: 20 * 1024 * 1024,
            allowedTypes: [{ extension: ".wav", contentTypes: ["audio/wav"] }],
            multipartThresholdBytes: 256 * 1024 * 1024,
            partSizeBytes: 16 * 1024 * 1024,
            maxPartCount: 10000,
            partPresignBatchLimit: 20,
          }),
        );
      if (config.url.endsWith("/media/presign"))
        return Promise.resolve(
          axiosResponse({
            resourceId: IDS.resource,
            presignedUrl: "https://storage.example.test/audio.wav",
            objectName: "audio/source.wav",
          }),
        );
      if (config.url.endsWith("/confirm"))
        return Promise.resolve(
          axiosResponse({
            id: IDS.resource,
            uploaderId: IDS.user,
            objectName: "audio/source.wav",
            originalName: "bonjour.wav",
            module: "Audio",
            status: "Active",
            size: 4,
            extension: ".wav",
            contentType: "audio/wav",
            url: null,
            createdAt: "2026-08-01T10:00:00Z",
          }),
        );
      return Promise.resolve(
        axiosResponse(
          audioDetails(
            config.url.endsWith("/publish")
              ? {
                  processingStatus: "Ready",
                  publicationStatus: "Published",
                  durationSeconds: 1.2,
                  sampleRate: 44100,
                  channels: 1,
                  containerFormat: "mp3",
                  sourceCodec: "pcm_s16le",
                  publishedAt: "2026-08-01T10:05:00Z",
                }
              : {},
          ),
          config.url === "/admin/audio" ? 201 : 200,
        ),
      );
    });
    const store = createAppStore();
    const file = new File(["wave"], "bonjour.wav", { type: "audio/wav" });
    await store
      .dispatch(wordsApi.endpoints.getAudioUploadCapability.initiate())
      .unwrap();
    await store
      .dispatch(wordsApi.endpoints.presignWordAudio.initiate(file))
      .unwrap();
    await store
      .dispatch(
        wordsApi.endpoints.confirmWordAudioResource.initiate(IDS.resource),
      )
      .unwrap();
    const body = {
      sourceMediaResourceId: IDS.resource,
      title: "bonjour 发音",
      description: null,
      kind: "WordPronunciation",
    };
    await store
      .dispatch(wordsApi.endpoints.createWordAudio.initiate(body))
      .unwrap();
    await store
      .dispatch(wordsApi.endpoints.publishWordAudio.initiate(IDS.audio))
      .unwrap();
    await store
      .dispatch(wordsApi.endpoints.retryWordAudio.initiate(IDS.audio))
      .unwrap();

    expect(
      requestMock.mock.calls.map(([config]) => [
        config.url,
        config.method,
        config.data,
      ]),
    ).toEqual([
      ["/uploads/admin/media/capabilities?module=Audio", "GET", undefined],
      [
        "/uploads/admin/media/presign",
        "POST",
        {
          originalName: "bonjour.wav",
          extension: ".wav",
          contentType: "audio/wav",
          size: 4,
          module: "Audio",
        },
      ],
      [`/uploads/resources/${IDS.resource}/confirm`, "PUT", undefined],
      ["/admin/audio", "POST", body],
      [`/admin/audio/${IDS.audio}/publish`, "POST", undefined],
      [`/admin/audio/${IDS.audio}/retry`, "POST", undefined],
    ]);
  });

  it("contains unknown response enums as contract errors", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(axiosResponse(detail({ status: "Deleted" }))),
    );
    const store = createAppStore();
    await expect(
      store
        .dispatch(wordsApi.endpoints.getAdminWord.initiate(IDS.word))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });
});
