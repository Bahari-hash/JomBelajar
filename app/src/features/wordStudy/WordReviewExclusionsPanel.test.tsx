import type { AxiosAdapter } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import WordReviewExclusionsPanel from "./WordReviewExclusionsPanel";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

describe("WordReviewExclusionsPanel", () => {
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
        <WordReviewExclusionsPanel modal onClose={onClose} />
      </Provider>,
    );

    expect(
      await screen.findByRole("dialog", { name: "已停止复习" }),
    ).toHaveClass("modal", "modal-open");
    await user.click(screen.getByRole("button", { name: "关闭已停止复习" }));
    expect(onClose).toHaveBeenCalledTimes(1);
    await user.click(
      screen.getByRole("button", { name: "关闭已停止复习弹窗" }),
    );
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("uses a dialog before restoring a word", async () => {
    const user = userEvent.setup();
    httpClient.defaults.adapter = (async (config) => ({
      data:
        config.method === "delete"
          ? null
          : {
              items: [
                {
                  wordId: "word-1",
                  headword: "hello",
                  senses: [],
                  audioResourceId: null,
                  excludedAt: "2026-08-18T00:00:00Z",
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
    })) as AxiosAdapter;
    render(
      <Provider store={createAppStore()}>
        <WordReviewExclusionsPanel />
      </Provider>,
    );

    await user.click(
      await screen.findByRole("button", { name: "恢复复习 hello" }),
    );
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "确认恢复复习" }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "取消" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("clamps the page after restoring the last item", async () => {
    let totalCount = 21;
    const pages: number[] = [];
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "delete" && config.url?.includes("exclusions")) {
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
              excludedAt: "2026-08-18T00:00:00Z",
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
        <WordReviewExclusionsPanel />
      </Provider>,
    );
    await user.click(await screen.findByRole("button", { name: "下一页" }));
    await screen.findByText("hello-2");
    await user.click(screen.getByRole("button", { name: "恢复复习 hello-2" }));
    await user.click(screen.getByRole("button", { name: "确认恢复复习" }));
    await waitFor(() => expect(pages.at(-1)).toBe(1));
    expect(screen.getByText("hello-1")).toBeInTheDocument();
  });

  it("shows restore failure inside the open dialog", async () => {
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "delete") throw new Error("failed");
      return {
        data: {
          items: [
            {
              wordId: "word-1",
              headword: "hello",
              senses: [],
              audioResourceId: null,
              excludedAt: "2026-08-18T00:00:00Z",
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
        <WordReviewExclusionsPanel />
      </Provider>,
    );
    await user.click(
      await screen.findByRole("button", { name: "恢复复习 hello" }),
    );
    await user.click(screen.getByRole("button", { name: "确认恢复复习" }));

    expect(
      await within(screen.getByRole("dialog")).findByRole("alert"),
    ).toHaveTextContent("恢复复习失败");
  });
});
