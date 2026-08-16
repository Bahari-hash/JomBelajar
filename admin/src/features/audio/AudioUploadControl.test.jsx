import { AxiosHeaders } from "axios";
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { Provider } from "react-redux";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AudioUploadControl } from "@/features/audio/AudioUploadControl.jsx";
import { objectStorageClient } from "@/services/objectStorageTransport.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const MEDIA_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const SESSION_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

function capability(overrides = {}) {
  return {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [{ extension: ".wav", contentTypes: ["audio/wav"] }],
    multipartThresholdBytes: 8 * 1024 * 1024,
    partSizeBytes: 5 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
    ...overrides,
  };
}

function readyAudio() {
  return {
    id: AUDIO_ID,
    name: "lesson.wav",
    status: "Ready",
    durationSeconds: 1.25,
    sampleRate: 44100,
    channels: 1,
    containerFormat: "mp3",
    sourceCodec: "pcm_s16le",
    lastFailureCode: null,
    currentOutputVersion: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
    concurrencyStamp: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee",
    createdAt: "2026-08-16T07:00:00Z",
    updatedAt: "2026-08-16T08:00:00Z",
  };
}

function processingAudio() {
  return {
    ...readyAudio(),
    status: "Processing",
    durationSeconds: null,
    sampleRate: null,
    channels: null,
    containerFormat: null,
    sourceCodec: null,
    currentOutputVersion: null,
  };
}

afterEach(() => vi.restoreAllMocks());

describe("AudioUploadControl", () => {
  it("uploads a small file, confirms it and waits for Ready", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockImplementation(
      async (config) => {
        config.onUploadProgress?.({ loaded: 4, total: 4 });
        return { status: 200, headers: new AxiosHeaders() };
      },
    );
    let detailRequests = 0;
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/uploads/simple"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: "https://storage.example.test/lesson.wav",
              multipartSessionId: null,
              partSize: null,
              partCount: null,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/upload/confirm"))
        return Promise.resolve(axiosResponse(null, 204));
      detailRequests += 1;
      return Promise.resolve(
        axiosResponse(detailRequests === 1 ? processingAudio() : readyAudio()),
      );
    });
    const onStarted = vi.fn();
    const onCompleted = vi.fn();
    const user = userEvent.setup();
    const { container } = render(
      <StrictMode>
        <Provider store={createAppStore()}>
          <AudioUploadControl onStarted={onStarted} onCompleted={onCompleted} />
        </Provider>
      </StrictMode>,
    );

    await screen.findByText(/支持 .wav/);
    const file = new File(["wave"], "lesson.wav", { type: "audio/wav" });
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: { files: [file] },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    await waitFor(() =>
      expect(onCompleted).toHaveBeenCalledWith(
        expect.objectContaining({ id: AUDIO_ID, status: "Ready" }),
      ),
    );
    expect(onStarted).toHaveBeenCalledWith(AUDIO_ID, "lesson.wav");
    expect(objectStorageClient.request).toHaveBeenCalledWith(
      expect.objectContaining({
        url: "https://storage.example.test/lesson.wav",
        method: "PUT",
        data: file,
      }),
    );
    expect(
      requestMock.mock.calls.map(([config]) => [config.url, config.method]),
    ).toEqual([
      ["/uploads/admin/media/capabilities?module=Audio", "GET"],
      ["/admin/audio/uploads/simple", "POST"],
      [`/admin/audio/${AUDIO_ID}/upload/confirm`, "PUT"],
      [`/admin/audio/${AUDIO_ID}`, "GET"],
      [`/admin/audio/${AUDIO_ID}`, "GET"],
    ]);
  });

  it("returns after upload confirmation without waiting for Ready", async () => {
    tokenVault.install("access", "refresh");
    const storageMock = vi
      .spyOn(objectStorageClient, "request")
      .mockResolvedValue({ status: 200, headers: new AxiosHeaders() });
    let detailRequests = 0;
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/uploads/simple"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: "https://storage.example.test/lesson.wav",
              multipartSessionId: null,
              partSize: null,
              partCount: null,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/upload/confirm"))
        return Promise.resolve(axiosResponse(null, 204));
      detailRequests += 1;
      return Promise.resolve(
        axiosResponse(detailRequests === 1 ? processingAudio() : readyAudio()),
      );
    });
    const onStarted = vi.fn();
    const onCompleted = vi.fn();
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl
          waitForProcessing={false}
          onStarted={onStarted}
          onCompleted={onCompleted}
        />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    const file = new File(["wave"], "lesson.wav", { type: "audio/wav" });
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: { files: [file] },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    await waitFor(() =>
      expect(onCompleted).toHaveBeenCalledWith(
        expect.objectContaining({ id: AUDIO_ID, status: "Processing" }),
      ),
    );
    expect(onStarted).toHaveBeenCalledWith(AUDIO_ID, "lesson.wav");
    expect(onStarted.mock.invocationCallOrder[0]).toBeLessThan(
      storageMock.mock.invocationCallOrder[0],
    );
    expect(
      requestMock.mock.calls.filter(
        ([config]) => config.url === `/admin/audio/${AUDIO_ID}`,
      ),
    ).toHaveLength(1);
    expect(screen.getByText("源文件上传完成，后台处理中")).toBeVisible();
  });

  it("uses multipart upload above the server threshold", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders({ etag: "etag-1" }),
    });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(
          axiosResponse(
            capability({ multipartThresholdBytes: 1, partSizeBytes: 4 }),
          ),
        );
      if (config.url.endsWith("/uploads/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: null,
              multipartSessionId: SESSION_ID,
              partSize: 4,
              partCount: 1,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse([
            {
              partNumber: 1,
              presignedUrl: "https://storage.example.test/part-1",
              contentLength: 4,
              expiresAt: "2026-08-16T08:15:00Z",
            },
          ]),
        );
      if (config.url.endsWith("/complete"))
        return Promise.resolve(
          axiosResponse({
            resourceId: MEDIA_ID,
            sessionId: SESSION_ID,
            status: "Completed",
            partSize: 4,
            partCount: 1,
            expiresAt: "2026-08-16T08:15:00Z",
            uploadedParts: [{ partNumber: 1, eTag: "etag-1", size: 4 }],
          }),
        );
      if (config.url.endsWith("/upload/confirm"))
        return Promise.resolve(axiosResponse(null, 204));
      return Promise.resolve(axiosResponse(readyAudio()));
    });
    const onCompleted = vi.fn();
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl onCompleted={onCompleted} />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["wave"], "lesson.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    await waitFor(() => expect(onCompleted).toHaveBeenCalled());
    expect(requestMock.mock.calls.map(([config]) => config.url)).toContain(
      `/admin/audio/multipart/${SESSION_ID}/complete`,
    );
    expect(
      requestMock.mock.calls.find(([config]) =>
        config.url.endsWith("/complete"),
      )[0].data,
    ).toEqual({ parts: [{ partNumber: 1, eTag: "etag-1" }] });
  });

  it("cannot cancel or interrupt multipart finalization", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders({ etag: "etag-1" }),
    });
    let completeSignal;
    let resolveComplete;
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(
          axiosResponse(
            capability({ multipartThresholdBytes: 1, partSizeBytes: 4 }),
          ),
        );
      if (config.url.endsWith("/uploads/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: null,
              multipartSessionId: SESSION_ID,
              partSize: 4,
              partCount: 1,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse([
            {
              partNumber: 1,
              presignedUrl: "https://storage.example.test/part-1",
              contentLength: 4,
              expiresAt: "2026-08-16T08:15:00Z",
            },
          ]),
        );
      if (config.url.endsWith("/complete")) {
        completeSignal = config.signal;
        return new Promise((resolve) => {
          resolveComplete = resolve;
        });
      }
      if (config.url.endsWith("/upload/confirm"))
        return Promise.resolve(axiosResponse(null, 204));
      return Promise.resolve(axiosResponse(readyAudio()));
    });
    const user = userEvent.setup();
    const { container, unmount } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["wave"], "lesson.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    expect(await screen.findByText("正在完成分片上传")).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "取消" }),
    ).not.toBeInTheDocument();
    unmount();
    expect(completeSignal.aborted).toBe(false);

    await act(async () => {
      resolveComplete(
        axiosResponse({
          resourceId: MEDIA_ID,
          sessionId: SESSION_ID,
          status: "Completed",
          partSize: 4,
          partCount: 1,
          expiresAt: "2026-08-16T08:15:00Z",
          uploadedParts: [{ partNumber: 1, eTag: "etag-1", size: 4 }],
        }),
      );
    });
    await waitFor(() =>
      expect(
        requestMock.mock.calls.some(([config]) =>
          config.url.endsWith("/upload/confirm"),
        ),
      ).toBe(true),
    );
  });

  it("cannot cancel or interrupt upload confirmation", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders(),
    });
    let confirmSignal;
    let resolveConfirm;
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/uploads/simple"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: "https://storage.example.test/lesson.wav",
              multipartSessionId: null,
              partSize: null,
              partCount: null,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/upload/confirm")) {
        confirmSignal = config.signal;
        return new Promise((resolve) => {
          resolveConfirm = resolve;
        });
      }
      return Promise.resolve(axiosResponse(readyAudio()));
    });
    const user = userEvent.setup();
    const { container, unmount } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["wave"], "lesson.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    expect(await screen.findByText("正在确认上传")).toBeVisible();
    expect(
      screen.queryByRole("button", { name: "取消" }),
    ).not.toBeInTheDocument();
    unmount();
    expect(confirmSignal.aborted).toBe(false);

    await act(async () => {
      resolveConfirm(axiosResponse(null, 204));
    });
    expect(
      requestMock.mock.calls.some(
        ([config]) => config.url === `/admin/audio/${AUDIO_ID}`,
      ),
    ).toBe(false);
  });

  it("rejects duplicate multipart presign numbers before object upload", async () => {
    tokenVault.install("access", "refresh");
    const storageMock = vi
      .spyOn(objectStorageClient, "request")
      .mockResolvedValue({ status: 200, headers: new AxiosHeaders() });
    mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(
          axiosResponse(
            capability({ multipartThresholdBytes: 1, partSizeBytes: 4 }),
          ),
        );
      if (config.url.endsWith("/uploads/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: null,
              multipartSessionId: SESSION_ID,
              partSize: 4,
              partCount: 2,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      return Promise.resolve(
        axiosResponse([
          {
            partNumber: 1,
            presignedUrl: "https://storage.example.test/part-1-a",
            contentLength: 4,
            expiresAt: "2026-08-16T08:15:00Z",
          },
          {
            partNumber: 1,
            presignedUrl: "https://storage.example.test/part-1-b",
            contentLength: 4,
            expiresAt: "2026-08-16T08:15:00Z",
          },
        ]),
      );
    });
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["12345678"], "lesson.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));

    expect(
      await screen.findByText("服务端返回的分片预签名集合与请求不一致。"),
    ).toBeVisible();
    expect(storageMock).not.toHaveBeenCalled();
  });

  it("aborts a multipart session without deleting the created resource", async () => {
    tokenVault.install("access", "refresh");
    const storageMock = vi
      .spyOn(objectStorageClient, "request")
      .mockImplementation(
        (config) =>
          new Promise((_resolve, reject) => {
            config.signal.addEventListener(
              "abort",
              () => reject(new DOMException("Aborted", "AbortError")),
              { once: true },
            );
          }),
      );
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(
          axiosResponse(
            capability({ multipartThresholdBytes: 1, partSizeBytes: 4 }),
          ),
        );
      if (config.url.endsWith("/uploads/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              audioResourceId: AUDIO_ID,
              mediaResourceId: MEDIA_ID,
              presignedUrl: null,
              multipartSessionId: SESSION_ID,
              partSize: 4,
              partCount: 1,
              expiresAt: "2026-08-16T08:15:00Z",
            },
            201,
          ),
        );
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse([
            {
              partNumber: 1,
              presignedUrl: "https://storage.example.test/part-1",
              contentLength: 4,
              expiresAt: "2026-08-16T08:15:00Z",
            },
          ]),
        );
      return Promise.resolve(axiosResponse(null, 204));
    });
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <AudioUploadControl />
      </Provider>,
    );

    await screen.findByText(/支持 .wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["wave"], "lesson.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "开始上传" }));
    await waitFor(() => expect(storageMock).toHaveBeenCalled());
    await user.click(screen.getByRole("button", { name: "取消" }));

    await waitFor(() =>
      expect(
        requestMock.mock.calls.some(
          ([config]) =>
            config.url === `/admin/audio/multipart/${SESSION_ID}` &&
            config.method === "DELETE",
        ),
      ).toBe(true),
    );
    expect(
      await screen.findByText("上传已取消。已创建的音频资源会保留在列表中。"),
    ).toBeVisible();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url === `/admin/audio/${AUDIO_ID}` &&
          config.method === "DELETE",
      ),
    ).toBe(false);
  });
});
