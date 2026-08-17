import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosHttpError, axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const WORD_ID = "11111111-1111-4111-8111-111111111111";

function payload() {
  return {
    words: [
      {
        headword: "hello",
        audioFileName: "hello.mp3",
        senses: [
          {
            partOfSpeech: "Interjection",
            definition: "你好",
            usageNote: null,
            sortOrder: 0,
            examples: [],
          },
        ],
      },
    ],
  };
}

function validation(overrides = {}) {
  return {
    isValid: true,
    summary: {
      wordCount: 1,
      senseCount: 1,
      exampleCount: 0,
      wordAudioReferenceCount: 1,
      exampleAudioReferenceCount: 0,
      matchedAudioReferenceCount: 1,
    },
    rows: [
      {
        rowNumber: 1,
        headword: "hello",
        normalizedHeadword: "HELLO",
        wordAudioName: "hello.mp3",
        senseCount: 1,
        exampleCount: 0,
        audioReferenceCount: 1,
        matchedAudioCount: 1,
      },
    ],
    errors: [],
    ...overrides,
  };
}

function wordPage() {
  return {
    items: [],
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0,
  };
}

function chooseJson(container, value = payload()) {
  const file = new File([JSON.stringify(value)], "words.json", {
    type: "application/json",
  });
  fireEvent.change(container.querySelector('input[type="file"]'), {
    target: { files: [file] },
  });
  return file;
}

describe("WordBatchImport", () => {
  it("validates a file and renders its summary, row preview and example download", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse(validation())),
    );
    const { container } = renderAppAt("/words/batch");

    expect(
      await screen.findByRole("heading", { level: 1, name: "批量导入单词" }),
    ).toBeVisible();
    expect(screen.getByRole("link", { name: "下载示例 JSON" })).toHaveAttribute(
      "download",
      "tiny-lang-word-import-example.json",
    );
    chooseJson(container);

    expect(await screen.findByText("校验通过，可以导入。")).toBeVisible();
    expect(screen.getByText("单词数")).toBeVisible();
    expect(screen.getByText("HELLO")).toBeVisible();
    expect(screen.getByText("hello.mp3")).toBeVisible();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === "/admin/words/batch/validate" &&
          config.method === "POST" &&
          config.data.words[0].headword === "hello",
      ),
    ).toBe(true);
    expect(screen.getByRole("button", { name: "确认导入" })).toBeEnabled();
  });

  it("disables import for invalid validation and shows row, field and message", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse(
          validation({
            isValid: false,
            errors: [
              {
                rowNumber: 1,
                field: "words[0].audioFileName",
                errorCode: "WordBatchAudioNotFound",
                message: "音频不存在。",
              },
            ],
          }),
        ),
      ),
    );
    const { container } = renderAppAt("/words/batch");

    await screen.findByRole("heading", { level: 1, name: "批量导入单词" });
    chooseJson(container);

    expect(await screen.findByText("第 1 行")).toBeVisible();
    expect(screen.getByText("words[0].audioFileName")).toBeVisible();
    expect(screen.getByText("音频不存在。")).toBeVisible();
    expect(screen.getByRole("button", { name: "确认导入" })).toBeDisabled();
  });

  it("replaces validation with a structured 422 response and keeps the file", async () => {
    tokenVault.install("access", "refresh");
    const invalid = validation({
      isValid: false,
      errors: [
        {
          rowNumber: 1,
          field: "words[0].headword",
          errorCode: "WordBatchConflict",
          message: "单词已存在。",
        },
      ],
    });
    const requestMock = mockHttpClient();
    requestMock.mockResolvedValueOnce(axiosResponse(validation()));
    requestMock.mockRejectedValueOnce(axiosHttpError(invalid, 422));
    const user = userEvent.setup();
    const { container } = renderAppAt("/words/batch");

    await screen.findByRole("heading", { level: 1, name: "批量导入单词" });
    chooseJson(container);
    await user.click(await screen.findByRole("button", { name: "确认导入" }));

    expect(await screen.findByText("单词已存在。")).toBeVisible();
    expect(screen.getByText("words.json")).toBeVisible();
    expect(screen.getByRole("button", { name: "确认导入" })).toBeDisabled();
  });

  it("imports a valid batch and shows the one-time notice on the word list", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.endsWith("/validate"))
        return Promise.resolve(axiosResponse(validation()));
      if (config.url === "/admin/words/batch")
        return Promise.resolve(
          axiosResponse({
            createdCount: 1,
            items: [{ rowNumber: 1, wordId: WORD_ID }],
          }),
        );
      return Promise.resolve(axiosResponse(wordPage()));
    });
    const user = userEvent.setup();
    const { container, router } = renderAppAt("/words/batch");

    await screen.findByRole("heading", { level: 1, name: "批量导入单词" });
    chooseJson(container);
    await user.click(await screen.findByRole("button", { name: "确认导入" }));

    expect(await screen.findByText("已批量创建 1 个单词。")).toBeVisible();
    expect(router.state.location.pathname).toBe("/words");
    await waitFor(() => expect(router.state.location.state).toBeNull());
    expect(
      requestMock.mock.calls.some(
        ([config]) => config.url === "/admin/words/batch",
      ),
    ).toBe(true);
  });

  it("uses batch-specific copy when unsaved file navigation is blocked", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(validation())));
    const user = userEvent.setup();
    const { container } = renderAppAt("/words/batch");

    await screen.findByRole("heading", { level: 1, name: "批量导入单词" });
    chooseJson(container);
    await screen.findByText("校验通过，可以导入。");
    await user.click(screen.getByRole("link", { name: "返回单词列表" }));

    expect(
      screen.getByRole("heading", { name: "离开批量导入？" }),
    ).toBeVisible();
    expect(screen.getByText("当前文件和校验结果将会丢失。")).toBeVisible();
  });
});
