import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { clearSession, setSession } from "@/features/auth/sessionStore";
import { wordStudyApi } from "@/features/wordStudy/wordStudyApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
const SESSION_ID = "11111111-2222-3333-4444-555555555555";
const ITEM_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
  clearSession();
});

describe("wordStudyApi", () => {
  it("uses the separated learning and review contracts", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      return {
        data: config.url?.endsWith("/overview")
          ? { activeSession: null }
          : { id: SESSION_ID, status: "Active" },
        status: config.method === "post" ? 201 : 200,
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
      .dispatch(wordStudyApi.endpoints.getLearningOverview.initiate())
      .unwrap();
    await store
      .dispatch(wordStudyApi.endpoints.startLearning.initiate())
      .unwrap();
    await store
      .dispatch(
        wordStudyApi.endpoints.submitLearningMemorization.initiate({
          sessionId: SESSION_ID,
          itemId: ITEM_ID,
          result: "Remembered",
          itemConcurrencyStamp: "stamp-1",
        }),
      )
      .unwrap();
    await store
      .dispatch(wordStudyApi.endpoints.getReviewOverview.initiate())
      .unwrap();
    await store
      .dispatch(
        wordStudyApi.endpoints.submitReviewSpelling.initiate({
          sessionId: SESSION_ID,
          itemId: ITEM_ID,
          answer: "école",
          itemConcurrencyStamp: "stamp-2",
        }),
      )
      .unwrap();

    expect(requests.map((request) => [request.method, request.url])).toEqual(
      expect.arrayContaining([
        ["get", "/word-study/learning/overview"],
        ["post", "/word-study/learning/sessions"],
        [
          "post",
          `/word-study/learning/sessions/${SESSION_ID}/items/${ITEM_ID}/memorization`,
        ],
        ["get", "/word-study/review/overview"],
        [
          "post",
          `/word-study/review/sessions/${SESSION_ID}/items/${ITEM_ID}/spelling`,
        ],
      ]),
    );
    const memorizationRequest = requests.find((request) =>
      request.url?.endsWith("/memorization"),
    );
    const spellingRequest = requests.find((request) =>
      request.url?.endsWith("/spelling"),
    );
    expect(memorizationRequest?.data).toBe(
      JSON.stringify({ result: "Remembered", itemConcurrencyStamp: "stamp-1" }),
    );
    expect(spellingRequest?.data).toBe(
      JSON.stringify({ answer: "école", itemConcurrencyStamp: "stamp-2" }),
    );
    expect(
      requests.every(
        (request) => request.headers.Authorization === "Bearer access",
      ),
    ).toBe(true);
  });
});
