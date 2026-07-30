import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { axiosResponse, mockHttpClient, userListItem } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

function pageResponse(items = [userListItem()]) {
  return {
    items,
    page: 2,
    pageSize: 5,
    totalCount: items.length,
    totalPages: 2,
  };
}

describe("Users", () => {
  it("reads supported URL filters and renders numeric response roles", async () => {
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse(pageResponse())),
    );

    renderAppAt("/users?page=2&pageSize=5&keyword=alice&role=Editor&status=Banned");

    expect(await screen.findByRole("heading", { level: 1, name: "用户管理" })).toBeVisible();
    const email = await screen.findByText("alice@example.test");
    expect(email).toBeVisible();
    expect(within(email.closest("tr")).getByText("编辑")).toBeVisible();
    expect(requestMock.mock.calls[0][0].url).toBe(
      "/admin/users?page=2&pageSize=5&keyword=alice&role=Editor&status=Banned",
    );
  });

  it("submits the required ban reason and refreshes the affected list", async () => {
    const user = userEvent.setup();
    const requestMock = mockHttpClient((config) => {
      if (config.url.endsWith("/ban")) {
        return Promise.resolve(axiosResponse(undefined, 204));
      }
      return Promise.resolve(axiosResponse(pageResponse()));
    });
    renderAppAt("/users?page=2&pageSize=5");

    await screen.findByText("alice@example.test");
    await user.click(screen.getByRole("button", { name: "管理用户 alice@example.test" }));
    await user.click(await screen.findByRole("menuitem", { name: "封禁用户" }));
    const dialog = await screen.findByRole("alertdialog");
    await user.type(within(dialog).getByLabelText("封禁原因"), "policy violation");
    await user.click(within(dialog).getByRole("button", { name: "确认封禁" }));

    await vi.waitFor(() => {
      expect(
        requestMock.mock.calls.some(([config]) => config.url.endsWith("/ban")),
      ).toBe(true);
    });
    const banCall = requestMock.mock.calls.find(([config]) =>
      config.url.endsWith("/ban"),
    );
    expect(banCall[0].data).toEqual({ reason: "policy violation" });
  });
});
