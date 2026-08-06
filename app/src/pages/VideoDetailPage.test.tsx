import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { Provider } from "react-redux";
import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import VideoDetailPage from "@/pages/VideoDetailPage";
import { httpClient } from "@/services/httpClient";
import { createAppStore } from "@/store/store";

vi.mock("@/features/videos/VideoPlayer", () => ({
  default: ({ videoId }: { videoId: string }) => (
    <div data-testid="video-player">播放器 {videoId}</div>
  ),
}));

const VIDEO_ID = "11111111-2222-3333-4444-555555555555";
const CATEGORY_ID = "99999999-8888-7777-6666-555555555555";
const originalAdapter = httpClient.defaults.adapter;

afterEach(() => {
  httpClient.defaults.adapter = originalAdapter;
});

function renderDetail(path: string) {
  render(
    <Provider store={createAppStore()}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/videos/:videoId" element={<VideoDetailPage />} />
        </Routes>
      </MemoryRouter>
    </Provider>,
  );
}

describe("VideoDetailPage", () => {
  it("does not request invalid video identifiers", () => {
    const requests: InternalAxiosRequestConfig[] = [];
    httpClient.defaults.adapter = async (config) => {
      requests.push(config);
      throw new Error("should not request");
    };

    renderDetail("/videos/not-a-guid");

    expect(
      screen.getByRole("heading", { name: "视频不存在或已下架" }),
    ).toBeInTheDocument();
    expect(requests).toHaveLength(0);
  });

  it("places real publisher metadata and the player inside the detail card", async () => {
    const adapter: AxiosAdapter = async (config) => ({
      data: {
        id: VIDEO_ID,
        title: "Listening in context",
        description: "A practical listening lesson.",
        originalLanguage: "en",
        durationSeconds: 95,
        displayWidth: 1920,
        displayHeight: 1080,
        author: {
          id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          nickname: "Video Publisher",
          avatarUrl: "https://media.example.test/avatar.jpg",
        },
        publishedAt: "2026-08-06T08:00:00Z",
        categories: [{ id: CATEGORY_ID, name: "Listening", slug: "listening" }],
      },
      status: 200,
      statusText: "OK",
      headers: new AxiosHeaders(),
      config,
    });
    httpClient.defaults.adapter = adapter;

    renderDetail(`/videos/${VIDEO_ID}`);

    const title = await screen.findByRole("heading", {
      name: "Listening in context",
    });
    const card = title.closest("article");
    expect(card).not.toBeNull();
    expect(within(card!).getByText("Video Publisher")).toBeInTheDocument();
    expect(
      within(card!).getByAltText("Video Publisher的头像"),
    ).toBeInTheDocument();
    expect(within(card!).getByTestId("video-player")).toBeInTheDocument();
    expect(card).not.toContainElement(
      screen.getByRole("link", { name: "返回视频列表" }),
    );
    expect(screen.getByRole("link", { name: "Listening" })).toHaveAttribute(
      "href",
      `/videos?categoryId=${CATEGORY_ID}`,
    );
  });
});
