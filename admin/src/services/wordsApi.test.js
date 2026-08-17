import { describe, expect, it } from "vitest";
import {
  normalizeWordBatchImport,
  normalizeWordBatchValidation,
} from "@/services/wordBatchContracts.js";
import * as wordService from "@/services/wordsApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosHttpError, axiosResponse, mockHttpClient } from "@/test/http.js";

const { wordsApi } = wordService;
const IDS = Object.freeze({
  word: "11111111-1111-4111-8111-111111111111",
  stamp: "33333333-3333-4333-8333-333333333333",
  sense: "44444444-4444-4444-8444-444444444444",
  example: "55555555-5555-4555-8555-555555555555",
  audio: "66666666-6666-4666-8666-666666666666",
  exampleAudio: "77777777-7777-4777-8777-777777777777",
});

function audio(overrides = {}) {
  return {
    id: IDS.audio,
    name: "bonjour.mp3",
    status: "Ready",
    durationSeconds: 1.8,
    lastFailureCode: null,
    ...overrides,
  };
}

function example(overrides = {}) {
  return {
    id: IDS.example,
    sentence: "Bonjour, Marie!",
    translation: "你好，玛丽！",
    audio: audio({ id: IDS.exampleAudio, name: "example.mp3" }),
    sortOrder: 0,
    ...overrides,
  };
}

function detail(overrides = {}) {
  return {
    id: IDS.word,
    headword: "bonjour",
    audio: audio(),
    concurrencyStamp: IDS.stamp,
    senses: [
      {
        id: IDS.sense,
        partOfSpeech: "Interjection",
        definition: "你好",
        usageNote: null,
        sortOrder: 0,
        examples: [example()],
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
    primaryPartOfSpeech: "Interjection",
    primaryDefinition: "你好",
    senseCount: 1,
    exampleCount: 1,
    hasAudio: true,
    createdAt: value.createdAt,
    updatedAt: value.updatedAt,
    concurrencyStamp: value.concurrencyStamp,
  };
}

function batchValidation(overrides = {}) {
  return {
    isValid: true,
    summary: {
      wordCount: 1,
      senseCount: 1,
      exampleCount: 1,
      wordAudioReferenceCount: 1,
      exampleAudioReferenceCount: 1,
      matchedAudioReferenceCount: 2,
    },
    rows: [
      {
        rowNumber: 1,
        headword: "bonjour",
        normalizedHeadword: "BONJOUR",
        wordAudioName: "bonjour.mp3",
        senseCount: 1,
        exampleCount: 1,
        audioReferenceCount: 2,
        matchedAudioCount: 2,
      },
    ],
    errors: [],
    ...overrides,
  };
}

function batchImport(overrides = {}) {
  return {
    createdCount: 1,
    items: [{ rowNumber: 1, wordId: IDS.word }],
    ...overrides,
  };
}

describe("wordsApi", () => {
  it("exposes batch endpoints without restoring retired lifecycle or audio endpoints", () => {
    expect(wordsApi.endpoints.validateWordBatch).toBeDefined();
    expect(wordsApi.endpoints.importWordBatch).toBeDefined();
    expect(wordService.useValidateWordBatchMutation).toBeTypeOf("function");
    expect(wordService.useImportWordBatchMutation).toBeTypeOf("function");
    for (const endpoint of [
      "publishWord",
      "unpublishWord",
      "archiveWord",
      "getWordAudioOptions",
      "getAudioUploadCapability",
      "presignWordAudio",
      "confirmWordAudioResource",
      "createWordAudio",
      "publishWordAudio",
      "retryWordAudio",
      "getAudioPlayback",
    ])
      expect(wordsApi.endpoints[endpoint]).toBeUndefined();
    for (const hook of [
      "usePublishWordMutation",
      "useUnpublishWordMutation",
      "useArchiveWordMutation",
    ])
      expect(wordService[hook]).toBeUndefined();
  });

  it("normalizes valid word batch validation and import responses", () => {
    expect(normalizeWordBatchValidation(batchValidation())).toEqual(
      batchValidation(),
    );
    expect(normalizeWordBatchImport(batchImport())).toEqual(batchImport());
  });

  it.each([
    [
      "zero row number",
      () =>
        batchValidation({
          rows: [{ ...batchValidation().rows[0], rowNumber: 0 }],
        }),
    ],
    [
      "unknown error code",
      () =>
        batchValidation({
          errors: [
            {
              rowNumber: 1,
              field: "words[0].headword",
              errorCode: "Unknown",
              message: "invalid",
            },
          ],
        }),
    ],
    [
      "negative count",
      () =>
        batchValidation({
          summary: { ...batchValidation().summary, wordCount: -1 },
        }),
    ],
    [
      "non-integer count",
      () =>
        batchValidation({
          summary: { ...batchValidation().summary, exampleCount: 1.5 },
        }),
    ],
  ])("rejects batch validation with %s", (_case, buildValue) => {
    expect(() => normalizeWordBatchValidation(buildValue())).toThrow();
  });

  it.each([
    [
      "zero row number",
      batchValidation({
        rows: [{ ...batchValidation().rows[0], rowNumber: 0 }],
      }),
    ],
    [
      "unknown error code",
      batchValidation({
        errors: [
          {
            rowNumber: 1,
            field: "words[0].headword",
            errorCode: "Unknown",
            message: "invalid",
          },
        ],
      }),
    ],
    [
      "negative count",
      batchValidation({
        summary: { ...batchValidation().summary, wordCount: -1 },
      }),
    ],
    [
      "non-integer count",
      batchValidation({
        summary: { ...batchValidation().summary, exampleCount: 1.5 },
      }),
    ],
  ])(
    "returns a contract error for validation with %s",
    async (_case, value) => {
      tokenVault.install("access", "refresh");
      mockHttpClient(() => Promise.resolve(axiosResponse(value)));
      const store = createAppStore();

      await expect(
        store
          .dispatch(
            wordsApi.endpoints.validateWordBatch.initiate({ words: [] }),
          )
          .unwrap(),
      ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
    },
  );

  it("rejects an import response whose count does not match its items", () => {
    expect(() =>
      normalizeWordBatchImport(batchImport({ createdCount: 2 })),
    ).toThrow();
  });

  it("returns a contract error for an inconsistent import response", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(axiosResponse(batchImport({ createdCount: 2 }))),
    );
    const store = createAppStore();

    await expect(
      store
        .dispatch(wordsApi.endpoints.importWordBatch.initiate({ words: [] }))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });

  it("calls validation and import mutations with normalized responses", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/validate") ? batchValidation() : batchImport(),
        ),
      ),
    );
    const store = createAppStore();
    const body = { words: [{ headword: "bonjour", senses: [] }] };

    await expect(
      store
        .dispatch(wordsApi.endpoints.validateWordBatch.initiate(body))
        .unwrap(),
    ).resolves.toEqual(batchValidation());
    await expect(
      store
        .dispatch(wordsApi.endpoints.importWordBatch.initiate(body))
        .unwrap(),
    ).resolves.toEqual(batchImport());
    expect(
      requestMock.mock.calls.map(([config]) => [
        config.url,
        config.method,
        config.data,
      ]),
    ).toEqual([
      ["/admin/words/batch/validate", "POST", body],
      ["/admin/words/batch", "POST", body],
    ]);
  });

  it("keeps a valid 422 validation response and rejects an invalid one", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient();
    requestMock.mockRejectedValueOnce(
      axiosHttpError(batchValidation({ isValid: false }), 422),
    );
    requestMock.mockRejectedValueOnce(
      axiosHttpError({ isValid: false, errors: [] }, 422),
    );
    const store = createAppStore();
    const body = { words: [] };

    await expect(
      store
        .dispatch(wordsApi.endpoints.importWordBatch.initiate(body))
        .unwrap(),
    ).rejects.toMatchObject({
      status: 422,
      data: batchValidation({ isValid: false }),
    });
    await expect(
      store
        .dispatch(wordsApi.endpoints.importWordBatch.initiate(body))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });

  it("encodes only supported list filters and normalizes the new list contract", async () => {
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
        language: "fr",
        partOfSpeech: "Interjection",
        definition: "你好",
      }),
    );
    const result = await request.unwrap();
    expect(result.items[0]).toEqual(listItem());
    for (const property of [
      "status",
      "createdBy",
      "lastEditor",
      "publishedAt",
      "archivedAt",
      "pronunciationCount",
    ])
      expect(result.items[0]).not.toHaveProperty(property);
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/words?page=2&pageSize=20&keyword=bonjour&partOfSpeech=Interjection&definition=%E4%BD%A0%E5%A5%BD",
    );
    request.unsubscribe();
  });

  it("uses the immediate aggregate and concurrency-protected delete contracts", async () => {
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
      audioResourceId: IDS.audio,
      senses: [],
    };
    const created = await store
      .dispatch(wordsApi.endpoints.createWord.initiate(body))
      .unwrap();
    expect(created.audio).toEqual(audio());
    expect(created.senses[0].examples[0].audio).toEqual(
      audio({ id: IDS.exampleAudio, name: "example.mp3" }),
    );
    await store
      .dispatch(
        wordsApi.endpoints.updateWord.initiate({
          wordId: IDS.word,
          ...body,
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
      [`/admin/words/${IDS.word}`, "DELETE", { concurrencyStamp: IDS.stamp }],
    ]);
  });

  it("contains unknown shared audio statuses as contract errors", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(detail({ audio: audio({ status: "Deleted" }) })),
      ),
    );
    const store = createAppStore();
    await expect(
      store
        .dispatch(wordsApi.endpoints.getAdminWord.initiate(IDS.word))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });

  it("accepts a null example audio association", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          detail({
            senses: [
              {
                ...detail().senses[0],
                examples: [example({ audio: null })],
              },
            ],
          }),
        ),
      ),
    );
    const store = createAppStore();

    const result = await store
      .dispatch(wordsApi.endpoints.getAdminWord.initiate(IDS.word))
      .unwrap();

    expect(result.senses[0].examples[0].audio).toBeNull();
  });

  it.each([
    ["unknown status", audio({ status: "Deleted" })],
    ["empty id", audio({ id: "00000000-0000-0000-0000-000000000000" })],
  ])("rejects example audio with %s", async (_case, invalidAudio) => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          detail({
            senses: [
              {
                ...detail().senses[0],
                examples: [example({ audio: invalidAudio })],
              },
            ],
          }),
        ),
      ),
    );
    const store = createAppStore();

    await expect(
      store
        .dispatch(wordsApi.endpoints.getAdminWord.initiate(IDS.word))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });
});
