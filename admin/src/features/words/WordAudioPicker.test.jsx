import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { WordAudioPicker } from "@/features/words/WordAudioPicker.jsx";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const IDS = Object.freeze({
  published: "77777777-7777-4777-8777-777777777777",
  draft: "88888888-8888-4888-8888-888888888888",
  failed: "99999999-9999-4999-8999-999999999999",
  queued: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  resource: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
});

function capability() {
  return {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [{ extension: ".wav", contentTypes: ["audio/wav"] }],
    multipartThresholdBytes: 256 * 1024 * 1024,
    partSizeBytes: 16 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
  };
}

function listAudio(overrides = {}) {
  return {
    id: IDS.published,
    title: "bonjour 发音",
    languageTag: "fr",
    kind: "WordPronunciation",
    processingStatus: "Ready",
    publicationStatus: "Published",
    durationSeconds: 1.5,
    failureCode: null,
    updatedAt: "2026-08-01T10:00:00Z",
    ...overrides,
  };
}

function audioDetails(overrides = {}) {
  const list = listAudio(overrides);
  return {
    id: list.id,
    sourceMediaResourceId: IDS.resource,
    title: list.title,
    description: null,
    languageTag: list.languageTag,
    kind: list.kind,
    processingStatus: list.processingStatus,
    publicationStatus: list.publicationStatus,
    durationSeconds: list.durationSeconds,
    sampleRate: 44100,
    channels: 1,
    containerFormat: "mp3",
    sourceCodec: "pcm_s16le",
    failureCode: null,
    publishedAt:
      list.publicationStatus === "Published" ? "2026-08-01T10:00:00Z" : null,
    createdAt: "2026-08-01T09:00:00Z",
    updatedAt: list.updatedAt,
  };
}

function renderPicker(options = {}) {
  return render(
    <Provider store={createAppStore()}>
      <ThemeProvider>
        <TooltipProvider>
          <WordAudioPicker
            kind="WordPronunciation"
            language="fr"
            selectedId={null}
            onSelect={options.onSelect ?? (() => {})}
            onClose={options.onClose ?? (() => {})}
          />
        </TooltipProvider>
      </ThemeProvider>
    </Provider>,
  );
}

afterEach(() => vi.restoreAllMocks());

describe("WordAudioPicker", () => {
  it("loads safe metadata, requests playback on demand and selects published audio", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
    vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.includes("/playback"))
        return Promise.resolve(
          axiosResponse({
            url: "https://media.example.test/bonjour.mp3",
            expiresAt: null,
            durationSeconds: 1.5,
            languageTag: "fr",
            audioClipKind: "WordPronunciation",
          }),
        );
      return Promise.resolve(
        axiosResponse({
          items: [listAudio()],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    const onSelect = vi.fn();
    const user = userEvent.setup();
    renderPicker({ onSelect });

    expect(await screen.findByText("bonjour 发音")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "试听 bonjour 发音" }));
    expect(await screen.findByText("正在试听：bonjour 发音")).toBeVisible();
    expect(document.querySelector("audio")?.getAttribute("src")).toBe(
      "https://media.example.test/bonjour.mp3",
    );
    await user.click(screen.getByRole("button", { name: "选择" }));
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        id: IDS.published,
        kind: "WordPronunciation",
      }),
    );
    const listRequest = requestMock.mock.calls
      .map(([config]) => config)
      .find((config) => config.url.startsWith("/admin/audio?"));
    expect(listRequest.url).toBe(
      "/admin/audio?page=1&pageSize=20&kind=WordPronunciation&language=fr",
    );
    expect(
      requestMock.mock.calls.some(
        ([config]) => config.url === `/audio/${IDS.published}/playback`,
      ),
    ).toBe(true);
  });

  it("shows processing states, retries failures and publishes Ready audio before selecting", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/publish"))
        return Promise.resolve(
          axiosResponse(
            audioDetails({
              id: IDS.draft,
              title: "待发布发音",
              publicationStatus: "Published",
            }),
          ),
        );
      if (config.url.endsWith("/retry"))
        return Promise.resolve(
          axiosResponse(
            audioDetails({
              id: IDS.failed,
              title: "失败发音",
              processingStatus: "Queued",
              publicationStatus: "Draft",
              durationSeconds: null,
            }),
          ),
        );
      return Promise.resolve(
        axiosResponse({
          items: [
            listAudio({
              id: IDS.draft,
              title: "待发布发音",
              publicationStatus: "Draft",
            }),
            listAudio({
              id: IDS.failed,
              title: "失败发音",
              processingStatus: "Failed",
              publicationStatus: "Draft",
              durationSeconds: null,
              failureCode: "ProbeFailed",
            }),
            listAudio({
              id: IDS.queued,
              title: "排队发音",
              processingStatus: "Queued",
              publicationStatus: "Draft",
              durationSeconds: null,
            }),
          ],
          page: 1,
          pageSize: 20,
          totalCount: 3,
          totalPages: 1,
        }),
      );
    });
    const onSelect = vi.fn();
    const onClose = vi.fn();
    const user = userEvent.setup();
    renderPicker({ onSelect, onClose });

    expect(await screen.findByText("待发布发音")).toBeVisible();
    expect(screen.getByText("处理失败")).toBeVisible();
    expect(screen.getByText("等待处理")).toBeVisible();
    expect(screen.queryByText("ProbeFailed")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "重试处理" }));
    expect(await screen.findByText("“失败发音”已重新提交处理。")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "发布并选择" }));
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        id: IDS.draft,
        publicationStatus: "Published",
      }),
    );
    expect(onClose).toHaveBeenCalled();
    expect(
      requestMock.mock.calls.some(
        ([config]) => config.url === `/admin/audio/${IDS.failed}/retry`,
      ),
    ).toBe(true);
    expect(
      requestMock.mock.calls.some(
        ([config]) => config.url === `/admin/audio/${IDS.draft}/publish`,
      ),
    ).toBe(true);
  });
});
