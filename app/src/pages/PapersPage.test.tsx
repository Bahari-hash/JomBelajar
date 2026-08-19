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
  paperCategories = ["grammar", "a2"],
  emptyPapers = false,
) {
  httpClient.defaults.adapter = (async (config) => {
    requests.push(config);
    const isCategoryRequest = config.url === "/paper-categories";
    const isCategorySearch = config.params?.keyword === "listen";
    const data = isCategoryRequest
      ? {
          items: isCategorySearch
            ? [
                {
                  id: "77777777-7777-4777-8777-777777777777",
                  name: "listening",
                  slug: "listening",
                },
              ]
            : [
                {
                  id: "77777777-7777-4777-8777-777777777771",
                  name: "cet-4",
                  slug: "cet-4",
                },
                {
                  id: "77777777-7777-4777-8777-777777777772",
                  name: "grammar",
                  slug: "grammar",
                },
              ],
          page: Number(config.params?.page ?? 1),
          pageSize: Number(config.params?.pageSize ?? 6),
          totalCount: isCategorySearch ? 1 : 2,
          totalPages: 1,
        }
      : {
          items: emptyPapers
            ? []
            : [
                {
                  id: PAPER_ID,
                  title: "A2 Grammar Check",
                  description: "Check practical grammar knowledge.",
                  categories: paperCategories.map((name, index) => ({
                    id: `77777777-7777-4777-8777-77777777777${index}`,
                    name,
                    slug: name,
                  })),
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
    const tags = screen.getByLabelText("试卷分类");
    const grammarTag = within(tags).getByRole("link", { name: "grammar" });
    expect(grammarTag).toHaveAttribute(
      "href",
      expect.stringContaining("categoryId="),
    );
    expect(within(tags).getByRole("link", { name: "a2" })).toHaveAttribute(
      "href",
      expect.stringContaining("categoryId="),
    );
    const link = screen.getByRole("link", { name: "查看《A2 Grammar Check》" });
    expect(link).toHaveAttribute("href", `/papers/${PAPER_ID}`);
  });
  it("filters by a shortcut category and preserves keyword state", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage("/papers?keyword=grammar");

    const shortcut = await screen.findByRole("button", { name: "cet-4" });
    await user.click(shortcut);

    await waitFor(() => {
      const paperRequests = requests.filter(
        (request) => request.url === "/papers",
      );
      expect(paperRequests.at(-1)?.params).toMatchObject({
        keyword: "grammar",
        categoryId: expect.any(String),
        page: 1,
      });
      expect(shortcut).toHaveAttribute("aria-pressed", "true");
      expect(screen.getByTestId("location").textContent).toContain(
        "/papers?keyword=grammar&categoryId=",
      );
    });
  });

  it("selects a category from the directory", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests);
    renderPage();

    await user.click(await screen.findByRole("button", { name: "grammar" }));

    await waitFor(() => {
      expect(screen.getByTestId("location").textContent).toContain(
        "/papers?categoryId=",
      );
      expect(
        requests.some((request) => request.url === "/paper-categories"),
      ).toBe(true);
    });
  });

  it("clears keyword and tag together from an empty filtered result", async () => {
    const user = userEvent.setup();
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests, ["grammar"], true);
    renderPage("/papers?keyword=grammar");

    await user.click(await screen.findByRole("button", { name: "清除筛选" }));

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/papers");
      const paperRequests = requests.filter(
        (request) => request.url === "/papers",
      );
      expect(paperRequests.at(-1)?.params.keyword).toBeUndefined();
      expect(paperRequests.at(-1)?.params.categoryId).toBeUndefined();
    });
  });

  it("does not render a tag container when a paper has no tags", async () => {
    const requests: InternalAxiosRequestConfig[] = [];
    installAdapter(requests, []);
    renderPage();

    await screen.findByRole("heading", { name: "A2 Grammar Check" });
    expect(screen.queryByLabelText("试卷分类")).not.toBeInTheDocument();
  });
});
