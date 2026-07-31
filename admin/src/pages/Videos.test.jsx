import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  axiosResponse,
  mockHttpClient,
  userListItem,
  videoCategory,
  videoListItem,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

describe("Videos", () => {
  it("restores URL filters and renders the global video list", async () => {
    const requestMock = mockHttpClient((config) => {
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [videoCategory()],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      if (config.url.startsWith("/admin/users"))
        return Promise.resolve(
          axiosResponse({
            items: [userListItem({ role: "Admin" })],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      return Promise.resolve(
        axiosResponse({
          items: [
            videoListItem({
              id: "0198b9f2-01a2-7def-8123-0123456789ab",
              concurrencyStamp: "00000000-0000-0000-0000-000000000000",
            }),
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    renderAppAt("/videos?processingStatus=Ready&publicationStatus=Draft");
    expect(
      await screen.findByRole("heading", { level: 1, name: "视频管理" }),
    ).toBeVisible();
    const title = await screen.findByRole("link", { name: "French greetings" });
    expect(within(title.closest("tr")).getByText("已就绪")).toBeVisible();
    expect(
      requestMock.mock.calls.some(
        ([config]) =>
          config.url ===
          "/admin/videos?page=1&pageSize=20&processingStatus=Ready&publicationStatus=Draft",
      ),
    ).toBe(true);
  });
});
