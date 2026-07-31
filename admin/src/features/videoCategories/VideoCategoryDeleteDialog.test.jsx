import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { VideoCategoryDeleteDialog } from "@/features/videoCategories/VideoCategoryDeleteDialog.jsx";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient, videoCategory } from "@/test/http.js";

describe("VideoCategoryDeleteDialog", () => {
  it("requires separate clear and delete confirmations", async () => {
    const user = userEvent.setup();
    const category = videoCategory({ videoCount: 3 });
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/videos")
            ? { categoryId: category.id, removedVideoCount: 3 }
            : undefined,
          config.url.endsWith("/videos") ? 200 : 204,
        ),
      ),
    );
    const onCleared = vi.fn().mockResolvedValue(undefined);
    const onDone = vi.fn();
    const store = createAppStore();
    render(
      <Provider store={store}>
        <VideoCategoryDeleteDialog
          category={category}
          onClose={vi.fn()}
          onDone={onDone}
          onCleared={onCleared}
        />
      </Provider>,
    );
    let dialog = await screen.findByRole("alertdialog");
    await user.click(
      within(dialog).getByRole("button", { name: "确认解除关联" }),
    );
    expect(onCleared).toHaveBeenCalledOnce();
    expect(onDone).not.toHaveBeenCalled();
    dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByText(/删除仍需再次确认/)).toBeVisible();
    await user.click(within(dialog).getByRole("button", { name: "确认删除" }));
    expect(onDone).toHaveBeenCalledOnce();
    expect(requestMock.mock.calls.map(([config]) => config.url)).toEqual([
      `/admin/video-categories/${category.id}/videos`,
      `/admin/video-categories/${category.id}`,
    ]);
  });
});
