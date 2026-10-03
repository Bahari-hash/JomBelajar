import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordsPage from "@/pages/WordsPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordsPage", () => {
  it("shows one study entry with both learning and review counts", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: config.url?.includes("/learning/")
        ? {
            totalLearnedCount: 120,
            todayLearnedCount: 15,
            hasMoreWords: true,
            activeSession: { id: "learning-session" },
          }
        : {
            dueCount: 34,
            overdueCount: 8,
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
          <WordsPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(await screen.findByText("今日已学习 15 个")).toBeInTheDocument();
    expect(screen.getByText("待复习 34 个")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "继续学习" })).toHaveAttribute(
      "href",
      "/words/study",
    );
    expect(screen.queryByRole("link", { name: "开始旧词复习" })).not.toBeInTheDocument();
  });
});
