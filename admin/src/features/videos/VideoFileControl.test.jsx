import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Label } from "@/components/ui/label.jsx";
import { VideoFileControl } from "@/features/videos/VideoFileControl.jsx";

describe("VideoFileControl", () => {
  it("selects and presents a source file through an accessible styled button", async () => {
    const user = userEvent.setup();
    const onFileChange = vi.fn();
    const file = new File(["video"], "lesson.mp4", { type: "video/mp4" });
    const { rerender } = render(
      <>
        <Label htmlFor="source-video">视频源文件</Label>
        <VideoFileControl
          id="source-video"
          file={null}
          accept="video/mp4"
          disabled={false}
          error={null}
          status={null}
          onFileChange={onFileChange}
        />
      </>,
    );

    expect(screen.getByRole("button", { name: "选择视频" })).toBeVisible();
    await user.upload(screen.getByLabelText("视频源文件"), file);
    expect(onFileChange).toHaveBeenCalledWith(file);

    rerender(
      <>
        <Label htmlFor="source-video">视频源文件</Label>
        <VideoFileControl
          id="source-video"
          file={file}
          accept="video/mp4"
          disabled={false}
          error={null}
          status={null}
          onFileChange={onFileChange}
        />
      </>,
    );
    expect(screen.getByRole("button", { name: "重新选择视频" })).toBeVisible();
    expect(screen.getByText(/lesson\.mp4/)).toBeVisible();
  });
});
