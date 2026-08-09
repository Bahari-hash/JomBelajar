import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordsPage from "@/pages/WordsPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
const SESSION_ID = "11111111-2222-3333-4444-555555555555";
const ITEM_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function session(status: "Active" | "Completed") {
  return {
    id: SESSION_ID,
    requestedCount: 1,
    actualCount: 1,
    includePreviouslyStudied: true,
    selectionMode: "Sequential",
    languageTag: null,
    status,
    completedCount: status === "Completed" ? 1 : 0,
    rememberedCount: status === "Completed" ? 1 : 0,
    forgottenCount: 0,
    skippedCount: 0,
    startedAt: "2026-08-09T01:00:00Z",
    completedAt: status === "Completed" ? "2026-08-09T01:01:00Z" : null,
    abandonedAt: null,
  };
}

describe("WordsPage", () => {
  it("resumes the daily card and submits a remembered result", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      const data = config.url === "/word-study/today"
        ? {
            studyDateUtc: "2026-08-09T00:00:00Z",
            dailyWordStudyCount: 1,
            state: requests.some((request) => request.url?.endsWith("/result"))
              ? "Completed"
              : "Active",
            session: requests.some((request) => request.url?.endsWith("/result"))
              ? session("Completed")
              : session("Active"),
          }
        : config.url?.endsWith("/next")
          ? {
              sessionId: SESSION_ID,
              itemId: ITEM_ID,
              position: 0,
              actualCount: 1,
              wordId: "word-1",
              headword: "hello",
              languageTag: "en",
              senses: [
                {
                  partOfSpeech: "Interjection",
                  definition: "你好",
                  definitionLanguageTag: "zh",
                  usageNote: null,
                  sortOrder: 0,
                  examples: [
                    {
                      sentence: "Hello, everyone.",
                      languageTag: "en",
                      translation: "大家好。",
                      translationLanguageTag: "zh",
                      audioClipId: null,
                      sortOrder: 0,
                    },
                  ],
                },
              ],
              pronunciations: [],
            }
          : session("Completed");
      return {
        data,
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <MemoryRouter>
          <WordsPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(await screen.findByRole("heading", { name: "hello" })).toBeInTheDocument();
    expect(screen.getByText("你好")).toBeInTheDocument();
    expect(screen.getByText("Hello, everyone.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "记住了" }));

    expect(await screen.findByRole("heading", { name: "今天的单词已完成" })).toBeInTheDocument();
    await waitFor(() => {
      const result = requests.find((request) => request.url?.endsWith("/result"));
      expect(result?.data).toBe(JSON.stringify({ result: "Remembered" }));
    });
  });
});
