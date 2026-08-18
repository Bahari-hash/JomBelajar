import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordLearningPage from "./WordLearningPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordLearningPage", () => {
  it("does not loop when session creation keeps failing", async () => {
    let starts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.url?.endsWith("/sessions")) {
        starts += 1;
        throw new Error("failed");
      }
      return {
        data: {
          totalLearnedCount: 0,
          todayLearnedCount: 0,
          hasMoreWords: true,
          activeSession: null,
        },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    render(
      <Provider store={createAppStore()}>
        <MemoryRouter>
          <WordLearningPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(
      await screen.findByText("新词学习会话创建失败，请重试。"),
    ).toBeInTheDocument();
    await waitFor(() => expect(starts).toBe(1));
  });

  it("retries session creation only after an explicit reload", async () => {
    let starts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.url?.endsWith("/sessions")) {
        starts += 1;
        throw new Error("failed");
      }
      return {
        data: {
          totalLearnedCount: 0,
          todayLearnedCount: 0,
          hasMoreWords: true,
          activeSession: null,
        },
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
          <WordLearningPage />
        </MemoryRouter>
      </Provider>,
    );

    await screen.findByText("新词学习会话创建失败，请重试。");
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(starts).toBe(1);
    await user.click(screen.getByRole("button", { name: "重新加载" }));
    await waitFor(() => expect(starts).toBe(2));
  });

  it("reloads an active session through the normalization endpoint", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      return {
        data: config.url?.endsWith("/overview")
          ? {
              totalLearnedCount: 0,
              todayLearnedCount: 0,
              hasMoreWords: true,
              activeSession: { id: "session-1" },
            }
          : {
              id: "session-1",
              sessionType: "Learning",
              phase: "Memorization",
              status: "Active",
              actualCount: 1,
              completedCount: 0,
              memorizationPassedCount: 0,
              spellingPassedCount: 0,
              excludedCount: 0,
              skippedCount: 0,
              startedAt: "2026-08-18T00:00:00Z",
              completedAt: null,
              currentItem: {
                phase: "Memorization",
                itemId: "item-1",
                wordId: "word-1",
                itemConcurrencyStamp: "stamp",
                isFavorite: false,
                memorization: {
                  headword: "latest",
                  senses: [],
                  audioResourceId: null,
                },
                spelling: null,
              },
            },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    render(
      <Provider store={createAppStore()}>
        <MemoryRouter>
          <WordLearningPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "latest" }),
    ).toBeInTheDocument();
    expect(
      requests.some(
        (request) => request.url === "/word-study/learning/sessions/session-1",
      ),
    ).toBe(true);
  });

  it("shows a stable empty state when all words are learned", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: {
        totalLearnedCount: 10,
        todayLearnedCount: 2,
        hasMoreWords: false,
        activeSession: null,
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    render(
      <Provider store={createAppStore()}>
        <MemoryRouter>
          <WordLearningPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "新词已经全部学完" }),
    ).toBeInTheDocument();
  });
});
