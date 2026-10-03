import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { BulkResourceActions } from "./BulkResourceActions.jsx";
import { collectResourceSnapshot } from "./collectResourceSnapshot.js";

describe("BulkResourceActions", () => {
  it("collects every page and deduplicates snapshot ids", async () => {
    const load = vi.fn(async page => ({ totalPages: 2, items: page === 1 ? [{ id: "a" }] : [{ id: "a" }, { id: "b" }] }));
    expect(await collectResourceSnapshot(load)).toEqual([{ id: "a" }, { id: "b" }]);
    expect(load.mock.calls).toEqual([[1], [2]]);
  });
  it("requires exact confirmation, deletes only the snapshot and reports in-use audio", async () => {
    const user = userEvent.setup(); const done = vi.fn();
    const remove = vi.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce({ errorCode: "AudioInUse" });
    const load = vi.fn(async page => ({ totalPages: 2, items: [{ id: String(page), name: `audio-${page}` }] }));
    render(<BulkResourceActions label="音频" loadPage={load} remove={remove} onDone={done} />);
    await user.click(screen.getByRole("button", { name: "删除全部音频", exact: true }));
    const dialog = await screen.findByRole("alertdialog");
    expect(within(dialog).getByText("audio-2")).toBeVisible();
    const confirm = within(dialog).getByRole("button", { name: "确认删除" });
    expect(confirm).toBeDisabled(); expect(remove).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText("删除确认文字"), "删除全部音频");
    await user.click(confirm);
    expect(await screen.findByText("已删除 1 个音频，1 个未删除。")).toBeVisible();
    expect(screen.getByText(/音频被内容引用/)).toBeVisible();
    expect(remove.mock.calls.map(([item]) => item.id)).toEqual(["1", "2"]);
    expect(done).toHaveBeenCalledTimes(1);
  });
  it("cancels without deletion and selected mode uses only selected items", async () => {
    const user = userEvent.setup(); const remove = vi.fn().mockResolvedValue(undefined);
    render(<BulkResourceActions label="音频" selected={[{ id: "x", name: "chosen" }]} loadPage={vi.fn()} remove={remove} />);
    await user.click(screen.getByRole("button", { name: "批量删除音频" }));
    await user.click(screen.getByRole("button", { name: "取消" })); expect(remove).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "批量删除音频" }));
    await user.click(screen.getByRole("button", { name: "确认删除" }));
    expect(remove).toHaveBeenCalledWith({ id: "x", name: "chosen" });
  });
  it("never enables deletion if collecting the full list fails", async () => {
    const user = userEvent.setup(); const remove = vi.fn();
    render(<BulkResourceActions label="单词" allOnly loadPage={vi.fn().mockRejectedValue(new Error("offline"))} remove={remove} />);
    await user.click(screen.getByRole("button", { name: "删除全部单词" }));
    expect(await screen.findByRole("alert")).toBeVisible(); expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(remove).not.toHaveBeenCalled();
  });
});
