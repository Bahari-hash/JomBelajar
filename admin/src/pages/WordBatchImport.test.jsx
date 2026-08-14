import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { tokenVault } from "@/services/tokenVault.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const PAYLOAD = {
  rows: [
    { headword: "bonjour", senses: [], pronunciations: [] },
  ],
};

describe("WordBatchImport", () => {
  it("does not request validation for invalid JSON", async () => {
    const requestMock = mockHttpClient(() =>
      Promise.resolve(axiosResponse({})),
    );
    const user = userEvent.setup();
    renderAppAt("/words/batch");
    fireEvent.change(await screen.findByLabelText("批量 JSON *"), {
      target: { value: "{" },
    });
    await user.click(screen.getByRole("button", { name: "校验内容" }));
    expect(
      await screen.findByText("JSON 语法无效，请检查括号、引号和逗号。"),
    ).toBeVisible();
    expect(requestMock).not.toHaveBeenCalled();
  });

  it("validates, previews and atomically imports the same payload", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.endsWith("/validate")
            ? {
                isValid: true,
                errors: [],
                rows: [
                  { rowIndex: 0, normalized: PAYLOAD.rows[0], errors: [] },
                ],
              }
            : {
                batchId: "11111111-1111-4111-8111-111111111111",
                createdCount: 1,
                items: [
                  {
                    rowIndex: 0,
                    wordId: "22222222-2222-4222-8222-222222222222",
                  },
                ],
              },
        ),
      ),
    );
    const user = userEvent.setup();
    renderAppAt("/words/batch");
    fireEvent.change(await screen.findByLabelText("批量 JSON *"), {
      target: { value: JSON.stringify(PAYLOAD) },
    });
    await user.click(screen.getByRole("button", { name: "校验内容" }));
    expect(await screen.findByText("校验通过，可以导入")).toBeVisible();
    await user.click(screen.getByRole("button", { name: "确认原子导入" }));
    expect(await screen.findByText("批量导入成功")).toBeVisible();
    expect(
      requestMock.mock.calls.map(([config]) => [config.url, config.data]),
    ).toEqual([
      ["/admin/words/batch/validate", PAYLOAD],
      ["/admin/words/batch", PAYLOAD],
    ]);
  });
});
