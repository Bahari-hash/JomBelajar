import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AudioBatchUploadControl } from "@/features/audio/AudioBatchUploadControl.jsx";

const runner = vi.hoisted(() => ({
  uploadAudio: vi.fn(),
  refetchCapability: vi.fn(),
  capability: {
    module: "Audio",
    maxSizeBytes: 20 * 1024 * 1024,
    allowedTypes: [{ extension: ".mp3", contentTypes: ["audio/mpeg"] }],
    multipartThresholdBytes: 8 * 1024 * 1024,
    partSizeBytes: 5 * 1024 * 1024,
    maxPartCount: 10000,
    partPresignBatchLimit: 20,
  },
}));

vi.mock("@/features/audio/useAudioUploadRunner.js", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    useAudioUploadRunner: () => ({
      capability: runner.capability,
      capabilityError: null,
      capabilityLoading: false,
      refetchCapability: runner.refetchCapability,
      uploadAudio: runner.uploadAudio,
    }),
  };
});

function audio(id, name, status = "Ready") {
  return { id, name, status };
}

function file(name, contents = "audio", lastModified = 1000) {
  return new File([contents], name, {
    type: "audio/mpeg",
    lastModified,
  });
}

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

function selectFiles(container, files) {
  fireEvent.change(container.querySelector('input[type="file"]'), {
    target: { files },
  });
}

beforeEach(() => {
  runner.uploadAudio.mockReset();
  runner.refetchCapability.mockReset();
});

describe("AudioBatchUploadControl", () => {
  it("uses a multiple input with accept derived from capability", () => {
    const { container } = render(<AudioBatchUploadControl />);

    const input = container.querySelector('input[type="file"]');
    expect(input).toHaveAttribute("multiple");
    expect(input).toHaveAttribute("accept", ".mp3,audio/mpeg");
  });

  it("starts at most two files until one active upload settles", async () => {
    const pending = [deferred(), deferred(), deferred()];
    runner.uploadAudio.mockImplementation(({ file: selected, onStarted }) => {
      const index = runner.uploadAudio.mock.calls.length - 1;
      onStarted?.(`audio-${index + 1}`);
      return pending[index].promise.then(() =>
        audio(`audio-${index + 1}`, selected.name),
      );
    });
    const { container } = render(<AudioBatchUploadControl />);

    selectFiles(container, [
      file("one.mp3"),
      file("two.mp3"),
      file("three.mp3"),
    ]);

    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(2));
    expect(
      runner.uploadAudio.mock.calls.map(([value]) => value.file.name),
    ).toEqual(["one.mp3", "two.mp3"]);

    await act(async () => pending[0].resolve());

    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(3));
    expect(runner.uploadAudio.mock.calls[2][0].file.name).toBe("three.mp3");
  });

  it("marks invalid files individually without blocking valid files", async () => {
    runner.uploadAudio.mockImplementation(({ file: selected, onStarted }) => {
      onStarted?.("valid-id");
      return Promise.resolve(audio("valid-id", selected.name));
    });
    const { container } = render(<AudioBatchUploadControl />);
    const invalid = new File([], "empty.mp3", { type: "audio/mpeg" });

    selectFiles(container, [invalid, file("valid.mp3")]);

    expect(await screen.findByText("不能上传空文件。")).toBeVisible();
    expect(await screen.findByText("valid.mp3")).toBeVisible();
    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(1));
    expect(runner.uploadAudio.mock.calls[0][0].file.name).toBe("valid.mp3");
  });

  it("continues other uploads and displays the original to final name", async () => {
    runner.uploadAudio.mockImplementation(({ file: selected, onStarted }) => {
      onStarted?.(`${selected.name}-id`);
      return selected.name === "broken.mp3"
        ? Promise.reject(new Error("storage unavailable"))
        : Promise.resolve(audio("renamed-id", "hello (2).mp3"));
    });
    const { container } = render(<AudioBatchUploadControl />);

    selectFiles(container, [file("broken.mp3"), file("hello.mp3")]);

    expect(await screen.findByText("storage unavailable")).toBeVisible();
    expect(await screen.findByText("hello.mp3 -> hello (2).mp3")).toBeVisible();
    expect(runner.uploadAudio).toHaveBeenCalledTimes(2);
  });

  it("retries with an existing resource ID after initialization", async () => {
    runner.uploadAudio
      .mockImplementationOnce(({ onStarted }) => {
        onStarted?.("existing-id");
        return Promise.reject(new Error("upload failed"));
      })
      .mockResolvedValueOnce(audio("existing-id", "retry.mp3"));
    const user = userEvent.setup();
    const { container } = render(<AudioBatchUploadControl />);

    selectFiles(container, [file("retry.mp3")]);
    await user.click(
      await screen.findByRole("button", { name: "重试 retry.mp3" }),
    );

    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(2));
    expect(runner.uploadAudio.mock.calls[1][0].resource).toEqual({
      id: "existing-id",
    });
  });

  it("creates a new resource when retrying a pre-initialization failure", async () => {
    runner.uploadAudio
      .mockRejectedValueOnce(new Error("initialization failed"))
      .mockResolvedValueOnce(audio("new-id", "retry.mp3"));
    const user = userEvent.setup();
    const { container } = render(<AudioBatchUploadControl />);

    selectFiles(container, [file("retry.mp3")]);
    await user.click(
      await screen.findByRole("button", { name: "重试 retry.mp3" }),
    );

    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(2));
    expect(runner.uploadAudio.mock.calls[1][0].resource).toBeNull();
  });

  it("cancels only the selected entry with independent controllers", async () => {
    const pending = new Map();
    runner.uploadAudio.mockImplementation(({ file: selected, signal }) => {
      const request = deferred();
      signal.addEventListener(
        "abort",
        () => request.reject(new DOMException("Aborted", "AbortError")),
        { once: true },
      );
      pending.set(selected.name, request);
      return request.promise;
    });
    const user = userEvent.setup();
    const { container } = render(<AudioBatchUploadControl />);

    selectFiles(container, [file("one.mp3"), file("two.mp3")]);
    await waitFor(() => expect(runner.uploadAudio).toHaveBeenCalledTimes(2));
    const firstSignal = runner.uploadAudio.mock.calls[0][0].signal;
    const secondSignal = runner.uploadAudio.mock.calls[1][0].signal;
    expect(firstSignal).not.toBe(secondSignal);

    await user.click(screen.getByRole("button", { name: "取消 one.mp3" }));

    expect(firstSignal.aborted).toBe(true);
    expect(secondSignal.aborted).toBe(false);
    await act(async () =>
      pending.get("two.mp3").resolve(audio("two-id", "two.mp3")),
    );
    expect(await screen.findByText(/上传已取消。$/)).toBeVisible();
    expect(await screen.findByText(/上传完成$/)).toBeVisible();
  });

  it("keeps distinct IDs and terminal results across parent rerenders", async () => {
    runner.uploadAudio.mockImplementation(({ file: selected }) =>
      Promise.resolve(audio(`${selected.name}-id`, selected.name)),
    );
    const duplicateA = file("same.mp3", "audio", 1234);
    const duplicateB = file("same.mp3", "audio", 1234);
    const { container, rerender } = render(<AudioBatchUploadControl />);

    selectFiles(container, [duplicateA, duplicateB]);

    await waitFor(() =>
      expect(screen.getAllByText(/上传完成$/)).toHaveLength(2),
    );
    const ids = Array.from(container.querySelectorAll("[data-entry-id]")).map(
      (element) => element.getAttribute("data-entry-id"),
    );
    expect(new Set(ids).size).toBe(2);
    expect(ids.every((id) => id.includes("same.mp3:5:1234:"))).toBe(true);

    rerender(<AudioBatchUploadControl onTerminal={() => {}} />);
    expect(screen.getAllByText(/上传完成$/)).toHaveLength(2);
  });
});
