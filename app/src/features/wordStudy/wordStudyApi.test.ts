import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { waitFor } from "@testing-library/dom";
import { afterEach, describe, expect, it } from "vitest";
import { setSession, clearSession } from "@/features/auth/sessionStore";
import { wordStudyApi } from "@/features/wordStudy/wordStudyApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
const SESSION_ID = "11111111-2222-3333-4444-555555555555";
const ITEM_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const AUDIO_ID = "99999999-8888-7777-6666-555555555555";
const EXAMPLE_AUDIO_ID = "12345678-1234-4234-8234-123456789abc";

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
  clearSession();
});

describe("wordStudyApi", () => {
  it("uses the authenticated settings, session item, and result contracts", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      const data =
        config.url === `/word-study/sessions/${SESSION_ID}/items`
          ? [
              {
                itemId: ITEM_ID,
                wordId: "word-1",
                position: 0,
                status: "Pending",
                contentAvailable: true,
                content: {
                  headword: "hello",
                  senses: [
                    {
                      partOfSpeech: "Interjection",
                      definition: "used as a greeting",
                      usageNote: null,
                      sortOrder: 0,
                      examples: [
                        {
                          sentence: "Hello, world.",
                          translation: "你好，世界。",
                          sortOrder: 0,
                          audioResourceId: EXAMPLE_AUDIO_ID,
                        },
                        {
                          sentence: "Hello again.",
                          translation: "再次你好。",
                          sortOrder: 1,
                          audioResourceId: null,
                        },
                      ],
                    },
                  ],
                  audioResourceId: AUDIO_ID,
                },
              },
            ]
          : config.url?.endsWith("/result")
            ? { id: SESSION_ID, status: "Completed" }
            : config.url?.endsWith("/start")
              ? { id: SESSION_ID, status: "Active" }
              : { dailyWordStudyCount: 20 };
      return {
        data,
        status:
          config.method === "post" && config.url?.endsWith("/start")
            ? 201
            : 200,
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

    await store
      .dispatch(wordStudyApi.endpoints.getSettings.initiate())
      .unwrap();
    await store.dispatch(wordStudyApi.endpoints.startToday.initiate()).unwrap();
    await store
      .dispatch(wordStudyApi.endpoints.getSessionItems.initiate(SESSION_ID))
      .unwrap();
    await store
      .dispatch(
        wordStudyApi.endpoints.submitResult.initiate({
          sessionId: SESSION_ID,
          itemId: ITEM_ID,
          result: "Remembered",
        }),
      )
      .unwrap();
    expect(requests.map((request) => [request.method, request.url])).toEqual([
      ["get", "/users/me/word-study-settings"],
      ["post", "/word-study/today/start"],
      ["get", `/word-study/sessions/${SESSION_ID}/items`],
      ["post", `/word-study/sessions/${SESSION_ID}/items/${ITEM_ID}/result`],
      ["get", `/word-study/sessions/${SESSION_ID}/items`],
    ]);
    expect(
      wordStudyApi.endpoints.getSessionItems.select(SESSION_ID)(
        store.getState(),
      ).data?.[0]?.content?.audioResourceId,
    ).toBe(AUDIO_ID);
    expect(
      wordStudyApi.endpoints.getSessionItems
        .select(SESSION_ID)(store.getState())
        .data?.[0]?.content?.senses[0]?.examples.map(
          (example) => example.audioResourceId,
        ),
    ).toEqual([EXAMPLE_AUDIO_ID, null]);
    expect(requests[3]?.data).toBe(JSON.stringify({ result: "Remembered" }));
    expect(
      requests.every(
        (request) => request.headers.Authorization === "Bearer access",
      ),
    ).toBe(true);
  });

  it("rolls back an optimistic item result when the request fails", async () => {
    const resultControl: { reject?: (reason?: unknown) => void } = {};
    httpClient.defaults.adapter = ((config) => {
      if (config.url?.endsWith("/result")) {
        return new Promise((_resolve, reject) => {
          resultControl.reject = reject;
        });
      }
      return Promise.resolve({
        data: [
          {
            itemId: ITEM_ID,
            wordId: "word-1",
            position: 0,
            status: "Pending",
            contentAvailable: true,
            content: {
              headword: "hello",
              senses: [],
              audioResourceId: null,
            },
          },
        ],
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      });
    }) as AxiosAdapter;
    const store = createAppStore();
    await store
      .dispatch(wordStudyApi.endpoints.getSessionItems.initiate(SESSION_ID))
      .unwrap();

    const mutation = store.dispatch(
      wordStudyApi.endpoints.submitResult.initiate({
        sessionId: SESSION_ID,
        itemId: ITEM_ID,
        result: "Remembered",
      }),
    );
    await waitFor(() => expect(resultControl.reject).toBeDefined());
    expect(
      wordStudyApi.endpoints.getSessionItems.select(SESSION_ID)(
        store.getState(),
      ).data?.[0]?.status,
    ).toBe("Remembered");

    resultControl.reject?.(new Error("request failed"));
    await expect(mutation.unwrap()).rejects.toBeDefined();

    expect(
      wordStudyApi.endpoints.getSessionItems.select(SESSION_ID)(
        store.getState(),
      ).data?.[0]?.status,
    ).toBe("Pending");
  });
});
