import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import PaperDetailPage from "@/pages/PaperDetailPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const PAPER_ID = "11111111-2222-3333-4444-555555555555";
const ATTEMPT_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function detail(categories = ["grammar", "a2"]) {
  return {
    id: PAPER_ID,
    title: "Grammar Check",
    description: "Practice grammar.",
    instructions: "Answer every question.",
    categories: categories.map((name, index) => ({
      id: `77777777-7777-4777-8777-77777777777${index}`,
      name,
      slug: name,
    })),
    questionCount: 2,
    totalScore: 4,
    passingScore: 2,
    publishedAt: "2026-08-10T00:00:00Z",
  };
}

function attempt() {
  return {
    id: ATTEMPT_ID,
    paperId: PAPER_ID,
    attemptNumber: 1,
    status: "InProgress",
    title: "Grammar Check",
    description: null,
    instructions: null,
    questionCount: 0,
    paperTotalScore: 4,
    paperPassingScore: 2,
    startedAt: "2026-08-10T00:00:00Z",
    submittedAt: null,
    questions: [],
  };
}

function LocationProbe() {
  const location = useLocation();
  return (
    <output data-testid="location">{`${location.pathname}${location.search}`}</output>
  );
}

function renderPage(path = `/papers/${PAPER_ID}`) {
  const router = (
    <MemoryRouter initialEntries={[path]}>
      <LocationProbe />
      <Routes>
        <Route path="/papers/:paperId" element={<PaperDetailPage />} />
        <Route
          path="/paper-attempts/:attemptId"
          element={<div>attempt route</div>}
        />
      </Routes>
    </MemoryRouter>
  );
  render(<Provider store={createAppStore()}>{router}</Provider>);
}

describe("PaperDetailPage", () => {
  it("does not request invalid identifiers", () => {
    const requests: string[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config.url ?? "");
      throw new Error("should not request");
    }) as AxiosAdapter;
    renderPage("/papers/not-a-guid");
    expect(
      screen.getByRole("heading", { name: "试卷不存在或已下架" }),
    ).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it("shows details and starts only one attempt request", async () => {
    const requests: string[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(`${config.method} ${config.url}`);
      const isStart = config.url === `/papers/${PAPER_ID}/attempts`;
      return {
        data: isStart ? attempt() : detail(),
        status: isStart ? 201 : 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    renderPage();
    expect(
      await screen.findByRole("heading", { name: "Grammar Check" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Answer every question.")).toBeInTheDocument();
    const tags = screen.getByLabelText("试卷分类");
    expect(within(tags).getByText("grammar")).toBeInTheDocument();
    expect(within(tags).getByText("a2")).toBeInTheDocument();
    const button = screen.getByRole("button", { name: "开始测试" });
    await user.click(button);
    expect(button).toBeDisabled();
    await user.click(button);
    await waitFor(() =>
      expect(screen.getByTestId("location")).toHaveTextContent(
        `/paper-attempts/${ATTEMPT_ID}`,
      ),
    );
    expect(requests.filter((value) => value.includes("/attempts")).length).toBe(
      1,
    );
  });
  it("does not render a tag container when details have no tags", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      data: detail([]),
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    renderPage();

    await screen.findByRole("heading", { name: "Grammar Check" });
    expect(screen.queryByLabelText("试卷分类")).not.toBeInTheDocument();
  });
});
