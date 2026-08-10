import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import PapersPage from "@/pages/PapersPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const PAPER_ID = "11111111-2222-3333-4444-555555555555";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function installAdapter(requests: InternalAxiosRequestConfig[]) {
  httpClient.defaults.adapter = (async (config) => {
    requests.push(config);
    return {
      data: {
        items: [
          {
            id: PAPER_ID,
            title: "A2 Grammar Check",
            description: "Check practical grammar knowledge.",
            languageTag: "en",
            questionCount: 12,
            totalScore: 24,
            passingScore: 15,
            publishedAt: "2026-08-10T00:00:00Z",
          },
        ],
        page: 1,
        pageSize: 12,
        totalCount: 60,
        totalPages: 5,
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    };
  }) as AxiosAdapter;
}

function LocationProbe() {
  const location = useLocation();
  return (
    <output data-testid="location">{`${location.pathname}${location.search}`}</output>
  );
}

function renderPage(path = "/papers") {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[path]}>
        <LocationProbe />
        <PapersPage />
      </MemoryRouter>
    </Provider>,
  );
}

describe("PapersPage", () => {
  it("searches only on submit and resets the page", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?page=4");

    expect(
      await screen.findByRole("heading", { name: "A2 Grammar Check" }),
    ).toBeInTheDocument();
    const beforeTyping = requests.length;
    await user.type(
      screen.getByRole("searchbox", { name: "搜索试卷标题" }),
      "grammar",
    );
    expect(requests).toHaveLength(beforeTyping);
    await user.click(screen.getByRole("button", { name: "搜索" }));

    await waitFor(() => {
      expect(requests.at(-1)?.params).toMatchObject({
        page: 1,
        pageSize: 12,
        keyword: "grammar",
      });
      expect(screen.getByTestId("location")).toHaveTextContent(
        "/papers?keyword=grammar",
      );
    });
  });

  it("normalizes invalid URL state before querying", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?page=-2&keyword=%20%20");
    await screen.findByRole("heading", { name: "A2 Grammar Check" });
    expect(requests[0]?.params).toMatchObject({ page: 1, pageSize: 12 });
    expect(requests[0]?.params.keyword).toBeUndefined();
  });

  it("shows only real catalog metadata and preserves the list return path", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?keyword=grammar");

    expect(await screen.findByText("12 题")).toBeInTheDocument();
    expect(screen.getByText("总分 24")).toBeInTheDocument();
    expect(screen.getByText("及格 15")).toBeInTheDocument();
    const link = screen.getByRole("link", { name: "查看《A2 Grammar Check》" });
    expect(link).toHaveAttribute("href", `/papers/${PAPER_ID}`);
  });
});
