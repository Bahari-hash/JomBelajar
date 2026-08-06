import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import {
  adminVideo,
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

vi.mock("@/features/videos/VideoPlayer.jsx", () => ({
  VideoPlayer: ({ playback }) => (
    <div aria-label="视频播放器">{playback.masterPlaylistUrl}</div>
  ),
}));

describe("VideoDetails", () => {
  it("previews a ready draft through the administrator playback endpoint", async () => {
    const user = userEvent.setup();
    const initial = adminVideo({ publicationStatus: "Draft" });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(axiosResponse(videoCoverCapability()));
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.url.endsWith("/playback"))
        return Promise.resolve(
          axiosResponse({
            masterPlaylistUrl: "https://media.example.test/master.m3u8",
            posterUrl: null,
            expiresAt: null,
            durationSeconds: 42.5,
            positionSeconds: 0,
            isCompleted: false,
          }),
        );
      return Promise.resolve(axiosResponse(initial));
    });

    renderAppAt(`/videos/${initial.id}`);
    await user.click(
      await screen.findByRole("button", { name: "加载播放预览" }),
    );

    expect(await screen.findByLabelText("视频播放器")).toHaveTextContent(
      "https://media.example.test/master.m3u8",
    );
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === `/admin/videos/${initial.id}/playback` &&
          config.method === "POST",
      ),
    ).toBe(true);
  });

  it("does not offer playback before processing is ready", async () => {
    const initial = adminVideo({ processingStatus: "Processing" });
    mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(axiosResponse(videoCoverCapability()));
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      return Promise.resolve(axiosResponse(initial));
    });

    renderAppAt(`/videos/${initial.id}`);
    await screen.findByLabelText(/^标题/);

    expect(
      screen.queryByRole("button", { name: "加载播放预览" }),
    ).not.toBeInTheDocument();
  });

  it("saves metadata with the latest concurrency stamp", async () => {
    const user = userEvent.setup();
    const initial = adminVideo();
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(axiosResponse(videoCoverCapability()));
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.method === "PUT")
        return Promise.resolve(
          axiosResponse(
            adminVideo({
              title: config.data.title,
              concurrencyStamp: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            }),
          ),
        );
      return Promise.resolve(axiosResponse(initial));
    });
    renderAppAt(`/videos/${initial.id}`);
    const title = await screen.findByLabelText(/^标题/);
    await user.clear(title);
    await user.type(title, "Updated title");
    await user.click(screen.getByRole("button", { name: "保存修改" }));
    await screen.findByText("视频信息已保存。");
    const update = requestMock.mock.calls.find(
      ([config]) => config.method === "PUT",
    )[0];
    expect(update.data).toMatchObject({
      title: "Updated title",
      concurrencyStamp: initial.concurrencyStamp,
    });
  });

  it("keeps local input when the server reports a concurrency conflict", async () => {
    const user = userEvent.setup();
    const initial = adminVideo();
    mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(axiosResponse(videoCoverCapability()));
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.method === "PUT")
        return Promise.reject(
          axiosHttpError(
            {
              status: 409,
              detail: "Conflict",
              errorCode: "VideoConcurrencyConflict",
            },
            409,
          ),
        );
      return Promise.resolve(axiosResponse(initial));
    });
    renderAppAt(`/videos/${initial.id}`);
    const title = await screen.findByLabelText(/^标题/);
    await user.clear(title);
    await user.type(title, "Local unsaved title");
    await user.click(screen.getByRole("button", { name: "保存修改" }));
    expect(await screen.findByText(/你的输入仍然保留/)).toBeVisible();
    expect(title).toHaveValue("Local unsaved title");
  });

  it("confirms clearing a custom cover and sends the explicit Clear action", async () => {
    const user = userEvent.setup();
    const initial = adminVideo({
      cover: {
        id: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
        originalName: "cover.jpg",
        url: "https://media.example.test/cover.jpg",
      },
    });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(axiosResponse(videoCoverCapability()));
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.method === "PUT")
        return Promise.resolve(axiosResponse(adminVideo({ cover: null })));
      return Promise.resolve(axiosResponse(initial));
    });

    renderAppAt(`/videos/${initial.id}`);
    await user.click(await screen.findByRole("button", { name: "清除封面" }));
    await user.click(screen.getByRole("button", { name: "确认清除" }));
    await user.click(screen.getByRole("button", { name: "保存修改" }));

    const update = requestMock.mock.calls.find(
      ([config]) => config.method === "PUT",
    )[0];
    expect(update.data).toMatchObject({
      coverAction: "Clear",
      coverMediaResourceId: null,
    });
  });
});

function videoCoverCapability() {
  return {
    module: "VideoCover",
    maxSizeBytes: 5_242_880,
    allowedTypes: [{ extension: ".jpg", contentTypes: ["image/jpeg"] }],
    multipartThresholdBytes: 268_435_456,
    partSizeBytes: 16_777_216,
    maxPartCount: 10_000,
    partPresignBatchLimit: 20,
  };
}
