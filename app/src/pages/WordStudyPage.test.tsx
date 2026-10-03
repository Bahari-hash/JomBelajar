import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { StrictMode } from "react";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import WordStudyPage from "./WordStudyPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import type { WordStudySessionState } from "@/features/wordStudy/wordStudyTypes";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => { httpClient.defaults.adapter = originalAdapter; });
const group = (type: "Learning" | "Review", phase: "Memorization" | "Summary" = "Memorization"): WordStudySessionState => ({
  id: type, sessionType: type, phase, status: "Active", actualCount: 1, completedCount: 0,
  memorizationPassedCount: 0, spellingPassedCount: 0, excludedCount: 0, skippedCount: 0,
  startedAt: "2026-10-03T00:00:00Z", completedAt: null,
  currentItem: phase === "Summary" ? null : {phase: "Memorization", itemId: type, wordId: type,
    itemConcurrencyStamp: "stamp", isFavorite: false, spelling: null,
    memorization: { headword: type === "Review" ? "旧词" : "新词", senses: [], audioResourceId: null,
      ratingPreviews: [{rating: "Easy", dueAt: "2026-10-05T00:00:00Z", intervalSeconds: 172800}] }},
});
function setup(options: { due?: number; more?: boolean; activeLearning?: WordStudySessionState; activeReview?: WordStudySessionState; failStart?: boolean; failReview?: boolean } = {}) {
  let due = options.due ?? 0;
  let activeReview = options.activeReview ?? null;
  const requests: string[] = [];
  httpClient.defaults.adapter = (async config => {
    const url = config.url!;
    requests.push(`${config.method} ${url}`);
    let data: unknown;
    if (url.endsWith("learning/overview")) data = { totalLearnedCount: 1, todayLearnedCount: 0, hasMoreWords: options.more ?? true, activeSession: options.activeLearning ?? null };
    else if (url.endsWith("review/overview")) {
      if (options.failReview) throw new Error("overview failed");
      data = { dueCount: due, overdueCount: 0, activeSession: activeReview };
    } else if (url.endsWith("/sessions")) {
      if (options.failStart) throw new Error("start failed");
      data = group(url.includes("/review/") ? "Review" : "Learning");
    } else if (url.endsWith("/memorization")) {
      activeReview = group("Review", "Summary");
      data = { session: activeReview, spellingOutcome: null };
    } else if (url.endsWith("/summary")) {
      due = 0; activeReview = null;
      data = {...group("Review", "Summary"), status: "Completed", completedCount: 1};
    } else if (url.endsWith("/sessions/Learning")) data = options.activeLearning;
    else if (url.endsWith("/sessions/Review")) data = activeReview;
    else if (url.endsWith("/today")) data = { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 };
    else throw new Error(`Unexpected request ${url}`);
    return { data, status: 200, statusText: "OK", headers: new AxiosHeaders(), config };
  }) as AxiosAdapter;
  render(<StrictMode><Provider store={createAppStore()}><MemoryRouter><WordStudyPage /></MemoryRouter></Provider></StrictMode>);
  return requests;
}
describe("unified word study", () => {
  it("prioritizes due reviews and continues to new words on the same page after the optional spelling summary", async () => {
    const requests = setup({due: 1}); const user = userEvent.setup();
    await screen.findByRole("heading", {name: "旧词"});
    expect(requests.filter(r => r === "post /word-study/review/sessions")).toHaveLength(1);
    expect(requests).not.toContain("post /word-study/learning/sessions");
    await user.click(screen.getByRole("button", {name: "显示答案"}));
    await user.click(screen.getByRole("button", {name: /简单/}));
    await screen.findByRole("heading", {name: "本组学习小结"});
    await user.click(screen.getByRole("button", {name: "跳过拼写，完成本组"}));
    await screen.findByRole("heading", {name: "本组已完成"});
    await user.click(screen.getByRole("button", {name: "继续学习"}));
    await screen.findByRole("heading", {name: "新词"});
    expect(requests).toContain("post /word-study/learning/sessions");
  });
  it("starts new words when nothing is due", async () => {
    const requests = setup(); await screen.findByRole("heading", {name: "新词"});
    expect(requests).not.toContain("post /word-study/review/sessions");
  });
  it("resumes an unfinished learning summary before starting due reviews", async () => {
    const requests = setup({due: 2, activeLearning: group("Learning", "Summary")});
    await screen.findByRole("heading", {name: "本组学习小结"});
    expect(requests).toContain("get /word-study/learning/sessions/Learning");
    expect(requests.some(r => r.startsWith("post "))).toBe(false);
  });
  it("resumes the more recently started group when both legacy groups exist", async () => {
    const requests = setup({activeLearning: group("Learning"), activeReview: {...group("Review", "Summary"), startedAt: "2026-10-03T01:00:00Z"}});
    await screen.findByRole("heading", {name: "本组学习小结"});
    expect(requests).toContain("get /word-study/review/sessions/Review");
    expect(requests.some(r => r.startsWith("post "))).toBe(false);
  });
  it("does not infer no reviews from a failed overview", async () => {
    const requests = setup({failReview: true});
    await screen.findByRole("alert");
    expect(requests.some(r => r.startsWith("post "))).toBe(false);
  });
  it("does not retry a failed start until requested", async () => {
    const requests = setup({failStart: true}); const user = userEvent.setup();
    await screen.findByRole("alert");
    expect(requests.filter(r => r === "post /word-study/learning/sessions")).toHaveLength(1);
    await user.click(screen.getByRole("button", {name: "重新加载"}));
    await waitFor(() => expect(requests.filter(r => r === "post /word-study/learning/sessions")).toHaveLength(2));
  });
  it("finishes only when neither due nor new words remain", async () => {
    const requests = setup({more: false});
    await screen.findByRole("heading", {name: "当前学习已完成"});
    expect(requests.some(r => r.startsWith("post "))).toBe(false);
  });
});
