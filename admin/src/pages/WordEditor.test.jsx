import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const WORD_ID = "11111111-1111-4111-8111-111111111111";
const SENSE_ID = "44444444-4444-4444-8444-444444444444";
const EXAMPLE_ID = "55555555-5555-4555-8555-555555555555";
const AUDIO_ID = "66666666-6666-4666-8666-666666666666";
const AUDIO = {
  id: AUDIO_ID,
  name: "hello.mp3",
  status: "Processing",
  durationSeconds: null,
  lastFailureCode: null,
};

vi.mock("@/features/words/WordAudioControl.jsx", () => ({
  WordAudioControl: ({ value, onChange, disabled }) => (
    <div aria-label="单词读音">
      <p>{value ? `${value.name} ${value.status}` : "未关联单词读音"}</p>
      <button
        type="button"
        disabled={disabled}
        onClick={() => onChange(AUDIO)}
      >
        模拟选择单词音频
      </button>
      <button
        type="button"
        disabled={disabled || !value}
        onClick={() => onChange(null)}
      >
        模拟解除单词音频
      </button>
    </div>
  ),
}));

function word(overrides = {}) {
  return {
    id: WORD_ID,
    headword: "hello",
    audio: AUDIO,
    concurrencyStamp: "33333333-3333-4333-8333-333333333333",
    senses: [
      {
        id: SENSE_ID,
        partOfSpeech: "Noun",
        definition: "你好",
        usageNote: null,
        sortOrder: 0,
        examples: [],
      },
    ],
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-02T11:30:00Z",
    ...overrides,
  };
}

describe("WordEditor", () => {
  it("creates a word with one required sense and a shared audio association", async () => {
    tokenVault.install("access", "refresh");
    const saved = word();
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(saved, config.method === "POST" ? 201 : 200),
      ),
    );
    const user = userEvent.setup();
    const { router } = renderAppAt("/words/new");

    const heading = await screen.findByRole("heading", {
      level: 1,
      name: "新建单词",
    });
    const header = heading.closest("header");
    expect(within(header).getByRole("link", { name: "返回列表" })).toHaveClass(
      "border",
    );
    expect(screen.getAllByLabelText("释义 *")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "删除释义 1" })).toBeDisabled();

    await user.type(screen.getByLabelText("词头 *"), "hello");
    await user.type(screen.getByLabelText("释义 *"), "你好");
    await user.click(screen.getByRole("button", { name: "模拟选择单词音频" }));
    await user.click(within(header).getByRole("button", { name: "保存" }));

    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/words/${WORD_ID}`),
    );
    const createRequest = requestMock.mock.calls
      .map(([config]) => config)
      .find(
        (config) => config.url === "/admin/words" && config.method === "POST",
      );
    expect(createRequest.data).toEqual({
      headword: "hello",
      audioResourceId: AUDIO_ID,
      senses: [
        {
          partOfSpeech: "Noun",
          definition: "你好",
          usageNote: null,
          sortOrder: 0,
          examples: [],
        },
      ],
    });
    expect(await screen.findByText("单词已创建")).toBeVisible();
  });

  it("keeps invalid required content local", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");
    await user.type(await screen.findByLabelText("词头 *"), "hello");
    await user.click(screen.getByRole("button", { name: "保存" }));
    expect(screen.getByText("请先修正标记的字段。")).toBeVisible();
    expect(screen.getByText("请输入释义。")).toBeVisible();
  });

  it("collapses a completed sense and adds the next sense in place", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");

    await user.type(await screen.findByLabelText("释义 *"), "第一条释义");
    await user.click(
      screen.getByRole("button", { name: "收起并添加下一条释义" }),
    );

    expect(screen.getByRole("button", { name: "展开释义 1" })).toBeVisible();
    expect(screen.getByText("第一条释义")).toBeVisible();
    expect(screen.getByRole("button", { name: "折叠释义 2" })).toBeVisible();
    expect(screen.getAllByLabelText("释义 *")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "删除释义 2" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "展开释义 1" }));
    expect(screen.getAllByLabelText("释义 *")).toHaveLength(2);
  });

  it("keeps examples optional and supports adding them in place", async () => {
    const user = userEvent.setup();
    renderAppAt("/words/new");

    await screen.findByLabelText("释义 *");
    expect(screen.getByText("暂无例句。")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "添加例句" }));
    await user.type(screen.getByLabelText("例句原文 *"), "第一条例句");
    await user.click(
      screen.getByRole("button", { name: "收起并添加下一条例句" }),
    );

    expect(screen.getByRole("button", { name: "展开例句 1" })).toBeVisible();
    expect(screen.getByText("第一条例句")).toBeVisible();
    expect(screen.getByRole("button", { name: "折叠例句 2" })).toBeVisible();
  });

  it("preserves child ids, audio id, and concurrency stamp when updating", async () => {
    tokenVault.install("access", "refresh");
    const saved = word({
      senses: [
        {
          id: SENSE_ID,
          partOfSpeech: "Interjection",
          definition: "你好",
          usageNote: "问候语",
          sortOrder: 0,
          examples: [
            {
              id: EXAMPLE_ID,
              sentence: "Hello!",
              translation: "你好！",
              sortOrder: 0,
            },
          ],
        },
      ],
    });
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.method === "PUT" ? { ...saved, headword: "hi" } : saved,
        ),
      ),
    );
    const user = userEvent.setup();
    renderAppAt(`/words/${WORD_ID}`);
    const headword = await screen.findByLabelText("词头 *");
    await user.clear(headword);
    await user.type(headword, "hi");
    await user.click(screen.getByRole("button", { name: "保存" }));
    await screen.findByText("单词修改已保存。");

    const update = requestMock.mock.calls
      .map(([config]) => config)
      .find((config) => config.method === "PUT");
    expect(update.data).toEqual({
      headword: "hi",
      audioResourceId: AUDIO_ID,
      concurrencyStamp: saved.concurrencyStamp,
      senses: [
        {
          id: SENSE_ID,
          partOfSpeech: "Interjection",
          definition: "你好",
          usageNote: "问候语",
          sortOrder: 0,
          examples: [
            {
              id: EXAMPLE_ID,
              sentence: "Hello!",
              translation: "你好！",
              sortOrder: 0,
            },
          ],
        },
      ],
    });
  });

  it("shows only current metadata and no lifecycle or pronunciation controls", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(word())));
    renderAppAt(`/words/${WORD_ID}`);

    await screen.findByLabelText("词头 *");
    expect(screen.getByText("创建时间")).toBeVisible();
    expect(screen.getByText("更新时间")).toBeVisible();
    expect(screen.getByText("并发标识")).toBeVisible();
    expect(screen.queryByText("创建者")).toBeNull();
    expect(screen.queryByText("最后修改")).toBeNull();
    expect(screen.queryByRole("button", { name: "发布" })).toBeNull();
    expect(screen.queryByRole("button", { name: /下架/ })).toBeNull();
    expect(screen.queryByRole("button", { name: "添加发音" })).toBeNull();
    expect(screen.queryByLabelText("口音标签")).toBeNull();
    expect(screen.queryByLabelText("国际音标（IPA）")).toBeNull();
    expect(screen.queryByLabelText("设为默认发音")).toBeNull();
    expect(screen.queryByText(/草稿|已发布|已下架|已归档/)).toBeNull();
  });
});
