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
  it("loads, validates, and saves the account-backed daily count", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      const requestBody = typeof config.data === "string" ? JSON.parse(config.data) : config.data;
      return {
        data: {
          dailyWordStudyCount:
            config.method === "put" ? requestBody.dailyWordStudyCount : 20,
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
        <WordStudySettingsPanel />
      </Provider>,
    );

    const input = await screen.findByRole("spinbutton", { name: /每天背诵数量/ });
    expect(input).toHaveValue(20);
    await user.clear(input);
    await user.type(input, "101");
    await user.click(screen.getByRole("button", { name: "保存背诵设置" }));
    expect(screen.getByText("每日背诵数量必须在 1 到 100 之间。")).toBeInTheDocument();

    await user.clear(input);
    await user.type(input, "30");
    await user.click(screen.getByRole("button", { name: "保存背诵设置" }));
    expect(await screen.findByText(/每日背诵数量已保存/)).toBeInTheDocument();
    await waitFor(() => {
      const put = requests.find((request) => request.method === "put");
      expect(put?.url).toBe("/users/me/word-study-settings");
      expect(put?.data).toBe(JSON.stringify({ dailyWordStudyCount: 30 }));
    });
  });
});
