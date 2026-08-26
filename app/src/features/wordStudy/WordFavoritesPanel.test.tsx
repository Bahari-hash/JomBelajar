import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { useState } from "react";
import { Provider } from "react-redux";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";
import WordFavoritesPanel from "./WordFavoritesPanel";

const originalAdapter = httpClient.defaults.adapter;
afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

const favoriteWord = {
  wordId: "word-1",
  headword: "study",
  audioResourceId: "word-audio",
  createdAt: "2026-08-18T00:00:00Z",
  senses: [
    {
      partOfSpeech: "n.",
      definition: "书房",
      usageNote: null,
      sortOrder: 2,
      examples: [
        {
          sentence: "The study is quiet.",
          translation: "书房很安静。",
          sortOrder: 1,
          audioResourceId: null,
        },
      ],
    },
    {
      partOfSpeech: "v.",
      definition: "学习",
      usageNote: "用于描述获取知识。",
      sortOrder: 1,
      examples: [
        {
          sentence: "I study English.",
          translation: "我学习英语。",
          sortOrder: 1,
          audioResourceId: "example-audio",
        },
      ],
    },
  ],
};

function favoriteResponse(items = [favoriteWord]) {
  return {
    data: {
      items,
      page: 1,
      pageSize: 20,
      totalCount: items.length,
      totalPages: 1,
    },
    status: 200,
    statusText: "OK",
    headers: new AxiosHeaders(),
  };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });
  return { promise, reject, resolve };
}

function ModalHarness() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        打开收藏本
      </button>
      {open ? <WordFavoritesPanel modal onClose={() => setOpen(false)} /> : null}
    </>
  );
}

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

  it("handles Escape at the modal boundary and traps Tab within the dialog", async () => {
    const onClose = vi.fn();
    httpClient.defaults.adapter = (async (config) => ({
      ...favoriteResponse(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel modal onClose={onClose} />
      </Provider>,
    );

    const dialog = await screen.findByRole("dialog", { name: "收藏本" });
    const close = screen.getByRole("button", { name: "关闭收藏本" });
    expect(close).toHaveFocus();
    await user.keyboard("{Shift>}{Tab}{/Shift}");
    expect(dialog.contains(document.activeElement)).toBe(true);
    await user.keyboard("{Tab}");
    expect(close).toHaveFocus();

    const visibleControls = screen
      .getAllByRole("button")
      .filter((button) => button.getAttribute("aria-label") !== "关闭收藏本弹窗");
    for (let index = 0; index < visibleControls.length; index += 1) {
      await user.keyboard("{Tab}");
      expect(document.activeElement).not.toHaveAttribute(
        "aria-label",
        "关闭收藏本弹窗",
      );
    }

    close.focus();
    await user.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("restores focus to the modal opener after list-mode Escape", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      ...favoriteResponse(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <ModalHarness />
      </Provider>,
    );

    const opener = screen.getByRole("button", { name: "打开收藏本" });
    await user.click(opener);
    await screen.findByRole("dialog", { name: "收藏本" });
    await user.keyboard("{Escape}");

    await waitFor(() => expect(opener).toHaveFocus());
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

  it("opens complete details and restores focus to the inline row opener", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      ...favoriteResponse(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    const opener = await screen.findByRole("button", { name: "查看 study" });
    await user.click(opener);

    expect(screen.getByRole("region", { name: "study" })).toBeInTheDocument();
    expect(screen.getByText("v.")).toBeInTheDocument();
    expect(screen.getByText("学习")).toBeInTheDocument();
    expect(screen.getByText("用于描述获取知识。")).toBeInTheDocument();
    expect(screen.getByText("I study English.")).toBeInTheDocument();
    expect(screen.getByText("我学习英语。")).toBeInTheDocument();
    expect(screen.getByText("n.")).toBeInTheDocument();
    expect(screen.getByText("书房")).toBeInTheDocument();
    expect(screen.getByText("The study is quiet.")).toBeInTheDocument();
    expect(screen.getByText("书房很安静。")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /朗读 study/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /朗读例句 I study English\./ }),
    ).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "返回收藏本" }));
    expect(await screen.findByRole("button", { name: "查看 study" })).toHaveFocus();
  });

  it("keeps one modal dialog and closes details before the modal on Escape", async () => {
    httpClient.defaults.adapter = (async (config) => ({
      ...favoriteResponse(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel modal onClose={vi.fn()} />
      </Provider>,
    );

    const opener = await screen.findByRole("button", { name: "查看 study" });
    await user.click(opener);

    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "返回收藏本" })).toHaveFocus();
    await user.keyboard("{Escape}");

    expect(screen.getAllByRole("dialog")).toHaveLength(1);
    expect(await screen.findByRole("button", { name: "查看 study" })).toHaveFocus();
  });

  it("keeps modal close controls available while details are open", async () => {
    const onClose = vi.fn();
    httpClient.defaults.adapter = (async (config) => ({
      ...favoriteResponse(),
      config,
    })) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel modal onClose={onClose} />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    await user.click(screen.getByRole("button", { name: "关闭收藏本" }));
    await user.click(screen.getByRole("button", { name: "关闭收藏本弹窗" }));

    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("closes details and refetches after removing the selected word", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    let isFavorite = true;
    httpClient.defaults.adapter = (async (config) => {
      requests.push(config);
      if (config.method === "delete") {
        isFavorite = false;
        return {
          data: null,
          status: 204,
          statusText: "No Content",
          headers: new AxiosHeaders(),
          config,
        };
      }
      return {
        ...favoriteResponse(isFavorite ? [favoriteWord] : []),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));

    await waitFor(() => {
      expect(screen.queryByRole("region", { name: "study" })).not.toBeInTheDocument();
    });
    expect(await screen.findByText("暂无收藏单词")).toBeInTheDocument();
    expect(requests.some((request) => request.method === "delete")).toBe(true);
  });

  it("moves focus to the modal close control after removing the selected word", async () => {
    let isFavorite = true;
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "delete") {
        isFavorite = false;
        return {
          data: null,
          status: 204,
          statusText: "No Content",
          headers: new AxiosHeaders(),
          config,
        };
      }
      return { ...favoriteResponse(isFavorite ? [favoriteWord] : []), config };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel modal onClose={vi.fn()} />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));

    await waitFor(() => expect(screen.getByText("暂无收藏单词")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "关闭收藏本" })).toHaveFocus();
  });

  it("keeps details open and shows an error when detail removal fails", async () => {
    httpClient.defaults.adapter = (async (config) => {
      if (config.method === "delete") throw new Error("failed");
      return {
        ...favoriteResponse(),
        config,
      };
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "收藏状态更新失败，请重试。",
    );
    expect(screen.getByRole("region", { name: "study" })).toBeInTheDocument();
  });

  it("does not let a completed detail removal close a newer selection", async () => {
    const removal = deferred<{
      data: null;
      status: number;
      statusText: string;
      headers: AxiosHeaders;
      config: InternalAxiosRequestConfig;
    }>();
    const secondWord = { ...favoriteWord, wordId: "word-2", headword: "learn" };
    let favoriteReadCount = 0;
    httpClient.defaults.adapter = ((config) => {
      if (config.method === "delete") return removal.promise;
      favoriteReadCount += 1;
      return Promise.resolve({
        ...favoriteResponse([favoriteWord, secondWord]),
        config,
      });
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));
    await user.click(screen.getByRole("button", { name: "返回收藏本" }));
    await user.click(screen.getByRole("button", { name: "查看 learn" }));

    await act(async () => {
      removal.resolve({
        data: null,
        status: 204,
        statusText: "No Content",
        headers: new AxiosHeaders(),
        config: {} as InternalAxiosRequestConfig,
      });
      await removal.promise;
      await Promise.resolve();
    });

    await waitFor(() => expect(favoriteReadCount).toBeGreaterThan(1));
    expect(await screen.findByRole("region", { name: "learn" })).toBeInTheDocument();
  });

  it("does not show a row removal failure in a newer detail view", async () => {
    const removal = deferred<never>();
    const secondWord = { ...favoriteWord, wordId: "word-2", headword: "learn" };
    httpClient.defaults.adapter = ((config) => {
      if (config.method === "delete") return removal.promise;
      return Promise.resolve({
        ...favoriteResponse([favoriteWord, secondWord]),
        config,
      });
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(
      await screen.findByRole("button", { name: "取消收藏 study" }),
    );
    await user.click(screen.getByRole("button", { name: "查看 learn" }));
    await act(async () => {
      removal.reject(new Error("failed"));
      await removal.promise.catch(() => undefined);
      await Promise.resolve();
    });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "learn" })).toBeInTheDocument();
  });

  it("preserves the current detail removal error when an older row removal fails", async () => {
    const firstRemoval = deferred<never>();
    const secondRemoval = deferred<never>();
    let deleteCount = 0;
    const secondWord = { ...favoriteWord, wordId: "word-2", headword: "learn" };
    httpClient.defaults.adapter = ((config) => {
      if (config.method === "delete") {
        deleteCount += 1;
        return deleteCount === 1 ? firstRemoval.promise : secondRemoval.promise;
      }
      return Promise.resolve({
        ...favoriteResponse([favoriteWord, secondWord]),
        config,
      });
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(
      await screen.findByRole("button", { name: "取消收藏 study" }),
    );
    await user.click(screen.getByRole("button", { name: "查看 learn" }));
    await user.click(screen.getByRole("button", { name: "取消收藏" }));
    secondRemoval.reject(new Error("newer detail failure"));
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "收藏状态更新失败，请重试。",
    );

    await act(async () => {
      firstRemoval.reject(new Error("older row failure"));
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(screen.queryByRole("alert")).toHaveTextContent(
      "收藏状态更新失败，请重试。",
    );
    expect(screen.getByRole("region", { name: "learn" })).toBeInTheDocument();
  });

  it("disables a pending detail removal and sends one delete request", async () => {
    const removal = deferred<never>();
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = ((config) => {
      requests.push(config);
      if (config.method === "delete") return removal.promise;
      return Promise.resolve({ ...favoriteResponse(), config });
    }) as AxiosAdapter;
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <WordFavoritesPanel />
      </Provider>,
    );

    await user.click(await screen.findByRole("button", { name: "查看 study" }));
    const remove = screen.getByRole("button", { name: "取消收藏" });
    await user.click(remove);
    expect(remove).toBeDisabled();
    await user.click(remove);

    expect(requests.filter((request) => request.method === "delete")).toHaveLength(1);
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
