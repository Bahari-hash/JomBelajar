import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import WordStudySummaryPanel from "./WordStudySummaryPanel";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordStudySummaryPanel", () => {
  it("can retry an isolated overview failure", async () => {
    let attempts = 0;
    httpClient.defaults.adapter = (async (config) => {
      attempts += 1;
      if (attempts === 1) throw new Error("temporary");
      return {
        data: { todayLearnedCount: 3, totalLearnedCount: 12 },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordStudySummaryPanel />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "重新加载" }));
    expect(await screen.findByText("12")).toBeInTheDocument();
    expect(attempts).toBe(2);
  });
});
