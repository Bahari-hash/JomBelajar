import { describe, expect, it } from "vitest";
import { papersApi } from "@/services/papersApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const IDS = Object.freeze({
  paper: "11111111-1111-4111-8111-111111111111",
  user: "22222222-2222-4222-8222-222222222222",
  stamp: "33333333-3333-4333-8333-333333333333",
  question: "44444444-4444-4444-8444-444444444444",
  optionA: "55555555-5555-4555-8555-555555555555",
  optionB: "66666666-6666-4666-8666-666666666666",
});

function auditUser() {
  return { id: IDS.user, nickname: "管理员", avatarUrl: null };
}

function detail(overrides = {}) {
  return {
    id: IDS.paper,
    title: "English basics",
    description: null,
    instructions: "Choose the answer.",
    tags: ["grammar", "a2"],
    status: "Draft",
    passingScorePercentage: 60,
    passingScore: 1,
    totalScore: 2,
    attemptCount: 0,
    createdBy: auditUser(),
    lastEditor: auditUser(),
    publishedAt: null,
    archivedAt: null,
    concurrencyStamp: IDS.stamp,
    questions: [
      {
        id: IDS.question,
        type: "SingleChoice",
        prompt: "Hello means?",
        explanation: null,
        points: 2,
        sortOrder: 0,
        correctBoolean: null,
        fillBlankCaseSensitive: false,
        options: [
          {
            id: IDS.optionA,
            text: "你好",
            isCorrect: true,
            sortOrder: 0,
          },
          {
            id: IDS.optionB,
            text: "再见",
            isCorrect: false,
            sortOrder: 1,
          },
        ],
        acceptedAnswers: [],
      },
    ],
    createdAt: "2026-08-02T10:00:00Z",
    updatedAt: "2026-08-02T11:00:00Z",
    ...overrides,
  };
}

function listItem() {
  const value = detail();
  const {
    description: _description,
    instructions: _instructions,
    questions,
    ...common
  } = value;
  return { ...common, questionCount: questions.length };
}

describe("papersApi", () => {
  it("encodes every supported list filter", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [listItem()],
          page: 2,
          pageSize: 20,
          totalCount: 21,
          totalPages: 2,
        }),
      ),
    );
    const store = createAppStore();
    const request = store.dispatch(
      papersApi.endpoints.getAdminPapers.initiate({
        page: 2,
        pageSize: 20,
        keyword: "English",
        status: "Draft",
        tag: "cet-4",
      }),
    );
    await expect(request.unwrap()).resolves.toMatchObject({
      items: [{ title: "English basics", tags: ["grammar", "a2"] }],
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/papers?page=2&pageSize=20&keyword=English&status=Draft&tag=cet-4",
    );
    request.unsubscribe();
  });

  it("loads and validates the administrator Paper tag directory", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [{ name: "grammar", paperCount: 8 }],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    const store = createAppStore();
    await expect(
      store
        .dispatch(
          papersApi.endpoints.getAdminPaperTags.initiate({
            page: 1,
            pageSize: 20,
            keyword: "gram",
          }),
        )
        .unwrap(),
    ).resolves.toEqual({
      items: [{ name: "grammar", paperCount: 8 }],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    });
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/paper-tags?page=1&pageSize=20&keyword=gram",
    );
  });

  it("rejects malformed Paper tag directory responses", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [{ name: "", paperCount: 1 }],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    const store = createAppStore();
    await expect(
      store
        .dispatch(
          papersApi.endpoints.getAdminPaperTags.initiate({
            page: 1,
            pageSize: 20,
          }),
        )
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });

  it("uses exact aggregate, validation and lifecycle contracts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.endsWith("/validate"))
        return Promise.resolve(
          axiosResponse({
            isValid: false,
            issues: [
              {
                field: "questions[0].options",
                errorCode: "PaperQuestionShapeInvalid",
                message: "试卷题型与标准答案结构不匹配.",
                questionId: IDS.question,
                childId: null,
              },
            ],
          }),
        );
      if (config.method === "DELETE")
        return Promise.resolve(axiosResponse(undefined, 204));
      return Promise.resolve(
        axiosResponse(detail(), config.url === "/admin/papers" ? 201 : 200),
      );
    });
    const store = createAppStore();
    const body = {
      title: "English basics",
      description: null,
      instructions: null,
      tags: ["grammar", "a2"],
      passingScorePercentage: 60,
      questions: [],
    };
    await store
      .dispatch(papersApi.endpoints.createPaper.initiate(body))
      .unwrap();
    await store
      .dispatch(
        papersApi.endpoints.updatePaper.initiate({
          paperId: IDS.paper,
          ...body,
          concurrencyStamp: IDS.stamp,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        papersApi.endpoints.validatePaper.initiate({
          paperId: IDS.paper,
          concurrencyStamp: IDS.stamp,
        }),
      )
      .unwrap();
    for (const endpoint of ["publishPaper", "unpublishPaper", "archivePaper"])
      await store
        .dispatch(
          papersApi.endpoints[endpoint].initiate({
            paperId: IDS.paper,
            concurrencyStamp: IDS.stamp,
          }),
        )
        .unwrap();
    await store
      .dispatch(
        papersApi.endpoints.deletePaper.initiate({
          paperId: IDS.paper,
          concurrencyStamp: IDS.stamp,
        }),
      )
      .unwrap();

    expect(
      requestMock.mock.calls.map(([config]) => [
        config.url,
        config.method,
        config.data,
      ]),
    ).toEqual([
      ["/admin/papers", "POST", body],
      [
        `/admin/papers/${IDS.paper}`,
        "PUT",
        { ...body, concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/papers/${IDS.paper}/validate`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/papers/${IDS.paper}/publish`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/papers/${IDS.paper}/unpublish`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [
        `/admin/papers/${IDS.paper}/archive`,
        "POST",
        { concurrencyStamp: IDS.stamp },
      ],
      [`/admin/papers/${IDS.paper}`, "DELETE", { concurrencyStamp: IDS.stamp }],
    ]);
  });

  it("rejects unknown enums and obsolete question response shapes", async () => {
    tokenVault.install("access", "refresh");
    const store = createAppStore();
    mockHttpClient(() =>
      Promise.resolve(axiosResponse(detail({ status: "Deleted" }))),
    );
    await expect(
      store
        .dispatch(papersApi.endpoints.getAdminPaper.initiate(IDS.paper))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });

    store.dispatch(papersApi.util.resetApiState());
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          detail({
            questions: [
              {
                ...detail().questions[0],
                type: "TrueFalse",
                correctBoolean: true,
              },
            ],
          }),
        ),
      ),
    );
    await expect(
      store
        .dispatch(papersApi.endpoints.getAdminPaper.initiate(IDS.paper))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });

  it("rejects non-canonical paper tag contracts", async () => {
    tokenVault.install("access", "refresh");
    const store = createAppStore();
    mockHttpClient(() =>
      Promise.resolve(axiosResponse(detail({ tags: ["Grammar", "grammar"] }))),
    );

    await expect(
      store
        .dispatch(papersApi.endpoints.getAdminPaper.initiate(IDS.paper))
        .unwrap(),
    ).rejects.toMatchObject({ status: "CUSTOM_ERROR", kind: "contract" });
  });
});
