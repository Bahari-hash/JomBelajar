import { AxiosError, AxiosHeaders } from "axios";
import { describe, expect, it, vi } from "vitest";
import { AxiosHlsLoader } from "@/services/axiosHlsLoader.js";

function config() {
  return { timeout: 1000, loadPolicy: { maxLoadTimeMs: 1000 } };
}

describe("AxiosHlsLoader", () => {
  it("loads array buffers with an exclusive-end Range and no credentials", async () => {
    const loader = new AxiosHlsLoader();
    const request = vi.spyOn(loader.client, "request").mockResolvedValue({
      data: new Uint8Array([1, 2, 3]).buffer,
      status: 206,
      headers: new AxiosHeaders({ age: "2" }),
      request: { responseURL: "https://media.example.test/segment.ts" },
    });
    const onSuccess = vi.fn();
    loader.load(
      {
        url: "https://media.example.test/segment.ts",
        responseType: "arraybuffer",
        rangeStart: 5,
        rangeEnd: 10,
      },
      config(),
      { onSuccess, onError: vi.fn(), onTimeout: vi.fn() },
    );
    await vi.waitFor(() => expect(onSuccess).toHaveBeenCalledOnce());
    expect(request.mock.calls[0][0]).toMatchObject({
      responseType: "arraybuffer",
      headers: { Range: "bytes=5-9" },
      withCredentials: false,
    });
    expect(request.mock.calls[0][0].headers).not.toHaveProperty(
      "Authorization",
    );
    expect(loader.getCacheAge()).toBe(2);
    loader.destroy();
  });

  it("reports timeouts without exposing provider response bodies", async () => {
    const loader = new AxiosHlsLoader();
    vi.spyOn(loader.client, "request").mockRejectedValue(
      new AxiosError("timeout", "ECONNABORTED"),
    );
    const onTimeout = vi.fn();
    loader.load(
      { url: "https://media.example.test/master.m3u8", responseType: "text" },
      config(),
      { onSuccess: vi.fn(), onError: vi.fn(), onTimeout },
    );
    await vi.waitFor(() => expect(onTimeout).toHaveBeenCalledOnce());
  });
});
