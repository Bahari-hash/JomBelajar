import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderAppAt } from "@/test/renderApp.jsx";

describe("AdminLayout", () => {
  it("exposes the dashboard through the shared navigation", async () => {
    renderAppAt("/");

    const desktopNavigation = await screen.findByRole("navigation", {
      name: "主导航",
    });
    expect(
      within(desktopNavigation).getByRole("link", { name: "工作台" }),
    ).toHaveAttribute("href", "/");
  });

  it("opens and closes the mobile navigation while restoring trigger focus", async () => {
    const user = userEvent.setup();
    renderAppAt("/");
    const trigger = await screen.findByRole("button", { name: "打开导航菜单" });

    await user.click(trigger);
    expect(await screen.findByRole("dialog")).toBeVisible();

    await user.keyboard("{Escape}");
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(trigger).toHaveFocus();
  });

  it("closes the mobile navigation after choosing a destination", async () => {
    const user = userEvent.setup();
    renderAppAt("/");

    await user.click(
      await screen.findByRole("button", { name: "打开导航菜单" }),
    );
    const dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("link", { name: "工作台" }));

    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
  });
});
