import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { axiosHttpError, axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const READY_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const FAILED_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";

vi.mock("@/features/audio/AudioBatchUploadControl.jsx", async () => {
  const { useState } = await import("react");
  return {
    AudioBatchUploadControl: ({ onStarted, onTerminal }) => {
      const [resultVisible, setResultVisible] = useState(false);
      return (
        <section aria-label="批量音频上传">
          <button type="button" onClick={() => onStarted?.("batch-id")}>
            模拟开始上传
          </button>
          <button
            type="button"
            onClick={() => {
              setResultVisible(true);
              onTerminal?.({ stage: "completed" });
            }}
          >
            模拟上传完成
          </button>
          {resultVisible ? <span>批量结果仍然可见</span> : null}
        </section>
      );
    },
  };
});

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
  it("does not poll the audio list while the page is idle", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const requestMock = installAudioServer();
      renderAppAt("/audio");

      await screen.findByRole("heading", { level: 1, name: "音频资源" });
      const listRequestCount = () =>
        requestMock.mock.calls.filter(([config]) =>
          config.url.startsWith("/admin/audio?"),
        ).length;
      const initialCount = listRequestCount();

      await vi.advanceTimersByTimeAsync(6000);

      expect(listRequestCount()).toBe(initialCount);
    } finally {
      vi.useRealTimers();
    }
  });

  it("uses batch upload for new resources and refetches without clearing results", async () => {
    const requestMock = installAudioServer();
    const user = userEvent.setup();
    renderAppAt("/audio");

    expect(await screen.findByLabelText("批量音频上传")).toBeVisible();
    const listRequestCount = () =>
      requestMock.mock.calls.filter(([config]) =>
        config.url.startsWith("/admin/audio?"),
      ).length;
    const initialCount = listRequestCount();

    await user.click(screen.getByRole("button", { name: "模拟开始上传" }));
    await waitFor(() =>
      expect(listRequestCount()).toBeGreaterThan(initialCount),
    );
    const startedCount = listRequestCount();
    await user.click(screen.getByRole("button", { name: "模拟上传完成" }));

    await waitFor(() =>
      expect(listRequestCount()).toBeGreaterThan(startedCount),
    );
    expect(screen.getByText("批量结果仍然可见")).toBeVisible();
  });

  it("keeps the single-file control for retrying a failed resource", async () => {
    installAudioServer();
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "重新上传 failed.wav" }),
    );

    expect(screen.getByRole("heading", { name: "重新上传音频" })).toBeVisible();
    const retryInput = document.querySelector(`input#audio-retry-${FAILED_ID}`);
    expect(retryInput).toBeInTheDocument();
    expect(retryInput).not.toHaveAttribute("multiple");
  });

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
    expect(screen.getAllByText("处理失败").length).toBeGreaterThan(0);
    expect(await screen.findByTitle("音频暂不可用")).toBeDisabled();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url ===
          "/admin/audio?page=1&pageSize=20&keyword=lesson&status=Ready",
      ),
    ).toBe(true);
  });

  it("opens a themed native player dialog and requests playback", async () => {
    const requestMock = installAudioServer();
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "试听 lesson.mp3" }),
    );

    expect(screen.getByRole("heading", { name: "试听音频" })).toBeVisible();
    const player = await screen.findByLabelText("正在试听 lesson.mp3");
    expect(player).toHaveAttribute("controls");
    expect(player).toHaveAttribute("autoplay");
    expect(player).toHaveAttribute(
      "src",
      "https://media.example.test/lesson.mp3",
    );
    expect(player).toHaveStyle({ accentColor: "var(--primary)" });
    expect(player).toHaveClass(
      "[color-scheme:light]",
      "dark:[color-scheme:dark]",
    );
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === `/audio/${READY_ID}/playback` &&
          config.method === "POST",
      ),
    ).toBe(true);
  });

  it("opens the playback dialog immediately while the URL is loading", async () => {
    let resolvePlayback;
    installAudioServer({
      request: (config) =>
        config.url.endsWith("/playback")
          ? new Promise((resolve) => {
              resolvePlayback = resolve;
            })
          : null,
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "试听 lesson.mp3" }),
    );

    expect(screen.getByRole("heading", { name: "试听音频" })).toBeVisible();
    expect(screen.getByLabelText("正在加载 lesson.mp3")).toBeVisible();
    expect(screen.queryByLabelText("正在试听 lesson.mp3")).toBeNull();

    resolvePlayback(
      axiosResponse({
        url: "https://media.example.test/lesson.mp3",
        expiresAt: null,
        durationSeconds: 42.5,
      }),
    );
    expect(await screen.findByLabelText("正在试听 lesson.mp3")).toBeVisible();
  });

  it("shows playback errors in the dialog and retries", async () => {
    let playbackAttempts = 0;
    const requestMock = installAudioServer({
      request: (config) => {
        if (!config.url.endsWith("/playback")) return null;
        playbackAttempts += 1;
        return playbackAttempts === 1
          ? Promise.reject(
              axiosHttpError({ detail: "Playback unavailable." }, 503),
            )
          : Promise.resolve(
              axiosResponse({
                url: "https://media.example.test/lesson.mp3",
                expiresAt: null,
                durationSeconds: 42.5,
              }),
            );
      },
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "试听 lesson.mp3" }),
    );
    expect(await screen.findByText("Playback unavailable.")).toBeVisible();

    await user.click(screen.getByRole("button", { name: "重试" }));

    expect(await screen.findByLabelText("正在试听 lesson.mp3")).toBeVisible();
    expect(
      requestMock.mock.calls.filter(([config]) =>
        config.url.endsWith("/playback"),
      ),
    ).toHaveLength(2);
  });

  it("ignores a late playback response after the dialog is closed", async () => {
    let resolvePlayback;
    installAudioServer({
      request: (config) =>
        config.url.endsWith("/playback")
          ? new Promise((resolve) => {
              resolvePlayback = resolve;
            })
          : null,
    });
    const user = userEvent.setup();
    renderAppAt("/audio");

    await user.click(
      await screen.findByRole("button", { name: "试听 lesson.mp3" }),
    );
    expect(screen.getByLabelText("正在加载 lesson.mp3")).toBeVisible();

    await user.click(screen.getByRole("button", { name: "关闭" }));
    expect(screen.queryByRole("heading", { name: "试听音频" })).toBeNull();

    resolvePlayback(
      axiosResponse({
        url: "https://media.example.test/lesson.mp3",
        expiresAt: null,
        durationSeconds: 42.5,
      }),
    );

    await waitFor(() => {
      expect(screen.queryByRole("heading", { name: "试听音频" })).toBeNull();
      expect(screen.queryByLabelText("正在试听 lesson.mp3")).toBeNull();
    });
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
