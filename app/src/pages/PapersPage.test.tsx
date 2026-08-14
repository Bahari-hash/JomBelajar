import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import PapersPage from "@/pages/PapersPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

const PAPER_ID = "11111111-2222-3333-4444-555555555555";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function installAdapter(
  requests: InternalAxiosRequestConfig[],
  paperTags = ["grammar", "a2"],
  emptyPapers = false,
) {
  httpClient.defaults.adapter = (async (config) => {
    requests.push(config);
    const isTagRequest = config.url === "/paper-tags";
    const isTagSearch = config.params?.keyword === "listen";
    const data = isTagRequest
      ? {
          items: isTagSearch
            ? [{ name: "listening", paperCount: 4 }]
            : [
                { name: "cet-4", paperCount: 12 },
                { name: "grammar", paperCount: 8 },
              ],
          page: Number(config.params?.page ?? 1),
          pageSize: Number(config.params?.pageSize ?? 6),
          totalCount: isTagSearch ? 1 : 7,
          totalPages: isTagSearch ? 1 : 2,
        }
      : {
          items: emptyPapers
            ? []
            : [
                {
                  id: PAPER_ID,
                  title: "A2 Grammar Check",
                  description: "Check practical grammar knowledge.",
                  tags: paperTags,
                  questionCount: 12,
                  totalScore: 24,
                  passingScore: 15,
                  publishedAt: "2026-08-10T00:00:00Z",
                },
              ],
          page: 1,
          pageSize: 12,
          totalCount: emptyPapers ? 0 : 60,
          totalPages: emptyPapers ? 0 : 5,
        };
    return {
      data,
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    };
  }) as AxiosAdapter;
}

function LocationProbe() {
  const location = useLocation();
  return (
    <output data-testid="location">{`${location.pathname}${location.search}`}</output>
  );
}

function renderPage(path = "/papers") {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[path]}>
        <LocationProbe />
        <PapersPage />
      </MemoryRouter>
    </Provider>,
  );
}

describe("PapersPage", () => {
  it("searches only on submit and resets the page", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?page=4");

    expect(
      await screen.findByRole("heading", { name: "A2 Grammar Check" }),
    ).toBeInTheDocument();
    const beforeTyping = requests.length;
    await user.type(
      screen.getByRole("searchbox", { name: "搜索试卷标题" }),
      "grammar",
    );
    expect(requests).toHaveLength(beforeTyping);
    await user.click(screen.getByRole("button", { name: "搜索" }));

    await waitFor(() => {
      expect(requests.at(-1)?.params).toMatchObject({
        page: 1,
        pageSize: 12,
        keyword: "grammar",
      });
      expect(screen.getByTestId("location")).toHaveTextContent(
        "/papers?keyword=grammar",
      );
    });
  });

  it("normalizes invalid URL state before querying", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?page=-2&keyword=%20%20");
    await screen.findByRole("heading", { name: "A2 Grammar Check" });
    const paperRequest = requests.find((request) => request.url === "/papers");
    expect(paperRequest?.params).toMatchObject({ page: 1, pageSize: 12 });
    expect(paperRequest?.params.keyword).toBeUndefined();
  });

  it("shows only real catalog metadata and preserves the list return path", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?keyword=grammar");

    expect(await screen.findByText("12 题")).toBeInTheDocument();
    expect(screen.getByText("总分 24")).toBeInTheDocument();
    expect(screen.getByText("及格 15")).toBeInTheDocument();
    const tags = screen.getByLabelText("试卷标签");
    const grammarTag = within(tags).getByRole("link", { name: "grammar" });
    expect(grammarTag).toHaveAttribute("href", "/papers?tag=grammar");
    expect(within(tags).getByRole("link", { name: "a2" })).toHaveAttribute(
      "href",
      "/papers?tag=a2",
    );
    const link = screen.getByRole("link", { name: "查看《A2 Grammar Check》" });
    expect(link).toHaveAttribute("href", `/papers/${PAPER_ID}`);
  });
  it("filters by a shortcut tag and preserves keyword state", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?keyword=grammar");

    const shortcut = await screen.findByRole("button", { name: /cet-4 12/ });
    await user.click(shortcut);

    await waitFor(() => {
      const paperRequests = requests.filter(
        (request) => request.url === "/papers",
      );
      expect(paperRequests.at(-1)?.params).toMatchObject({
        keyword: "grammar",
        tag: "cet-4",
        page: 1,
      });
      expect(shortcut).toHaveAttribute("aria-pressed", "true");
      expect(screen.getByTestId("location")).toHaveTextContent(
        "/papers?keyword=grammar&tag=cet-4",
      );
    });
  });

  it("searches and selects a tag from the expanded directory", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage();

    await user.click(await screen.findByRole("button", { name: "更多标签" }));
    await user.type(
      screen.getByRole("searchbox", { name: "搜索试卷标签" }),
      "listen",
    );
    await user.click(screen.getByRole("button", { name: "搜索标签" }));
    await user.click(
      await screen.findByRole("button", { name: /listening 4/ }),
    );

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent(
        "/papers?tag=listening",
      );
      expect(
        requests.some(
          (request) =>
            request.url === "/paper-tags" &&
            request.params?.keyword === "listen",
        ),
      ).toBe(true);
    });
  });

  it("clears keyword and tag together from an empty filtered result", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests, ["grammar"], true);
    renderPage("/papers?keyword=grammar&tag=cet-4");

    await user.click(await screen.findByRole("button", { name: "清除筛选" }));

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/papers");
      const paperRequests = requests.filter(
        (request) => request.url === "/papers",
      );
      expect(paperRequests.at(-1)?.params.keyword).toBeUndefined();
      expect(paperRequests.at(-1)?.params.tag).toBeUndefined();
    });
  });

  it("does not render a tag container when a paper has no tags", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests, []);
    renderPage();

    await screen.findByRole("heading", { name: "A2 Grammar Check" });
    expect(screen.queryByLabelText("试卷标签")).not.toBeInTheDocument();
  });
});
