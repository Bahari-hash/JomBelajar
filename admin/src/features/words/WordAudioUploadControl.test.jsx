import { AxiosHeaders } from "axios";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WordAudioUploadControl } from "@/features/words/WordAudioUploadControl.jsx";
import { objectStorageClient } from "@/services/objectStorageTransport.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosHttpError, axiosResponse, mockHttpClient } from "@/test/http.js";

const RESOURCE_ID = "99999999-9999-4999-8999-999999999999";
const AUDIO_ID = "77777777-7777-4777-8777-777777777777";

function capability() {
  return {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [
      { extension: ".mp3", contentTypes: ["audio/mpeg"] },
      { extension: ".wav", contentTypes: ["audio/wav", "audio/x-wav"] },
    ],
    multipartThresholdBytes: 256 * 1024 * 1024,
    partSizeBytes: 16 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
  };
}

function audioDetails() {
  return {
    id: AUDIO_ID,
    sourceMediaResourceId: RESOURCE_ID,
    title: "bonjour",
    description: null,
    kind: "WordPronunciation",
    processingStatus: "Queued",
    publicationStatus: "Draft",
    durationSeconds: null,
    sampleRate: null,
    channels: null,
    containerFormat: null,
    sourceCodec: null,
    failureCode: null,
    publishedAt: null,
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: "2026-08-01T10:00:00Z",
  };
}

afterEach(() => vi.restoreAllMocks());

describe("WordAudioUploadControl", () => {
  it("uploads through presign, isolated Axios PUT, confirm and AudioClip create", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockImplementation(
      async (config) => {
        config.onUploadProgress?.({ loaded: 4, total: 4 });
        return { status: 200, headers: new AxiosHeaders() };
      },
    );
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/media/presign"))
        return Promise.resolve(
          axiosResponse({
            resourceId: RESOURCE_ID,
            presignedUrl: "https://storage.example.test/bonjour.wav",
            objectName: "audio/bonjour.wav",
          }),
        );
      if (config.url.endsWith("/confirm"))
        return Promise.resolve(
          axiosResponse({
            id: RESOURCE_ID,
            uploaderId: "22222222-2222-4222-8222-222222222222",
            objectName: "audio/bonjour.wav",
            originalName: "bonjour.wav",
            module: "Audio",
            status: "Active",
            size: 4,
            extension: ".wav",
            contentType: "audio/wav",
            url: null,
            createdAt: "2026-08-01T10:00:00Z",
          }),
        );
      return Promise.resolve(axiosResponse(audioDetails(), 201));
    });
    const onCreated = vi.fn();
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <WordAudioUploadControl
          kind="WordPronunciation"
          onCreated={onCreated}
        />
      </Provider>,
    );

    expect(await screen.findByText(/支持 .mp3、.wav/)).toBeVisible();
    const file = new File(["wave"], "bonjour.wav", { type: "audio/wav" });
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: { files: [file] },
    });
    expect(screen.getByLabelText("音频标题")).toHaveValue("bonjour");
    await user.click(screen.getByRole("button", { name: "上传并提交处理" }));

    await waitFor(() =>
      expect(onCreated).toHaveBeenCalledWith(
        expect.objectContaining({ id: AUDIO_ID, processingStatus: "Queued" }),
      ),
    );
    expect(objectStorageClient.request).toHaveBeenCalledWith(
      expect.objectContaining({
        url: "https://storage.example.test/bonjour.wav",
        method: "PUT",
        data: file,
        withCredentials: false,
        headers: { "Content-Type": "audio/wav" },
      }),
    );
    expect(
      objectStorageClient.request.mock.calls[0][0].headers,
    ).not.toHaveProperty("Authorization");
    expect(
      requestMock.mock.calls.map(([config]) => [config.url, config.data]),
    ).toEqual([
      ["/uploads/admin/media/capabilities?module=Audio", undefined],
      [
        "/uploads/admin/media/presign",
        {
          originalName: "bonjour.wav",
          extension: ".wav",
          contentType: "audio/wav",
          size: 4,
          module: "Audio",
        },
      ],
      [`/uploads/resources/${RESOURCE_ID}/confirm`, undefined],
      [
        "/admin/audio",
        {
          sourceMediaResourceId: RESOURCE_ID,
          title: "bonjour",
          description: null,
          kind: "WordPronunciation",
        },
      ],
    ]);
  });

  it("rejects files outside the server capability before presigning", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse(capability())),
    );
    const { container } = render(
      <Provider store={createAppStore()}>
        <WordAudioUploadControl
          kind="ExampleSentence"
          onCreated={() => {}}
        />
      </Provider>,
    );
    await screen.findByText(/支持 .mp3、.wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["text"], "sentence.txt", { type: "text/plain" })],
      },
    });
    expect(screen.getByText("文件扩展名或媒体类型不受支持。")).toBeVisible();
    expect(requestMock).toHaveBeenCalledTimes(1);
  });

  it("reuses a confirmed media resource when AudioClip creation is retried", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders(),
    });
    let createAttempts = 0;
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("/capabilities"))
        return Promise.resolve(axiosResponse(capability()));
      if (config.url.endsWith("/media/presign"))
        return Promise.resolve(
          axiosResponse({
            resourceId: RESOURCE_ID,
            presignedUrl: "https://storage.example.test/bonjour.wav",
            objectName: "audio/bonjour.wav",
          }),
        );
      if (config.url.endsWith("/confirm"))
        return Promise.resolve(
          axiosResponse({
            id: RESOURCE_ID,
            uploaderId: "22222222-2222-4222-8222-222222222222",
            objectName: "audio/bonjour.wav",
            originalName: "bonjour.wav",
            module: "Audio",
            status: "Active",
            size: 4,
            extension: ".wav",
            contentType: "audio/wav",
            url: null,
            createdAt: "2026-08-01T10:00:00Z",
          }),
        );
      createAttempts += 1;
      return createAttempts === 1
        ? Promise.reject(
            axiosHttpError(
              {
                detail: "暂时无法创建音频。",
                errorCode: "AudioProcessingUnavailable",
              },
              503,
            ),
          )
        : Promise.resolve(axiosResponse(audioDetails(), 201));
    });
    const onCreated = vi.fn();
    const user = userEvent.setup();
    const { container } = render(
      <Provider store={createAppStore()}>
        <WordAudioUploadControl
          kind="WordPronunciation"
          onCreated={onCreated}
        />
      </Provider>,
    );
    await screen.findByText(/支持 .mp3、.wav/);
    fireEvent.change(container.querySelector('input[type="file"]'), {
      target: {
        files: [new File(["wave"], "bonjour.wav", { type: "audio/wav" })],
      },
    });
    await user.click(screen.getByRole("button", { name: "上传并提交处理" }));
    expect(await screen.findByText("暂时无法创建音频。")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "上传并提交处理" }));
    await waitFor(() => expect(onCreated).toHaveBeenCalled());

    const urls = requestMock.mock.calls.map(([config]) => config.url);
    expect(
      urls.filter((url) => url === "/uploads/admin/media/presign"),
    ).toHaveLength(1);
    expect(
      urls.filter((url) => url === `/uploads/resources/${RESOURCE_ID}/confirm`),
    ).toHaveLength(1);
    expect(urls.filter((url) => url === "/admin/audio")).toHaveLength(2);
    expect(objectStorageClient.request).toHaveBeenCalledTimes(1);
  });
});
