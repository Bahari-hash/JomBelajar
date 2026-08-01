import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { WordAudioPicker } from "@/features/words/WordAudioPicker.jsx";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const AUDIO_ID = "77777777-7777-4777-8777-777777777777";

afterEach(() => vi.restoreAllMocks());

describe("WordAudioPicker", () => {
  it("loads safe metadata, requests playback on demand and selects the clip", async () => {
    tokenVault.install("access", "refresh");
    vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
    vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.includes("/playback")
            ? {
                url: "https://media.example.test/bonjour.mp3",
                expiresAt: null,
                durationSeconds: 1.5,
                languageTag: "fr",
                audioClipKind: "WordPronunciation",
              }
            : {
                items: [
                  {
                    id: AUDIO_ID,
                    title: "bonjour 发音",
                    languageTag: "fr",
                    kind: "WordPronunciation",
                    processingStatus: "Ready",
                    publicationStatus: "Published",
                    durationSeconds: 1.5,
                    failureCode: null,
                    updatedAt: "2026-08-01T10:00:00Z",
                  },
                ],
                page: 1,
                pageSize: 20,
                totalCount: 1,
                totalPages: 1,
              },
        ),
      ),
    );
    const onSelect = vi.fn();
    const user = userEvent.setup();
    render(
      <Provider store={createAppStore()}>
        <ThemeProvider>
          <TooltipProvider>
            <WordAudioPicker
              kind="WordPronunciation"
              language="fr"
              selectedId={null}
              onSelect={onSelect}
              onClose={() => {}}
            />
          </TooltipProvider>
        </ThemeProvider>
      </Provider>,
    );

    expect(await screen.findByText("bonjour 发音")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "试听 bonjour 发音" }));
    expect(await screen.findByText("正在试听：bonjour 发音")).toBeVisible();
    expect(document.querySelector("audio")?.getAttribute("src")).toBe(
      "https://media.example.test/bonjour.mp3",
    );
    await user.click(screen.getByRole("button", { name: "选择" }));
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ id: AUDIO_ID, kind: "WordPronunciation" }),
    );
    expect(requestMock.mock.calls[0][0].url).toContain(
      "processingStatus=Ready&publicationStatus=Published",
    );
    expect(requestMock.mock.calls[1][0].url).toBe(
      `/audio/${AUDIO_ID}/playback`,
    );
  });
});
