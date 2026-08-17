import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordsPage from "@/pages/WordsPage";
import { wordStudyApi } from "@/features/wordStudy/wordStudyApi";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import type {
  WordStudySession,
  WordStudySessionItem,
  WordStudySessionItemStatus,
} from "@/features/wordStudy/wordStudyTypes";

const originalAdapter = httpClient.defaults.adapter;
const SESSION_ID = "11111111-2222-3333-4444-555555555555";

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function session(
  status: "Active" | "Completed",
  completedCount = status === "Completed" ? 3 : 1,
): WordStudySession {
  return {
    id: SESSION_ID,
    requestedCount: 3,
    actualCount: 3,
    includePreviouslyStudied: true,
    selectionMode: "Sequential",
    status,
    completedCount,
    rememberedCount: status === "Completed" ? 2 : 1,
    forgottenCount: status === "Completed" ? 1 : 0,
    skippedCount: 0,
    startedAt: "2026-08-09T01:00:00Z",
    completedAt: status === "Completed" ? "2026-08-09T01:01:00Z" : null,
    abandonedAt: null,
  };
}

function item(
  itemId: string,
  position: number,
  headword: string,
  status: WordStudySessionItemStatus,
  contentAvailable = true,
): WordStudySessionItem {
  return {
    itemId,
    wordId: `word-${itemId}`,
    position,
    status,
    contentAvailable,
    content: contentAvailable
      ? {
          headword,
          senses: [
            {
              partOfSpeech: "Interjection",
              definition: `释义 ${headword}`,
              usageNote: null,
              sortOrder: 0,
              examples: [],
            },
          ],
          audioResourceId: null,
        }
      : null,
  };
}

function renderPage() {
  const store = createAppStore();
  const view = render(
    <Provider store={store}>
      <MemoryRouter>
        <WordsPage />
      </MemoryRouter>
    </Provider>,
  );
  return { ...view, store };
}

describe("WordsPage", () => {
  it("supports free directory navigation, read-only history, and result submission", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    const items = [
      item("item-1", 0, "hello", "Remembered"),
      item("item-2", 1, "world", "Pending"),
      item("item-3", 2, "future", "Pending"),
    ];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 3,
          state: "Active",
          session: session("Active"),
        });
      }
      if (config.url === `/word-study/sessions/${SESSION_ID}/items`) {
        return response(config, items);
      }
      if (config.url?.endsWith("/result")) {
        return response(config, session("Active"));
      }
      if (config.url?.endsWith("/next")) {
        return response(config, null, 204);
      }
      return response(config, session("Active"));
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "world" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "上一词" }));
    expect(
      await screen.findByRole("heading", { name: "hello" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent("结果：已记住");
    expect(
      screen.queryByRole("button", { name: "记住了" }),
    ).not.toBeInTheDocument();

    await user.click(
      screen.getAllByRole("button", { name: /future.*待背诵/ })[0],
    );
    expect(
      await screen.findByRole("heading", { name: "future" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "没记住" }));
    expect(
      await screen.findByRole("heading", { name: "world" }),
    ).toBeInTheDocument();
    await waitFor(() => {
      const resultRequest = requests.find((request) =>
        request.url?.endsWith("/result"),
      );
      expect(resultRequest?.data).toBe(JSON.stringify({ result: "Forgotten" }));
    });
  });

  it("keeps a completed session browsable", async () => {
    httpClient.defaults.adapter = (async (config) => {
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 1,
          state: "Completed",
          session: session("Completed"),
        });
      }
      return response(config, [item("item-1", 0, "hello", "Remembered")]);
    }) as AxiosAdapter;

    renderPage();

    expect(
      await screen.findByRole("heading", { name: "hello" }),
    ).toBeInTheDocument();
    expect(screen.getAllByRole("status")[0]).toHaveTextContent(
      "今天的单词已完成",
    );
    expect(screen.getAllByRole("status")[1]).toHaveTextContent("结果：已记住");
  });

  it("reconciles an active session containing only unavailable items", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 1,
          state: "Active",
          session: session("Active", 0),
        });
      }
      if (config.url?.endsWith("/next")) {
        return response(config, null, 204);
      }
      if (config.url?.includes("/items")) {
        return response(config, [
          item("item-1", 0, "hidden", "Pending", false),
        ]);
      }
      return response(config, session("Active", 0));
    }) as AxiosAdapter;

    renderPage();

    expect(await screen.findByText("该单词当前不可查看")).toBeInTheDocument();
    expect(requests.some((request) => request.url?.endsWith("/next"))).toBe(
      true,
    );
  });

  it("shows a recoverable empty state when the session has no items", async () => {
    httpClient.defaults.adapter = (async (config) => {
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 1,
          state: "Active",
          session: session("Active", 0),
        });
      }
      return response(config, []);
    }) as AxiosAdapter;

    renderPage();

    expect(
      await screen.findByRole("heading", { name: "今日词单暂时为空" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "刷新词单" }),
    ).toBeInTheDocument();
  });

  it("retries the session item request after an initial failure", async () => {
    let itemAttempts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 1,
          state: "Active",
          session: session("Active", 0),
        });
      }
      if (config.url?.includes("/items")) {
        itemAttempts += 1;
        if (itemAttempts === 1) {
          throw new Error("temporary failure");
        }
        return response(config, [item("item-1", 0, "recovered", "Pending")]);
      }
      return response(config, session("Active", 0));
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "今日词单加载失败" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "重新加载" }));

    expect(
      await screen.findByRole("heading", { name: "recovered" }),
    ).toBeInTheDocument();
    expect(itemAttempts).toBe(2);
  });

  it("locks previous and next navigation while saving a result", async () => {
    let releaseResult: ((value: TestResponse) => void) | undefined;
    let resultConfig: InternalAxiosRequestConfig | undefined;
    const resultResponse = new Promise<TestResponse>((resolve) => {
      releaseResult = resolve;
    });
    const items = [
      item("item-1", 0, "hello", "Pending"),
      item("item-2", 1, "world", "Pending"),
    ];
    httpClient.defaults.adapter = (async (config) => {
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 2,
          state: "Active",
          session: session("Active", 0),
        });
      }
      if (config.url?.includes("/items") && !config.url.endsWith("/result")) {
        return response(config, items);
      }
      if (config.url?.endsWith("/result")) {
        resultConfig = config;
        return resultResponse;
      }
      return response(config, session("Active", 0));
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();

    expect(
      await screen.findByRole("heading", { name: "hello" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "记住了" }));

    expect(screen.getByRole("button", { name: "上一词" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "下一词" })).toBeDisabled();
    expect(
      screen.getAllByRole("button", { name: /world.*待背诵/ })[0],
    ).toBeDisabled();
    expect(screen.getByRole("heading", { name: "hello" })).toBeInTheDocument();

    if (releaseResult && resultConfig) {
      releaseResult(response(resultConfig, session("Active", 0)));
    }
  });

  it("shows a recoverable alert when a background item refresh fails", async () => {
    let itemAttempts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.url === "/word-study/today") {
        return response(config, {
          studyDateUtc: "2026-08-09T00:00:00Z",
          dailyWordStudyCount: 1,
          state: "Active",
          session: session("Active", 0),
        });
      }
      if (config.url?.includes("/items")) {
        itemAttempts += 1;
        if (itemAttempts > 1) {
          throw new Error("refresh failed");
        }
        return response(config, [item("item-1", 0, "hello", "Pending")]);
      }
      return response(config, session("Active", 0));
    }) as AxiosAdapter;
    const { store } = renderPage();

    expect(
      await screen.findByRole("heading", { name: "hello" }),
    ).toBeInTheDocument();
    store.dispatch(
      wordStudyApi.util.invalidateTags([
        { type: "WordStudySessionItems", id: SESSION_ID },
      ]),
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("词单刷新失败");
    expect(
      screen.getByRole("button", { name: "重试词单" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "hello" })).toBeInTheDocument();
  });
});

type TestResponse = {
  data: unknown;
  status: number;
  statusText: string;
  headers: AxiosHeaders;
  config: InternalAxiosRequestConfig;
};

function response(
  config: InternalAxiosRequestConfig,
  data: unknown,
  status = 200,
): TestResponse {
  return {
    data,
    status,
    statusText: "OK",
    headers: new AxiosHeaders(),
    config,
  };
}
