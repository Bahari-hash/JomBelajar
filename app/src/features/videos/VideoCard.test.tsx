import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import VideoCard from "@/features/videos/VideoCard";
import type { VideoCatalogItem } from "@/features/videos/videoTypes";

const video: VideoCatalogItem = {
  id: "11111111-2222-3333-4444-555555555555",
  title: "Listening in context",
  description: "A practical listening lesson.",
  originalLanguage: "en",
  durationSeconds: 95,
  author: {
    id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    nickname: "Video Publisher",
    avatarUrl: "https://media.example.test/avatar.jpg",
  },
  publishedAt: "2026-08-06T08:00:00Z",
  categories: [
    {
      id: "99999999-8888-7777-6666-555555555555",
      name: "Listening",
      slug: "listening",
    },
  ],
};

function renderCard(item = video) {
  render(
    <MemoryRouter>
      <VideoCard video={item} listPath="/videos?page=2" />
    </MemoryRouter>,
  );
}

describe("VideoCard", () => {
  it("renders the real publisher in the same footer pattern as article cards", () => {
    renderCard();

    expect(screen.getByText("Video Publisher")).toBeInTheDocument();
    expect(screen.getByAltText("Video Publisher的头像")).toHaveAttribute(
      "src",
      video.author.avatarUrl,
    );
    expect(screen.getByRole("link", { name: video.title })).toHaveAttribute(
      "href",
      `/videos/${video.id}`,
    );
    expect(screen.getAllByText("1:35")).toHaveLength(2);
  });

  it("uses the established publisher fallback when nickname and avatar are absent", () => {
    renderCard({
      ...video,
      author: { ...video.author, nickname: null, avatarUrl: null },
    });

    expect(screen.getByText("TinyLang 编辑")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "默认头像" })).toBeInTheDocument();
  });
});
