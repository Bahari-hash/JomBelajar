import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordReviewPage from "./WordReviewPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordReviewPage", () => {
  it("does not retry failed session creation until reload is clicked", async () => {
    let starts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.url?.endsWith("/sessions")) {
        starts += 1;
        throw new Error("failed");
      }
      return {
        data: { dueCount: 1, overdueCount: 0, activeSession: null },
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
          <WordReviewPage />
        </MemoryRouter>
      </Provider>,
    );

    await screen.findByText("复习会话创建失败，请重试。");
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(starts).toBe(1);
    await user.click(screen.getByRole("button", { name: "重新加载" }));
    await waitFor(() => expect(starts).toBe(2));
  });

  it("shows a stable empty state when no words are due", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: { dueCount: 0, overdueCount: 0, activeSession: null },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    render(
      <Provider store={createAppStore()}>
        <MemoryRouter>
          <WordReviewPage />
        </MemoryRouter>
      </Provider>,
    );

    expect(
      await screen.findByRole("heading", { name: "当前没有到期复习" }),
    ).toBeInTheDocument();
  });
});
