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
  name: "word.mp3",
  status: "Queued",
  durationSeconds: null,
  lastFailureCode: null,
};

vi.mock("@/features/audio/AudioResourcePickerDialog.jsx", () => ({
  AudioResourcePickerDialog: ({ open, onSelect, onOpenChange }) =>
    open ? (
      <div role="dialog" aria-label="选择音频资源">
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
        模拟上传完成
      </button>
    </div>
  ),
}));

import { WordAudioControl } from "@/features/words/WordAudioControl.jsx";

describe("WordAudioControl", () => {
  it("selects an existing shared audio resource", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <WordAudioControl value={null} onChange={onChange} disabled={false} />,
    );

    await user.click(screen.getByRole("button", { name: "从资源库选择" }));
    expect(screen.getByRole("dialog", { name: "选择音频资源" })).toBeVisible();
    await user.click(screen.getByRole("button", { name: "选择模拟资源" }));

    expect(onChange).toHaveBeenCalledWith(EXISTING_AUDIO);
  });

  it("associates Uploading immediately and refreshes after upload completion", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <WordAudioControl value={null} onChange={onChange} disabled={false} />,
    );

    await user.click(screen.getByRole("button", { name: "上传新音频" }));
    expect(screen.getByText("后台处理")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "模拟上传初始化" }));
    expect(onChange).toHaveBeenLastCalledWith({
      id: UPLOADED_AUDIO.id,
      name: "word.mp3",
      status: "Uploading",
      durationSeconds: null,
      lastFailureCode: null,
    });

    await user.click(screen.getByRole("button", { name: "模拟上传完成" }));
    expect(onChange).toHaveBeenLastCalledWith(UPLOADED_AUDIO);
  });

  it("unlinks without deleting the shared resource", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <WordAudioControl
        value={EXISTING_AUDIO}
        onChange={onChange}
        disabled={false}
      />,
    );

    await user.click(screen.getByRole("button", { name: "解除关联" }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("keeps failed audio associated and directs the administrator to the library", () => {
    render(
      <WordAudioControl
        value={{
          ...EXISTING_AUDIO,
          name: "failed.mp3",
          status: "Failed",
          lastFailureCode: "AudioProcessingFailed",
        }}
        onChange={vi.fn()}
        disabled={false}
      />,
    );

    expect(screen.getByText("failed.mp3")).toBeVisible();
    expect(screen.getByText("处理失败")).toBeVisible();
    expect(
      screen.getByText(/请前往音频资源库重新上传或重新处理/),
    ).toBeVisible();
  });

  it("disables every association action", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <WordAudioControl value={EXISTING_AUDIO} onChange={onChange} disabled />,
    );

    expect(screen.getByRole("button", { name: "从资源库选择" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "上传新音频" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "解除关联" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "从资源库选择" }));
    expect(screen.queryByRole("dialog", { name: "选择音频资源" })).toBeNull();
    expect(onChange).not.toHaveBeenCalled();
  });

  it("closes expanded association controls when it becomes disabled", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { rerender } = render(
      <WordAudioControl value={null} onChange={onChange} disabled={false} />,
    );

    await user.click(screen.getByRole("button", { name: "从资源库选择" }));
    await user.click(screen.getByRole("button", { name: "上传新音频" }));
    expect(screen.getByRole("dialog", { name: "选择音频资源" })).toBeVisible();
    expect(screen.getByTestId("mock-audio-upload")).toBeVisible();

    rerender(<WordAudioControl value={null} onChange={onChange} disabled />);

    expect(screen.queryByRole("dialog", { name: "选择音频资源" })).toBeNull();
    expect(screen.queryByTestId("mock-audio-upload")).toBeNull();
    expect(onChange).not.toHaveBeenCalled();
  });
});
