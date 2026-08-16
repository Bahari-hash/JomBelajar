import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

const EXISTING_AUDIO = {
  id: "11111111-1111-4111-8111-111111111111",
  name: "library.mp3",
  status: "Processing",
  durationSeconds: null,
  lastFailureCode: null,
};

const UPLOADED_AUDIO = {
  id: "22222222-2222-4222-8222-222222222222",
  name: "reading.mp3",
  status: "Queued",
  durationSeconds: null,
  lastFailureCode: null,
};

vi.mock("@/features/audio/AudioResourcePickerDialog.jsx", () => ({
  AudioResourcePickerDialog: ({ open, onSelect, onOpenChange }) =>
    open ? (
      <div role="dialog" aria-label="模拟音频资源选择器">
        <button type="button" onClick={() => onSelect(EXISTING_AUDIO)}>
          选择模拟资源
        </button>
        <button type="button" onClick={() => onOpenChange(false)}>
          关闭模拟选择器
        </button>
      </div>
    ) : null,
}));

vi.mock("@/features/audio/AudioUploadControl.jsx", () => ({
  AudioUploadControl: ({ waitForProcessing, onStarted, onCompleted }) => (
    <div data-testid="mock-audio-upload">
      <span>{waitForProcessing ? "等待处理" : "后台处理"}</span>
      <button
        type="button"
        onClick={() => onStarted(UPLOADED_AUDIO.id, UPLOADED_AUDIO.name)}
      >
        模拟上传初始化
      </button>
      <button type="button" onClick={() => onCompleted(UPLOADED_AUDIO)}>
        模拟上传确认
      </button>
    </div>
  ),
}));

import { ArticleReadingAudioControl } from "@/features/articles/ArticleReadingAudioControl.jsx";

describe("ArticleReadingAudioControl", () => {
  it("applies an existing library resource immediately", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ArticleReadingAudioControl value={null} onChange={onChange} />);

    await user.click(screen.getByRole("button", { name: "从资源库选择" }));
    await user.click(screen.getByRole("button", { name: "选择模拟资源" }));

    expect(onChange).toHaveBeenCalledWith(EXISTING_AUDIO);
  });

  it("stores Uploading immediately and refreshes the summary after confirmation", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ArticleReadingAudioControl value={null} onChange={onChange} />);

    await user.click(screen.getByRole("button", { name: "上传新音频" }));
    expect(screen.getByText("后台处理")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "模拟上传初始化" }));
    expect(onChange).toHaveBeenLastCalledWith({
      id: UPLOADED_AUDIO.id,
      name: "reading.mp3",
      status: "Uploading",
      durationSeconds: null,
      lastFailureCode: null,
    });

    await user.click(screen.getByRole("button", { name: "模拟上传确认" }));
    expect(onChange).toHaveBeenLastCalledWith(UPLOADED_AUDIO);
  });

  it("unlinks without performing any resource management action", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <ArticleReadingAudioControl value={EXISTING_AUDIO} onChange={onChange} />,
    );

    await user.click(screen.getByRole("button", { name: "解除关联" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("keeps a failed resource associated and points administrators to the library", () => {
    render(
      <ArticleReadingAudioControl
        value={{
          ...EXISTING_AUDIO,
          name: "failed.mp3",
          status: "Failed",
          lastFailureCode: "AudioProcessingFailed",
        }}
        onChange={vi.fn()}
      />,
    );

    expect(screen.getByText("failed.mp3")).toBeVisible();
    expect(screen.getByText("处理失败")).toBeVisible();
    expect(
      screen.getByText(/请前往音频资源库重新上传或重新处理/),
    ).toBeVisible();
  });
});
