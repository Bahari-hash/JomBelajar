import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { axiosHttpError, axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const READY_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const FAILED_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

function audioItem(overrides = {}) {
  return {
    id: READY_ID,
    name: "lesson.mp3",
    status: "Ready",
    durationSeconds: 42.5,
    lastFailureCode: null,
    updatedAt: "2026-08-16T08:00:00Z",
    ...overrides,
  };
}

function audioDetails(overrides = {}) {
  return {
    ...audioItem(),
    sampleRate: 44100,
    channels: 2,
    containerFormat: "mp3",
    sourceCodec: "mp3",
    currentOutputVersion: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
    concurrencyStamp: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
    createdAt: "2026-08-16T07:00:00Z",
    ...overrides,
  };
}

function capability() {
  return {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [{ extension: ".mp3", contentTypes: ["audio/mpeg"] }],
    multipartThresholdBytes: 8 * 1024 * 1024,
    partSizeBytes: 5 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
  };
}

function audioPage() {
  return {
    items: [
      audioItem(),
      audioItem({
        id: FAILED_ID,
        name: "failed.wav",
        status: "Failed",
        durationSeconds: null,
        lastFailureCode: "AudioProcessingFailed",
      }),
    ],
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
  };
}

function installAudioServer(overrides = {}) {
  return mockHttpClient((config) => {
    if (overrides.request) {
      const result = overrides.request(config);
      if (result) return result;
    }
    if (config.url.includes("/capabilities"))
      return Promise.resolve(axiosResponse(capability()));
    if (config.url.startsWith("/admin/audio?"))
      return Promise.resolve(axiosResponse(audioPage()));
    if (config.url.endsWith("/playback"))
      return Promise.resolve(
        axiosResponse({
          url: "https://media.example.test/lesson.mp3",
          expiresAt: null,
          durationSeconds: 42.5,
        }),
      );
    return Promise.resolve(axiosResponse(audioDetails()));
  });
}

describe("AudioLibrary", () => {
  it("restores search filters and renders canonical resource states", async () => {
    const requestMock = installAudioServer();

    renderAppAt("/audio?keyword=lesson&status=Ready");

    expect(
      await screen.findByRole("heading", { level: 1, name: "音频资源" }),
    ).toBeVisible();
    expect(screen.getByRole("searchbox", { name: "搜索音频名称" })).toHaveValue(
      "lesson",
    );
    expect(screen.getAllByText("可播放").length).toBeGreaterThan(0);
    expect(screen.getByText("处理失败")).toBeVisible();
    expect(await screen.findByTitle("音频暂不可用")).toBeDisabled();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url ===
          "/admin/audio?page=1&pageSize=20&keyword=lesson&status=Ready",
      ),
    ).toBe(true);
  });

  it("requests a playback URL before rendering a Ready audio player", async () => {
    const requestMock = installAudioServer();
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "试听 lesson.mp3" }),
    );

    const player = await screen.findByLabelText("正在试听 lesson.mp3");
    expect(player).toHaveAttribute(
      "src",
      "https://media.example.test/lesson.mp3",
    );
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === `/audio/${READY_ID}/playback` &&
          config.method === "POST",
      ),
    ).toBe(true);
  });

  it("shows an explicit message when renaming conflicts with another resource", async () => {
    installAudioServer({
      request: (config) =>
        config.url.endsWith(`/${READY_ID}/name`)
          ? Promise.reject(
              axiosHttpError(
                {
                  detail: "Audio resource name already exists.",
                  errorCode: "AudioNameConflict",
                },
                409,
              ),
            )
          : null,
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "重命名 lesson.mp3" }),
    );
    const input = screen.getByRole("textbox", { name: "音频名称" });
    await user.clear(input);
    await user.type(input, "duplicate.mp3");
    await user.click(screen.getByRole("button", { name: "保存名称" }));

    expect(
      await screen.findByText("已有同名音频资源，请使用其他名称。"),
    ).toBeVisible();
  });

  it("allows a Failed resource to be reprocessed", async () => {
    const requestMock = installAudioServer({
      request: (config) =>
        config.url.endsWith(`/${FAILED_ID}/reprocess`)
          ? Promise.resolve(
              axiosResponse(
                audioDetails({
                  id: FAILED_ID,
                  name: "failed.wav",
                  status: "Queued",
                  durationSeconds: null,
                  lastFailureCode: null,
                }),
              ),
            )
          : null,
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "重新处理 failed.wav" }),
    );

    expect(await screen.findByText("已提交重新处理。")).toBeVisible();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === `/admin/audio/${FAILED_ID}/reprocess` &&
          config.method === "POST",
      ),
    ).toBe(true);
  });

  it("shows an explicit message when a referenced resource cannot be deleted", async () => {
    installAudioServer({
      request: (config) =>
        config.url === `/admin/audio/${READY_ID}` && config.method === "DELETE"
          ? Promise.reject(
              axiosHttpError(
                {
                  detail: "Audio resource is in use.",
                  errorCode: "AudioInUse",
                },
                409,
              ),
            )
          : null,
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "删除 lesson.mp3" }),
    );
    await user.click(screen.getByRole("button", { name: "确认删除" }));

    expect(
      await screen.findByText("该音频正在被其他内容使用，无法删除。"),
    ).toBeVisible();
  });
});
