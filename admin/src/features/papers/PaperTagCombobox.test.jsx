import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { PaperTagCombobox } from "@/features/papers/PaperTagCombobox.jsx";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

function renderCombobox(props = {}) {
  const onChange = props.onChange ?? vi.fn();
  render(
    <Provider store={createAppStore()}>
      <PaperTagCombobox value="" onChange={onChange} {...props} />
    </Provider>,
  );
  return { onChange };
}

describe("PaperTagCombobox", () => {
  it("searches and selects one Paper tag", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [{ name: "grammar", paperCount: 8 }],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );
    const user = userEvent.setup();
    const { onChange } = renderCombobox();

    const trigger = screen.getByRole("combobox", { name: "标签" });
    await user.click(trigger);
    const search = screen.getByRole("searchbox", { name: "搜索试卷标签" });
    expect(search).toHaveFocus();
    await user.type(search, "gram{Enter}");
    await user.click(await screen.findByRole("option", { name: "grammar 8" }));

    expect(onChange).toHaveBeenCalledWith("grammar");
    expect(requestMock.mock.calls.at(-1)[0].url).toBe(
      "/admin/paper-tags?page=1&pageSize=20&keyword=gram",
    );
    expect(trigger).toHaveFocus();
  });

  it("keeps a selected tag that is not in current results", () => {
    renderCombobox({ value: "cet-4" });

    expect(screen.getByRole("combobox", { name: "标签" })).toHaveTextContent(
      "cet-4",
    );
  });

  it("recovers from a tag directory contract error", async () => {
    tokenVault.install("access", "refresh");
    let calls = 0;
    mockHttpClient(() => {
      calls += 1;
      return Promise.resolve(
        axiosResponse({
          items:
            calls === 1
              ? [{ name: "", paperCount: 1 }]
              : [{ name: "grammar", paperCount: 8 }],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
      );
    });
    const user = userEvent.setup();
    renderCombobox();

    await user.click(screen.getByRole("combobox", { name: "标签" }));
    await user.click(await screen.findByRole("button", { name: "重试" }));

    expect(
      await screen.findByRole("option", { name: "grammar 8" }),
    ).toBeVisible();
    expect(calls).toBe(2);
  });

  it("closes with Escape and restores trigger focus", async () => {
    tokenVault.install("access", "refresh");
    mockHttpClient(() =>
      Promise.resolve(
        axiosResponse({
          items: [],
          page: 1,
          pageSize: 20,
          totalCount: 0,
          totalPages: 0,
        }),
      ),
    );
    const user = userEvent.setup();
    renderCombobox();

    const trigger = screen.getByRole("combobox", { name: "标签" });
    await user.click(trigger);
    expect(
      screen.getByRole("searchbox", { name: "搜索试卷标签" }),
    ).toHaveFocus();
    await user.keyboard("{Escape}");

    await waitFor(() => expect(trigger).toHaveFocus());
    expect(
      screen.queryByRole("searchbox", { name: "搜索试卷标签" }),
    ).not.toBeInTheDocument();
  });
});
