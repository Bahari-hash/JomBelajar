import axios from "axios";

/** Axios-only hls.js loader that never inherits TinyLang API credentials. */
export class AxiosHlsLoader {
  constructor() {
    this.client = axios.create({ withCredentials: false });
    this.controller = new AbortController();
    this.context = null;
    this.callbacks = null;
    this.response = null;
    this.stats = {
      aborted: false,
      loaded: 0,
      retry: 0,
      total: 0,
      chunkCount: 0,
      bwEstimate: 0,
      loading: { start: 0, first: 0, end: 0 },
      parsing: { start: 0, end: 0 },
      buffering: { start: 0, first: 0, end: 0 },
    };
  }

  load(context, config, callbacks) {
    this.context = context;
    this.callbacks = callbacks;
    this.stats.loading.start = performance.now();
    const headers = { ...(context.headers ?? {}) };
    delete headers.Authorization;
    delete headers.authorization;
    delete headers.Cookie;
    delete headers.cookie;
    if (Number.isFinite(context.rangeEnd))
      headers.Range = `bytes=${context.rangeStart ?? 0}-${context.rangeEnd - 1}`;
    const timeout = config.loadPolicy?.maxLoadTimeMs ?? config.timeout;
    this.client
      .request({
        url: context.url,
        method: "GET",
        responseType:
          context.responseType === "arraybuffer" ? "arraybuffer" : "text",
        headers,
        timeout,
        signal: this.controller.signal,
        withCredentials: false,
        onDownloadProgress: (event) => {
          if (!this.stats.loading.first)
            this.stats.loading.first = performance.now();
          this.stats.loaded = event.loaded;
          this.stats.total = event.total ?? this.stats.total;
        },
      })
      .then((response) => {
        this.response = response;
        const end = performance.now();
        this.stats.loading.first ||= end;
        this.stats.loading.end = end;
        const length = response.data?.byteLength ?? response.data?.length ?? 0;
        this.stats.loaded = length;
        this.stats.total ||= length;
        this.stats.chunkCount = length ? 1 : 0;
        const elapsed = Math.max(1, end - this.stats.loading.first);
        this.stats.bwEstimate = Math.round((length * 8000) / elapsed);
        callbacks.onProgress?.(this.stats, context, response.data, response);
        callbacks.onSuccess(
          {
            url: response.request?.responseURL ?? context.url,
            data: response.data,
            code: response.status,
          },
          this.stats,
          context,
          response,
        );
      })
      .catch((error) => {
        if (this.stats.aborted || axios.isCancel(error)) return;
        this.stats.loading.end = performance.now();
        if (error.code === "ECONNABORTED" || error.code === "ETIMEDOUT") {
          callbacks.onTimeout(this.stats, context, error.response ?? null);
          return;
        }
        callbacks.onError(
          { code: error.response?.status ?? 0, text: "媒体资源请求失败。" },
          context,
          error.response ?? null,
          this.stats,
        );
      });
  }

  abort() {
    if (this.stats.aborted || this.stats.loading.end) return;
    this.stats.aborted = true;
    this.controller.abort();
    this.callbacks?.onAbort?.(this.stats, this.context, this.response);
  }

  destroy() {
    this.abort();
    this.callbacks = null;
    this.context = null;
    this.response = null;
  }

  getResponseHeader(name) {
    const value =
      this.response?.headers?.get?.(name) ??
      this.response?.headers?.[name.toLowerCase()];
    return typeof value === "string" ? value : null;
  }

  getCacheAge() {
    const age = this.getResponseHeader("age");
    if (age === null) return null;
    const parsed = Number.parseFloat(age);
    return Number.isFinite(parsed) ? parsed : null;
  }
}
