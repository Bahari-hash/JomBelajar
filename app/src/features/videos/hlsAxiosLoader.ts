import axios, { type AxiosInstance, type CancelTokenSource } from "axios";
import type {
  Loader,
  LoaderCallbacks,
  LoaderConfiguration,
  LoaderContext,
  LoaderResponse,
  LoaderStats,
} from "hls.js";
import { isSafeVideoUrl } from "@/features/videos/videoUtils";

const mediaClient: AxiosInstance = axios.create({
  baseURL: "",
  withCredentials: false,
  responseType: "arraybuffer",
});

function createStats(start: number, loaded = 0, total = 0): LoaderStats {
  const now = performance.now();
  return {
    aborted: false,
    loaded,
    retry: 0,
    total,
    chunkCount: 0,
    bwEstimate: start < now && loaded ? (loaded * 8 * 1000) / (now - start) : 0,
    loading: { start, first: now, end: now },
    parsing: { start: 0, end: 0 },
    buffering: { start: 0, first: 0, end: 0 },
  };
}

/** Axios-backed hls.js loader that never forwards TinyLang authentication state. */
export default class HlsAxiosLoader implements Loader<LoaderContext> {
  context: LoaderContext | null = null;
  stats = createStats(performance.now());
  private source: CancelTokenSource | null = null;

  load(
    context: LoaderContext,
    config: LoaderConfiguration,
    callbacks: LoaderCallbacks<LoaderContext>,
  ) {
    this.context = context;
    const startedAt = performance.now();
    if (!isSafeVideoUrl(context.url)) {
      callbacks.onError(
        { code: 400, text: "媒体地址无效。" },
        context,
        null,
        createStats(startedAt),
      );
      return;
    }
    this.source = axios.CancelToken.source();
    const headers: Record<string, string> = {};
    if (context.rangeEnd) {
      headers.Range = `bytes=${context.rangeStart ?? 0}-${context.rangeEnd - 1}`;
    }
    const timeout = Math.max(
      1,
      config.loadPolicy.maxLoadTimeMs || config.timeout || 30000,
    );
    mediaClient
      .get<ArrayBuffer>(context.url, {
        cancelToken: this.source.token,
        timeout,
        headers,
      })
      .then((response) => {
        this.stats = createStats(
          startedAt,
          response.data.byteLength,
          response.data.byteLength,
        );
        const result: LoaderResponse = {
          url: context.url,
          data: response.data,
          code: response.status,
        };
        callbacks.onSuccess(result, this.stats, context, null);
      })
      .catch((error: unknown) => {
        this.stats = createStats(startedAt);
        if (axios.isCancel(error)) {
          this.stats.aborted = true;
          callbacks.onAbort?.(this.stats, context, null);
          return;
        }
        const timeoutError =
          axios.isAxiosError(error) &&
          (error.code === "ECONNABORTED" || error.code === "ETIMEDOUT");
        if (timeoutError) callbacks.onTimeout(this.stats, context, null);
        else
          callbacks.onError(
            {
              code: axios.isAxiosError(error)
                ? (error.response?.status ?? 500)
                : 500,
              text: "媒体加载失败。",
            },
            context,
            null,
            this.stats,
          );
      });
  }

  abort() {
    this.source?.cancel();
    this.source = null;
  }
  destroy() {
    this.abort();
    this.context = null;
  }
}
