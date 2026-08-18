import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import WordFavoritesPanel from "./WordFavoritesPanel";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordFavoritesPanel", () => {
  it("renders as a closable dialog in modal mode", async () => {
    const onClose = vi.fn();
    httpClient.defaults.adapter = (async (config) => ({
      data: {
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 1,
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel modal onClose={onClose} />
      </Provider>,
    );

    expect(await screen.findByRole("dialog", { name: "收藏本" })).toHaveClass(
      "modal",
      "modal-open",
    );
    await user.click(screen.getByRole("button", { name: "关闭收藏本" }));
    expect(onClose).toHaveBeenCalledTimes(1);
    await user.click(screen.getByRole("button", { name: "关闭收藏本弹窗" }));
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("renders a page and keeps the row after a failed removal", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      if (config.method === "delete") throw new Error("failed");
      return {
        data: {
          items: [
            {
              wordId: "word-1",
              headword: "hello",
              senses: [],
              audioResourceId: null,
              createdAt: "2026-08-18T00:00:00Z",
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(
      await screen.findByRole("button", { name: "取消收藏 hello" }),
    );
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "收藏状态更新失败",
    );
    expect(screen.getByText("hello")).toBeInTheDocument();
    expect(requests.some((request) => request.method === "delete")).toBe(true);
  });

  it("clamps the page after removing the last item", async () => {
    let totalCount = 21;
    const pages: number[] = [];
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "delete") {
        totalCount = 20;
        return {
          data: null,
          status: 204,
          statusText: "No Content",
          headers: new AxiosHeaders(),
          config,
        };
      }
      const page = Number(config.params?.page ?? 1);
      pages.push(page);
      return {
        data: {
          items: [
            {
              wordId: `word-${page}`,
              headword: `hello-${page}`,
              senses: [],
              audioResourceId: null,
              createdAt: "2026-08-18T00:00:00Z",
            },
          ],
          page,
          pageSize: 20,
          totalCount,
          totalPages: Math.ceil(totalCount / 20),
        },
        status: 200,
        statusText: "OK",
        headers: new AxiosHeaders(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );
    await user.click(await screen.findByRole("button", { name: "下一页" }));
    await screen.findByText("hello-2");
    await user.click(screen.getByRole("button", { name: "取消收藏 hello-2" }));
    await waitFor(() => expect(pages.at(-1)).toBe(1));
    expect(screen.getByText("hello-1")).toBeInTheDocument();
  });
});
