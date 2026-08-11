import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const PAPER_ID = "11111111-1111-4111-8111-111111111111";

function paperDetail(overrides = {}) {
  const user = {
    id: "22222222-2222-4222-8222-222222222222",
    nickname: "管理员",
    avatarUrl: null,
  };
  return {
    id: PAPER_ID,
    title: "English basics",
    description: null,
    instructions: null,
    languageTag: "en",
    tags: [],
    status: "Draft",
    passingScore: 0,
    passingScorePercentage: 60,
    totalScore: 1,
    attemptCount: 0,
    createdBy: user,
    lastEditor: user,
    publishedAt: null,
    archivedAt: null,
    concurrencyStamp: "33333333-3333-4333-8333-333333333333",
    questions: [
      {
        id: "44444444-4444-4444-8444-444444444444",
        type: "SingleChoice",
        prompt: "Hello means?",
        explanation: null,
        points: 1,
        sortOrder: 0,
        correctBoolean: null,
        fillBlankCaseSensitive: false,
        options: [],
        acceptedAnswers: [],
      },
    ],
    createdAt: "2026-08-02T10:00:00Z",
    updatedAt: "2026-08-02T11:00:00Z",
    ...overrides,
  };
}

describe("PaperEditor", () => {
  it("creates a server-valid incomplete draft with the exact aggregate body", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          paperDetail(),
          config.url === "/admin/papers" ? 201 : 200,
        ),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/papers/new");

    await user.type(await screen.findByLabelText("标题 *"), "English basics");
    const tagInput = screen.getByRole("textbox", { name: "试卷标签" });
    await user.type(tagInput, "Grammar");
    await user.keyboard("{Enter}");
    await user.type(tagInput, "grammar");
    await user.keyboard("{Enter}");
    await user.type(tagInput, "A2");
    await user.click(screen.getByRole("button", { name: "添加标签" }));
    await user.click(screen.getByRole("button", { name: "删除标签 grammar" }));
    await user.click(screen.getByRole("button", { name: "添加题目" }));
    await user.type(screen.getByLabelText("题干 *"), "Hello means?");
    expect(screen.getByLabelText("及格分百分比 *")).toHaveValue(60);
    await user.click(screen.getAllByRole("button", { name: "保存" })[0]);

    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/papers/${PAPER_ID}`),
    );
    const create = requestMock.mock.calls
      .map(([config]) => config)
      .find((config) => config.url === "/admin/papers");
    expect(create.data).toEqual({
      title: "English basics",
      description: null,
      instructions: null,
      languageTag: "ms",
      tags: ["a2"],
      passingScorePercentage: 60,
      questions: [
        {
          id: null,
          type: "SingleChoice",
          prompt: "Hello means?",
          explanation: null,
          points: 1,
          sortOrder: 0,
          correctBoolean: null,
          fillBlankCaseSensitive: false,
          options: [],
          acceptedAnswers: [],
        },
      ],
    });
  });

  it("keeps a cleared question score empty before accepting a replacement", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(paperDetail(), 200)));
    const user = userEvent.setup();
    renderAppAt("/papers/new");

    await screen.findByLabelText("标题 *");
    await user.click(screen.getByRole("button", { name: "添加题目" }));
    const points = screen.getByLabelText("分值 *");

    await user.clear(points);
    expect(points).toHaveValue(null);
    await user.type(points, "30");

    expect(points).toHaveValue(30);
  });
  it("collapses completed questions and adds the next question in place", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(paperDetail(), 200)));
    const user = userEvent.setup();
    renderAppAt("/papers/new");

    await screen.findByLabelText("标题 *");
    await user.click(screen.getByRole("button", { name: "添加题目" }));
    await user.type(screen.getByLabelText("题干 *"), "First question");
    await user.click(screen.getByRole("button", { name: "收起并添加下一题" }));

    expect(screen.getByRole("button", { name: "展开题目 1" })).toBeVisible();
    expect(screen.getByText("First question")).toBeVisible();
    expect(screen.getByRole("button", { name: "折叠题目 2" })).toBeVisible();
    expect(screen.getAllByLabelText("题干 *")).toHaveLength(1);

    await user.click(screen.getByRole("button", { name: "展开题目 1" }));
    expect(screen.getAllByLabelText("题干 *")).toHaveLength(2);
  });

  it("previews saved content without exposing the correct answer", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          paperDetail({
            questions: [
              {
                ...paperDetail().questions[0],
                options: [
                  {
                    id: "55555555-5555-4555-8555-555555555555",
                    text: "你好",
                    isCorrect: true,
                    sortOrder: 0,
                  },
                  {
                    id: "66666666-6666-4666-8666-666666666666",
                    text: "再见",
                    isCorrect: false,
                    sortOrder: 1,
                  },
                ],
              },
            ],
          }),
        ),
      ),
    );
    renderAppAt(`/papers/${PAPER_ID}`, {
      initialEntry: `/papers/${PAPER_ID}?tab=preview`,
    });

    expect(await screen.findByText("Hello means?")).toBeVisible();
    expect(screen.getByText("你好")).toBeVisible();
    expect(screen.getByText("再见")).toBeVisible();
    expect(
      screen.queryByLabelText("选项 1 为正确答案"),
    ).not.toBeInTheDocument();
  });
});
