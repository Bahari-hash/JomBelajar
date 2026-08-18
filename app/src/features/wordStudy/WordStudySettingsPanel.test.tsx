import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import WordStudySettingsPanel from "@/features/wordStudy/WordStudySettingsPanel";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordStudySettingsPanel", () => {
  it("validates learning and review limits independently", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: { dailyWordStudyCount: 20, dailyWordReviewCount: 50 },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordStudySettingsPanel />
      </Provider>,
    );
    const study = await screen.findByRole("spinbutton", {
      name: "每组新词数量",
    });
    const review = screen.getByRole("spinbutton", { name: "每组复习数量" });
    await user.clear(study);
    await user.type(study, "101");
    await user.clear(review);
    await user.type(review, "201");
    await user.click(screen.getByRole("button", { name: "保存设置" }));

    expect(
      screen.getByText("每组新词数量必须在 1 到 100 之间。"),
    ).toBeInTheDocument();
    expect(
      screen.getByText("每组复习数量必须在 1 到 200 之间。"),
    ).toBeInTheDocument();
  });

  it("loads and saves separate learning and review group sizes", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      const requestBody =
        typeof config.data === "string" ? JSON.parse(config.data) : config.data;
      return {
        data:
          config.method === "put"
            ? requestBody
            : { dailyWordStudyCount: 20, dailyWordReviewCount: 50 },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordStudySettingsPanel />
      </Provider>,
    );

    const study = await screen.findByRole("spinbutton", {
      name: "每组新词数量",
    });
    const review = screen.getByRole("spinbutton", { name: "每组复习数量" });
    expect(study).toHaveValue(20);
    expect(review).toHaveValue(50);
    await user.clear(study);
    await user.type(study, "30");
    await user.clear(review);
    await user.type(review, "80");
    await user.click(screen.getByRole("button", { name: "保存设置" }));

    expect(await screen.findByText(/设置已保存/)).toBeInTheDocument();
    await waitFor(() => {
      const put = requests.find((request) => request.method === "put");
      expect(put?.data).toBe(
        JSON.stringify({ dailyWordStudyCount: 30, dailyWordReviewCount: 80 }),
      );
    });
  });

  it("shows a retry action when loading settings fails", async () => {
    let requests = 0;
    httpClient.defaults.adapter = (async (config) => {
      requests += 1;
      if (requests === 1) throw new Error("failed");
      return {
        data: { dailyWordStudyCount: 20, dailyWordReviewCount: 50 },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordStudySettingsPanel />
      </Provider>,
    );

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "学习设置暂时无法加载",
    );
    await user.click(screen.getByRole("button", { name: "重新加载" }));
    await waitFor(() =>
      expect(
        screen.getByRole("spinbutton", { name: "每组新词数量" }),
      ).toHaveValue(20),
    );
  });

  it("shows save failure and preserves the draft for retry", async () => {
    let saveAttempts = 0;
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "put") {
        saveAttempts += 1;
        throw new Error("failed");
      }
      return {
        data: { dailyWordStudyCount: 20, dailyWordReviewCount: 50 },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordStudySettingsPanel />
      </Provider>,
    );
    const study = await screen.findByRole("spinbutton", {
      name: "每组新词数量",
    });
    await user.clear(study);
    await user.type(study, "30");
    await user.click(screen.getByRole("button", { name: "保存设置" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("设置保存失败");
    expect(study).toHaveValue(30);
    expect(saveAttempts).toBe(1);
  });
});
