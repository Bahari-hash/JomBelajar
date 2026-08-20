import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import VideoCard from "@/features/videos/VideoCard";
import type { VideoCatalogItem } from "@/features/videos/videoTypes";

const video: VideoCatalogItem = {
  id: "11111111-2222-3333-4444-555555555555",
  title: "Listening in context",
  description: "A practical listening lesson.",
  durationSeconds: 95,
  author: {
    id: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    nickname: "Video Publisher",
    avatarUrl: "https://media.example.test/avatar.jpg",
  },
  coverUrl: "https://media.example.test/video-cover.jpg",
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
    expect(screen.getByText("1:35")).toBeVisible();
    expect(screen.getByAltText("《Listening in context》封面")).toHaveAttribute(
      "src",
      video.coverUrl,
    );
  });

  it("falls back to the play placeholder when the cover URL is unsafe", () => {
    renderCard({ ...video, coverUrl: "javascript:alert(1)" });

    expect(
      screen.queryByAltText("《Listening in context》封面"),
    ).not.toBeInTheDocument();
    expect(screen.getByLabelText(`观看《${video.title}》`)).toBeInTheDocument();
  });

  it("renders the generated poster returned as the backend cover fallback", () => {
    const generatedPosterUrl =
      "https://media.example.test/videos/id/outputs/version/poster.jpg";

    renderCard({ ...video, coverUrl: generatedPosterUrl });

    expect(screen.getByAltText("《Listening in context》封面")).toHaveAttribute(
      "src",
      generatedPosterUrl,
    );
  });

  it("uses the established publisher fallback when nickname and avatar are absent", () => {
    renderCard({
      ...video,
      author: { ...video.author, nickname: null, avatarUrl: null },
    });

    expect(screen.getByText("JomBelajar 编辑")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "默认头像" })).toBeInTheDocument();
  });
});
