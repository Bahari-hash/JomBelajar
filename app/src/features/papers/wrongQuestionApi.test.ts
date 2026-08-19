import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { setSession, clearSession } from "@/features/auth/sessionStore";
import { wrongQuestionApi } from "@/features/papers/wrongQuestionApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
  clearSession();
});

describe("wrongQuestionApi", () => {
  it("uses list, detail and redo contracts", async () => {
    const requests: Array<{ url?: string; method?: string; data?: unknown }> =
      [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push({
        url: config.url,
        method: config.method,
        data: config.data,
      });
      const data = config.url?.endsWith("/redo")
        ? {
            id: "wrong",
            questionId: "question",
            status: "Mastered",
            type: "FillBlank",
            isCorrect: true,
            explanation: null,
            selectedOptionId: null,
            booleanAnswer: null,
            textAnswer: "answer",
            textAnswers: null,
            correctOptionId: null,
            correctBoolean: null,
            acceptedAnswers: ["answer"],
            dictationAnswers: [],
            wrongCount: 1,
            redoCount: 1,
            lastWrongAt: "2026-08-10T00:00:00Z",
            lastRedoAt: "2026-08-10T01:00:00Z",
            masteredAt: "2026-08-10T01:00:00Z",
            concurrencyStamp: "stamp",
          }
        : config.url === "/paper-wrong-questions/wrong"
          ? {
              id: "wrong",
              questionId: "question",
              paperId: "paper",
              paperTitle: "Paper",
              status: "Pending",
              type: "FillBlank",
              prompt: "Prompt",
              points: 1,
              options: [],
              audioResourceId: null,
              dictationBlanks: [],
              wrongCount: 1,
              redoCount: 0,
              firstWrongAt: "2026-08-10T00:00:00Z",
              lastWrongAt: "2026-08-10T00:00:00Z",
              lastRedoAt: null,
              masteredAt: null,
              concurrencyStamp: "stamp",
            }
          : { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
      return {
        data,
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
    await store
      .dispatch(
        wrongQuestionApi.endpoints.getWrongQuestions.initiate({
          page: 1,
          pageSize: 20,
          status: "Pending",
        }),
      )
      .unwrap();
    await store
      .dispatch(wrongQuestionApi.endpoints.getWrongQuestion.initiate("wrong"))
      .unwrap();
    await store
      .dispatch(
        wrongQuestionApi.endpoints.redoWrongQuestion.initiate({
          id: "wrong",
          answer: { textAnswer: "answer" },
        }),
      )
      .unwrap();
    expect(
      requests.slice(0, 3).map(({ method, url }) => [method, url]),
    ).toEqual([
      ["get", "/paper-wrong-questions"],
      ["get", "/paper-wrong-questions/wrong"],
      ["post", "/paper-wrong-questions/wrong/redo"],
    ]);
  });
});
