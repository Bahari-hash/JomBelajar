import axios, {
  AxiosHeaders,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from "axios";
import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import AudioPlaybackButton from "@/features/audio/AudioPlaybackButton";
import { requestAudioPlayback } from "@/features/audio/audioPlayback";

vi.mock("@/features/audio/audioPlayback", () => ({
  requestAudioPlayback: vi.fn(),
}));

const AUDIO_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const NEXT_AUDIO_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const requestMock = vi.mocked(requestAudioPlayback);

interface FakeAudio {
  src: string;
  onended: (() => void) | null;
  onerror: (() => void) | null;
  play: ReturnType<typeof vi.fn>;
  pause: ReturnType<typeof vi.fn>;
}

function installAudio() {
  const instances: FakeAudio[] = [];
  const constructor = vi.fn(function createAudio(url: string) {
    const audio: FakeAudio = {
      src: url,
      onended: null,
      onerror: null,
      play: vi.fn().mockResolvedValue(undefined),
      pause: vi.fn(),
    };
    instances.push(audio);
    return audio;
  });
  vi.stubGlobal("Audio", constructor);
  return { constructor, instances };
}

function playback() {
  return {
    url: "https://media.example/reading.mp3",
    expiresAt: null,
    durationSeconds: 12.5,
  };
}

function apiError(errorCode: string, detail: string) {
  const config = {
    headers: new AxiosHeaders(),
  } as InternalAxiosRequestConfig;
  const response: AxiosResponse = {
    data: { errorCode, detail },
    status: 409,
    statusText: "Conflict",
    headers: new AxiosHeaders(),
    config,
  };
  return new axios.AxiosError(
    detail,
    axios.AxiosError.ERR_BAD_RESPONSE,
    config,
    undefined,
    response,
  );
}

beforeEach(() => {
  requestMock.mockReset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("AudioPlaybackButton", () => {
  it("renders icon mode without visible copy and keeps play and pause behavior", async () => {
    requestMock.mockResolvedValue(playback());
    const { instances } = installAudio();
    const user = userEvent.setup();
    render(
      <AudioPlaybackButton
        audioResourceId={AUDIO_ID}
        label="单词发音"
        variant="icon"
      />,
    );

    const playButton = screen.getByRole("button", { name: "播放单词发音" });
    expect(playButton).toHaveAttribute("title", "播放单词发音");
    expect(screen.queryByText("播放朗读")).not.toBeInTheDocument();
    await user.click(playButton);

    const pauseButton = await screen.findByRole("button", {
      name: "暂停单词发音",
    });
    expect(requestMock).toHaveBeenCalledWith(AUDIO_ID, expect.any(AbortSignal));
    expect(instances[0]?.play).toHaveBeenCalledOnce();
    expect(pauseButton).toHaveAttribute("title", "暂停单词发音");
    expect(screen.queryByText("暂停朗读")).not.toBeInTheDocument();

    await user.click(pauseButton);
    expect(instances[0]?.pause).toHaveBeenCalledOnce();
    expect(
      screen.getByRole("button", { name: "播放单词发音" }),
    ).toBeEnabled();
  });

  it("aborts a pending icon-mode request on unmount", async () => {
    requestMock.mockReturnValue(new Promise(() => undefined));
    installAudio();
    const user = userEvent.setup();
    const view = render(
      <AudioPlaybackButton
        audioResourceId={AUDIO_ID}
        label="单词发音"
        variant="icon"
      />,
    );

    await user.click(screen.getByRole("button", { name: "播放单词发音" }));
    const signal = requestMock.mock.calls[0]?.[1];
    expect(signal?.aborted).toBe(false);
    view.unmount();

    expect(signal?.aborted).toBe(true);
  });

  it("shows the stable unavailable message in icon mode", async () => {
    requestMock.mockRejectedValue(
      apiError("AudioNotReady", "internal output object path"),
    );
    installAudio();
    const user = userEvent.setup();
    render(
      <AudioPlaybackButton
        audioResourceId={AUDIO_ID}
        label="单词发音"
        variant="icon"
      />,
    );

    await user.click(screen.getByRole("button", { name: "播放单词发音" }));

    expect(
      await screen.findByText("音频暂时不可用，请稍后再试。"),
    ).toBeInTheDocument();
    expect(screen.queryByText(/internal output object path/)).toBeNull();
  });

  it("requests a playback grant and starts the browser audio instance", async () => {
    requestMock.mockResolvedValue(playback());
    const { constructor, instances } = installAudio();
    const user = userEvent.setup();
    render(<AudioPlaybackButton audioResourceId={AUDIO_ID} label="文章朗读" />);

    await user.click(screen.getByRole("button", { name: "播放文章朗读" }));

    await waitFor(() => expect(instances[0]?.play).toHaveBeenCalledOnce());
    expect(requestMock).toHaveBeenCalledWith(AUDIO_ID, expect.any(AbortSignal));
    expect(constructor).toHaveBeenCalledWith(playback().url);
    expect(screen.getByRole("button", { name: "暂停文章朗读" })).toBeEnabled();
  });

  it("pauses while playing and returns to idle after ended", async () => {
    requestMock.mockResolvedValue(playback());
    const { instances } = installAudio();
    const user = userEvent.setup();
    render(<AudioPlaybackButton audioResourceId={AUDIO_ID} label="文章朗读" />);

    await user.click(screen.getByRole("button", { name: "播放文章朗读" }));
    await screen.findByRole("button", { name: "暂停文章朗读" });
    await user.click(screen.getByRole("button", { name: "暂停文章朗读" }));

    expect(instances[0]?.pause).toHaveBeenCalledOnce();
    expect(screen.getByRole("button", { name: "播放文章朗读" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "播放文章朗读" }));
    await screen.findByRole("button", { name: "暂停文章朗读" });
    act(() => instances[0]?.onended?.());

    expect(screen.getByRole("button", { name: "播放文章朗读" })).toBeEnabled();
  });

  it("releases old audio on id changes and aborts pending work on unmount", async () => {
    requestMock.mockResolvedValueOnce(playback());
    const pending = new Promise<ReturnType<typeof playback>>(() => undefined);
    requestMock.mockReturnValueOnce(pending);
    const { instances } = installAudio();
    const user = userEvent.setup();
    const view = render(
      <AudioPlaybackButton audioResourceId={AUDIO_ID} label="文章朗读" />,
    );

    await user.click(screen.getByRole("button", { name: "播放文章朗读" }));
    await screen.findByRole("button", { name: "暂停文章朗读" });
    view.rerender(
      <AudioPlaybackButton audioResourceId={NEXT_AUDIO_ID} label="文章朗读" />,
    );

    expect(instances[0]?.pause).toHaveBeenCalledOnce();
    expect(instances[0]?.src).toBe("");

    await user.click(screen.getByRole("button", { name: "播放文章朗读" }));
    const signal = requestMock.mock.calls[1]?.[1];
    expect(signal?.aborted).toBe(false);
    view.unmount();

    expect(signal?.aborted).toBe(true);
  });

  it("shows the stable unavailable message for AudioNotReady", async () => {
    requestMock.mockRejectedValue(
      apiError("AudioNotReady", "internal output object path"),
    );
    installAudio();
    const user = userEvent.setup();
    render(<AudioPlaybackButton audioResourceId={AUDIO_ID} />);

    await user.click(screen.getByRole("button", { name: "播放朗读" }));

    expect(
      await screen.findByText("音频暂时不可用，请稍后再试。"),
    ).toBeInTheDocument();
    expect(screen.queryByText(/internal output object path/)).toBeNull();
  });

  it("uses a safe fallback for unknown request and playback failures", async () => {
    requestMock.mockRejectedValue(
      apiError("UnknownAudioFailure", "bucket=private-audio"),
    );
    installAudio();
    const user = userEvent.setup();
    render(<AudioPlaybackButton audioResourceId={AUDIO_ID} />);

    await user.click(screen.getByRole("button", { name: "播放朗读" }));

    expect(
      await screen.findByText("音频加载失败，请重试。"),
    ).toBeInTheDocument();
    expect(screen.queryByText(/bucket=private-audio/)).toBeNull();
  });
});
