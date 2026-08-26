import type {
  LoaderCallbacks,
  LoaderConfiguration,
  LoaderContext,
} from "hls.js";
import { beforeEach, describe, expect, it, vi } from "vitest";

const { get, cancel, isAxiosError, isCancel } = vi.hoisted(() => ({
  get: vi.fn(),
  cancel: vi.fn(),
  isAxiosError: vi.fn(() => false),
  isCancel: vi.fn(() => false),
}));

vi.mock("axios", () => ({
  default: {
    create: vi.fn(() => ({ get })),
    CancelToken: {
      source: vi.fn(() => ({ token: {}, cancel })),
    },
    isAxiosError,
    isCancel,
  },
}));

import HlsAxiosLoader from "@/features/videos/hlsAxiosLoader";

const config = {
  timeout: 1_000,
  loadPolicy: { maxLoadTimeMs: 1_000 },
} as LoaderConfiguration;

describe("HlsAxiosLoader", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads HLS playlists as text so hls.js can parse them", async () => {
    get.mockResolvedValue({
      data: "#EXTM3U\n#EXT-X-VERSION:3",
      status: 200,
    });
    const onSuccess = vi.fn();
    const callbacks = {
      onSuccess,
      onError: vi.fn(),
      onTimeout: vi.fn(),
    } as unknown as LoaderCallbacks<LoaderContext>;
    const loader = new HlsAxiosLoader();

    loader.load(
      {
        url: "https://media.example.test/master.m3u8",
        responseType: "text",
      },
      config,
      callbacks,
    );

    await vi.waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
    expect(get).toHaveBeenCalledWith(
      "https://media.example.test/master.m3u8",
      expect.objectContaining({ responseType: "text" }),
    );
    expect(onSuccess.mock.calls[0]?.[0].data).toBe(
      "#EXTM3U\n#EXT-X-VERSION:3",
    );
  });

  it("loads ranged media segments as binary data", async () => {
    const segment = new Uint8Array([0, 1, 2, 3]).buffer;
    get.mockResolvedValue({ data: segment, status: 206 });
    const onSuccess = vi.fn();
    const callbacks = {
      onSuccess,
      onError: vi.fn(),
      onTimeout: vi.fn(),
    } as unknown as LoaderCallbacks<LoaderContext>;
    const loader = new HlsAxiosLoader();

    loader.load(
      {
        url: "https://media.example.test/segment.ts",
        responseType: "arraybuffer",
        rangeStart: 100,
        rangeEnd: 200,
      },
      config,
      callbacks,
    );

    await vi.waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
    expect(get).toHaveBeenCalledWith(
      "https://media.example.test/segment.ts",
      expect.objectContaining({
        responseType: "arraybuffer",
        headers: { Range: "bytes=100-199" },
      }),
    );
    expect(onSuccess.mock.calls[0]?.[0].data).toBe(segment);
  });
});
