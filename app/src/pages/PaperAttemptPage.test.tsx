import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import { ApiRequestError } from "@/features/auth/authErrors";
import PaperAttemptPage from "@/pages/PaperAttemptPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const ATTEMPT_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const PAPER_ID = "11111111-2222-3333-4444-555555555555";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function active() {
  return {
    id: ATTEMPT_ID,
    paperId: PAPER_ID,
    attemptNumber: 1,
    status: "InProgress",
    title: "Grammar Check",
    description: null,
    instructions: null,
    languageTag: "en",
    questionCount: 1,
    paperTotalScore: 1,
    paperPassingScore: 1,
    startedAt: "2026-08-10T00:00:00Z",
    submittedAt: null,
    questions: [
      {
        id: "q1",
        type: "SingleChoice",
        prompt: "Choose A",
        points: 1,
        sortOrder: 1,
        options: [{ id: "a", text: "A", sortOrder: 1 }],
        savedAnswer: null,
      },
    ],
  };
}

function interactiveAttempt() {
  const attempt = active();
  return {
    ...attempt,
    questionCount: 2,
    questions: [
      ...attempt.questions,
      {
        id: "q2",
        type: "FillBlank",
        prompt: "Write the answer",
        points: 1,
        sortOrder: 2,
        options: [],
        savedAnswer: null,
      },
    ],
  };
}

function submitted() {
  return {
    id: ATTEMPT_ID,
    paperId: PAPER_ID,
    attemptNumber: 1,
    status: "Submitted",
    title: "Grammar Check",
    description: null,
    instructions: null,
    languageTag: "en",
    questionCount: 1,
    paperTotalScore: 1,
    paperPassingScore: 1,
    startedAt: "2026-08-10T00:00:00Z",
    submittedAt: "2026-08-10T01:00:00Z",
    questions: [],
  };
}

function result() {
  return {
    id: ATTEMPT_ID,
    paperId: PAPER_ID,
    attemptNumber: 1,
    paperTitle: "Grammar Check",
    score: 1,
    paperTotalScore: 1,
    paperPassingScore: 1,
    isPassed: true,
    startedAt: "2026-08-10T00:00:00Z",
    submittedAt: "2026-08-10T01:00:00Z",
    questions: [],
  };
}

function renderPage(reactStrictMode = false) {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[`/paper-attempts/${ATTEMPT_ID}`]}>
        <Routes>
          <Route
            path="/paper-attempts/:attemptId"
            element={<PaperAttemptPage />}
          />
        </Routes>
      </MemoryRouter>
    </Provider>,
    { reactStrictMode },
  );
}

describe("PaperAttemptPage", () => {
  it("renders an editable workspace for an in-progress attempt", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: active(),
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    renderPage();
    expect(
      await screen.findByRole("heading", { name: "Choose A" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "A" })).toBeInTheDocument();
  });

  it("allows choosing options and typing fill answers in StrictMode", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: config.url?.includes("/answers/")
        ? undefined
        : interactiveAttempt(),
      status: config.url?.includes("/answers/") ? 204 : 200,
      statusText: config.url?.includes("/answers/") ? "No Content" : "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();

    renderPage(true);
    const option = await screen.findByRole("radio", { name: "A" });
    await user.click(option);
    expect(option).toBeChecked();

    await user.click(screen.getByRole("button", { name: "下一题" }));
    const fill = screen.getByRole("textbox", { name: "填空答案" });
    await user.type(fill, "hello");
    expect(fill).toHaveValue("hello");
  });

  it("renders submitted attempts as read-only results", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: config.url?.endsWith("/result") ? result() : submitted(),
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    renderPage();
    expect(
      await screen.findByRole("heading", { name: "测试结果" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  });

  it("recovers a submitted result after a submit conflict", async () => {
    const requests: string[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(`${config.method} ${config.url}`);
      if (config.url?.endsWith("/submit")) {
        throw new ApiRequestError("提交测试失败，请重试。", {
          code: "PaperAttemptConcurrencyConflict",
          status: 409,
        });
      }
      if (config.url?.endsWith("/result")) {
        return {
          data: result(),
          status: 200,
          statusText: "OK",
          headers: new AxiosHeaders(),
          config,
        };
      }
      if (config.url?.includes("/answers/")) {
        return {
          data: undefined,
          status: 204,
          statusText: "No Content",
          headers: new AxiosHeaders(),
          config,
        };
      }
      return {
        data: active(),
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();
    expect(
      await screen.findByRole("heading", { name: "Choose A" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /提交试卷/ }));
    await user.click(screen.getByRole("button", { name: "确认提交" }));
    expect(
      await screen.findByRole("heading", { name: "测试结果" }),
    ).toBeInTheDocument();
    expect(
      requests.filter((request) => request.endsWith("/result")),
    ).toHaveLength(1);
  });

  it("keeps the workspace when a submit conflict has no result yet", async () => {
    httpClient.defaults.adapter = (async (config) => {
      if (config.url?.endsWith("/submit") || config.url?.endsWith("/result")) {
        throw new ApiRequestError(
          config.url?.endsWith("/result")
            ? "结果暂不可用，请稍后重试。"
            : "提交测试失败，请重试。",
          {
            code: config.url?.endsWith("/result")
              ? "PaperAttemptNotSubmitted"
              : "PaperAttemptConcurrencyConflict",
            status: 409,
          },
        );
      }
      if (config.url?.includes("/answers/")) {
        return {
          data: undefined,
          status: 204,
          statusText: "No Content",
          headers: new AxiosHeaders(),
          config,
        };
      }
      return {
        data: active(),
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();
    expect(
      await screen.findByRole("heading", { name: "Choose A" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /提交试卷/ }));
    await user.click(screen.getByRole("button", { name: "确认提交" }));
    expect(
      await screen.findByRole("heading", { name: "Choose A" }),
    ).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "结果暂不可用，请稍后重试。",
    );
  });

  it("recovers a result when submission lost a concurrency race", async () => {
    const user = userEvent.setup();
    httpClient.defaults.adapter = (async (config) => {
      if (config.url?.endsWith("/submit")) {
        throw new ApiRequestError("提交测试失败，请重试。", {
          code: "PaperAttemptConcurrencyConflict",
          status: 409,
        });
      }
      if (config.url?.endsWith("/result")) {
        return {
          data: result(),
          status: 200,
          statusText: "OK",
          headers: new AxiosHeaders(),
          config,
        };
      }
      return {
        data: active(),
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;

    renderPage();
    await screen.findByRole("heading", { name: "Choose A" });
    await user.click(
      screen.getByRole("button", { name: /提交试卷.*1 题未作答/ }),
    );
    await user.click(screen.getByRole("button", { name: "确认提交" }));

    expect(
      await screen.findByRole("heading", { name: "测试结果" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  });
});
