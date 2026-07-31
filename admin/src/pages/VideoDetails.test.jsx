import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import {
  adminVideo,
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

describe("VideoDetails", () => {
  it("saves metadata with the latest concurrency stamp", async () => {
    const user = userEvent.setup();
    const initial = adminVideo();
    const requestMock = mockHttpClient((config) => {
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.method === "PUT")
        return Promise.resolve(
          axiosResponse(
            adminVideo({
              title: config.data.title,
              concurrencyStamp: "dddddddd-dddd-4ddd-8ddd-dddddddddddd",
            }),
          ),
        );
      return Promise.resolve(axiosResponse(initial));
    });
    renderAppAt(`/videos/${initial.id}`);
    const title = await screen.findByLabelText(/^标题/);
    await user.clear(title);
    await user.type(title, "Updated title");
    await user.click(screen.getByRole("button", { name: "保存修改" }));
    await screen.findByText("视频信息已保存。");
    const update = requestMock.mock.calls.find(
      ([config]) => config.method === "PUT",
    )[0];
    expect(update.data).toMatchObject({
      title: "Updated title",
      concurrencyStamp: initial.concurrencyStamp,
    });
  });

  it("keeps local input when the server reports a concurrency conflict", async () => {
    const user = userEvent.setup();
    const initial = adminVideo();
    mockHttpClient((config) => {
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [],
            page: 1,
            pageSize: 100,
            totalCount: 0,
            totalPages: 0,
          }),
        );
      if (config.method === "PUT")
        return Promise.reject(
          axiosHttpError(
            {
              status: 409,
              detail: "Conflict",
              errorCode: "VideoConcurrencyConflict",
            },
            409,
          ),
        );
      return Promise.resolve(axiosResponse(initial));
    });
    renderAppAt(`/videos/${initial.id}`);
    const title = await screen.findByLabelText(/^标题/);
    await user.clear(title);
    await user.type(title, "Local unsaved title");
    await user.click(screen.getByRole("button", { name: "保存修改" }));
    expect(await screen.findByText(/你的输入仍然保留/)).toBeVisible();
    expect(title).toHaveValue("Local unsaved title");
  });
});
