import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { setSession, clearSession } from "@/features/auth/sessionStore";
import { paperApi } from "@/features/papers/paperApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const PAPER_ID = "11111111-2222-3333-4444-555555555555";
const ATTEMPT_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const QUESTION_ID = "99999999-8888-7777-6666-555555555555";
const OPTION_ID = "12345678-1234-1234-1234-123456789012";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
  clearSession();
});

function attemptResponse() {
  return {
    id: ATTEMPT_ID,
    paperId: PAPER_ID,
    attemptNumber: 1,
    status: "InProgress",
    title: "Grammar Check",
    description: null,
    instructions: null,
    questionCount: 1,
    paperTotalScore: 2,
    paperPassingScore: 1,
    startedAt: "2026-08-10T00:00:00Z",
    submittedAt: null,
    questions: [
      {
        id: QUESTION_ID,
        type: "SingleChoice",
        prompt: "Choose one",
        points: 2,
        sortOrder: 1,
        options: [{ id: OPTION_ID, text: "A", sortOrder: 1 }],
        savedAnswer: null,
      },
    ],
  };
}

describe("paperApi", () => {
  it("uses authenticated Paper API contracts", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    const adapter: AxiosAdapter = async (config) => {
      requests.push(config);
      let data: unknown = {
        items: [],
        page: 1,
        pageSize: 12,
        totalCount: 0,
        totalPages: 0,
      };
      if (config.url === `/papers/${PAPER_ID}`) {
        data = {
          id: PAPER_ID,
          title: "Grammar Check",
          description: null,
          instructions: null,
          questionCount: 1,
          totalScore: 2,
          passingScore: 1,
          publishedAt: "2026-08-10T00:00:00Z",
        };
      } else if (config.url === `/papers/${PAPER_ID}/attempts`) {
        data = attemptResponse();
      } else if (config.url === `/paper-attempts/${ATTEMPT_ID}`) {
        data = attemptResponse();
      } else if (
        config.url?.endsWith("/result") ||
        config.url?.endsWith("/submit")
      ) {
        data = {
          id: ATTEMPT_ID,
          paperId: PAPER_ID,
          attemptNumber: 1,
          paperTitle: "Grammar Check",
          score: 2,
          paperTotalScore: 2,
          paperPassingScore: 1,
          isPassed: true,
          startedAt: "2026-08-10T00:00:00Z",
          submittedAt: "2026-08-10T01:00:00Z",
          questions: [],
        };
      }
      return {
        data,
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    };
    httpClient.defaults.adapter = adapter;
    setSession({
      token: "access",
      expiresIn: 300,
      user: { id: "user", email: "user@test", role: "User" },
    });
    const store = createAppStore();

    await store
      .dispatch(
        paperApi.endpoints.getPapers.initiate({
          page: 2,
          pageSize: 12,
          keyword: "grammar",
          tag: "cet-4",
        }),
      )
      .unwrap();
    await store
      .dispatch(
        paperApi.endpoints.getPaperTags.initiate({ page: 1, pageSize: 6 }),
      )
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.getPaper.initiate(PAPER_ID))
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.startAttempt.initiate(PAPER_ID))
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.getAttempt.initiate(ATTEMPT_ID))
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.getAttemptResult.initiate(ATTEMPT_ID))
      .unwrap();
    await store
      .dispatch(
        paperApi.endpoints.saveAnswer.initiate({
          attemptId: ATTEMPT_ID,
          questionId: QUESTION_ID,
          answer: { selectedOptionId: OPTION_ID },
        }),
      )
      .unwrap();
    await store
      .dispatch(
        paperApi.endpoints.clearAnswer.initiate({
          attemptId: ATTEMPT_ID,
          questionId: QUESTION_ID,
        }),
      )
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.submitAttempt.initiate(ATTEMPT_ID))
      .unwrap();
    await store
      .dispatch(paperApi.endpoints.getAttemptResult.initiate(ATTEMPT_ID))
      .unwrap();

    expect(requests.map((request) => [request.method, request.url])).toEqual([
      ["get", "/papers"],
      ["get", "/paper-tags"],
      ["get", `/papers/${PAPER_ID}`],
      ["post", `/papers/${PAPER_ID}/attempts`],
      ["get", `/paper-attempts/${ATTEMPT_ID}`],
      ["get", `/paper-attempts/${ATTEMPT_ID}/result`],
      ["put", `/paper-attempts/${ATTEMPT_ID}/answers/${QUESTION_ID}`],
      ["delete", `/paper-attempts/${ATTEMPT_ID}/answers/${QUESTION_ID}`],
      ["post", `/paper-attempts/${ATTEMPT_ID}/submit`],
      ["get", `/paper-attempts/${ATTEMPT_ID}`],
    ]);
    expect(requests[0]).toMatchObject({
      params: {
        page: 2,
        pageSize: 12,
        keyword: "grammar",
        tag: "cet-4",
      },
    });
    expect(requests[1]).toMatchObject({
      params: { page: 1, pageSize: 6 },
    });
    expect(JSON.parse(requests[6]!.data as string)).toEqual({
      selectedOptionId: OPTION_ID,
    });
    expect(
      requests.every(
        (request) => request.headers.Authorization === "Bearer access",
      ),
    ).toBe(true);
  });

  it("updates the cached saved answer after a successful save", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data:
        config.url === `/paper-attempts/${ATTEMPT_ID}`
          ? attemptResponse()
          : undefined,
      status: 204,
      statusText: "No Content",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    const store = createAppStore();
    await store
      .dispatch(paperApi.endpoints.getAttempt.initiate(ATTEMPT_ID))
      .unwrap();
    await store
      .dispatch(
        paperApi.endpoints.saveAnswer.initiate({
          attemptId: ATTEMPT_ID,
          questionId: QUESTION_ID,
          answer: { selectedOptionId: OPTION_ID },
        }),
      )
      .unwrap();
    expect(
      paperApi.endpoints.getAttempt.select(ATTEMPT_ID)(store.getState()).data
        ?.questions[0]?.savedAnswer?.selectedOptionId,
    ).toBe(OPTION_ID);
  });
});
