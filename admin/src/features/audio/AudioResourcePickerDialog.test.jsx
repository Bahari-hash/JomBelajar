import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { AudioResourcePickerDialog } from "@/features/audio/AudioResourcePickerDialog.jsx";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const AUDIO_IDS = {
  Uploading: "11111111-1111-4111-8111-111111111111",
  Queued: "22222222-2222-4222-8222-222222222222",
  Processing: "33333333-3333-4333-8333-333333333333",
  Ready: "44444444-4444-4444-8444-444444444444",
  Failed: "55555555-5555-4555-8555-555555555555",
};

function audio(status, overrides = {}) {
  return {
    id: AUDIO_IDS[status],
    name: `${status.toLowerCase()}.mp3`,
    status,
    durationSeconds: status === "Ready" ? 42.5 : null,
    lastFailureCode: status === "Failed" ? "AudioProcessingFailed" : null,
    updatedAt: "2026-08-16T08:00:00Z",
    ...overrides,
  };
}

function audioPage(overrides = {}) {
  return {
    items: Object.keys(AUDIO_IDS).map((status) => audio(status)),
    page: 1,
    pageSize: 20,
    totalCount: 5,
    totalPages: 1,
    ...overrides,
  };
}

function renderPicker(props = {}) {
  const onSelect = props.onSelect ?? vi.fn();
  const onOpenChange = props.onOpenChange ?? vi.fn();
  render(
    <Provider store={createAppStore()}>
      <AudioResourcePickerDialog
        open
        value={null}
        onSelect={onSelect}
        onOpenChange={onOpenChange}
        {...props}
      />
    </Provider>,
  );
  return { onSelect, onOpenChange };
}

describe("AudioResourcePickerDialog", () => {
  it("uses resource-neutral copy for shared consumers", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(audioPage())));
    renderPicker();

    expect(
      await screen.findByText("可以关联处于任意处理状态的音频资源。"),
    ).toBeVisible();
    expect(screen.queryByText(/文章可以关联/)).toBeNull();
  });

  it("uses the audio directory query for search, status filtering, and pagination", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      const params = new URLSearchParams(config.url.split("?")[1]);
      const page = Number(params.get("page"));
      return Promise.resolve(
        axiosResponse(
          audioPage({
            items: [audio("Processing", { name: "lesson.mp3" })],
            page,
            totalCount: 21,
            totalPages: 2,
          }),
        ),
      );
    });
    const user = userEvent.setup();
    renderPicker();

    await screen.findByRole("radio", { name: /lesson\.mp3/ });
    await user.type(
      screen.getByRole("searchbox", { name: "搜索音频名称" }),
      " lesson ",
    );
    await user.click(screen.getByRole("button", { name: "搜索" }));
    await user.click(screen.getByRole("combobox", { name: "筛选音频状态" }));
    await user.click(screen.getByRole("option", { name: "处理中" }));
    await user.click(screen.getByRole("button", { name: "下一页" }));

    await waitFor(() =>
      expect(
        requestMock.mock.calls.some(
          ([config]) =>
            config.url ===
            "/admin/audio?page=2&pageSize=20&keyword=lesson&status=Processing",
        ),
      ).toBe(true),
    );
  });

  it("allows every lifecycle state to be selected", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(audioPage())));
    const user = userEvent.setup();
    renderPicker();

    for (const status of Object.keys(AUDIO_IDS)) {
      const radio = await screen.findByRole("radio", {
        name: new RegExp(`${status.toLowerCase()}\\.mp3`),
      });
      expect(radio).toBeEnabled();
      await user.click(radio);
      expect(radio).toBeChecked();
    }
  });

  it("marks the current resource and confirms the selected audio summary", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(audioPage())));
    const user = userEvent.setup();
    const current = audio("Ready");
    const { onSelect, onOpenChange } = renderPicker({ value: current });

    expect(
      await screen.findByRole("radio", { name: /ready\.mp3/ }),
    ).toBeChecked();
    await user.click(screen.getByRole("radio", { name: /processing\.mp3/ }));
    await user.click(screen.getByRole("button", { name: "确认选择" }));

    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        id: AUDIO_IDS.Processing,
        name: "processing.mp3",
        status: "Processing",
      }),
    );
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it("closes without changing the current selection", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() => Promise.resolve(axiosResponse(audioPage())));
    const user = userEvent.setup();
    const { onSelect, onOpenChange } = renderPicker();

    await user.click(await screen.findByRole("radio", { name: /queued\.mp3/ }));
    await user.click(screen.getByRole("button", { name: "取消" }));

    expect(onSelect).not.toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
