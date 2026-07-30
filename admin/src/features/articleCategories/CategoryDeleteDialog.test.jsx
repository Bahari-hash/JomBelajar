import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { CategoryDeleteDialog } from "@/features/articleCategories/CategoryDeleteDialog.jsx";
import { createAppStore } from "@/store/index.js";
import {
  articleCategory,
  axiosHttpError,
  axiosResponse,
  mockHttpClient,
} from "@/test/http.js";

describe("CategoryDeleteDialog", () => {
  it("requires a separate clear confirmation after a concurrent in-use conflict", async () => {
    const user = userEvent.setup();
    const category = articleCategory({ articleCount: 0 });
    const onClose = vi.fn();
    const onDone = vi.fn();
    let deleteAttempts = 0;
    const requestMock = mockHttpClient((config) => {
      if (config.url.endsWith("/articles")) {
        return Promise.resolve(
          axiosResponse({ categoryId: category.id, removedArticleCount: 2 }),
        );
      }
      deleteAttempts += 1;
      if (deleteAttempts === 1) {
        return Promise.reject(
          axiosHttpError(
            {
              detail: "Category is in use.",
              errorCode: "ArticleCategoryInUse",
            },
            409,
          ),
        );
      }
      return Promise.resolve(axiosResponse(undefined, 204));
    });

    render(
      <Provider store={createAppStore()}>
        <TooltipProvider>
          <CategoryDeleteDialog
            category={category}
            onClose={onClose}
            onDone={onDone}
          />
        </TooltipProvider>
      </Provider>,
    );

    await user.click(screen.getByRole("button", { name: "确认删除" }));
    expect(
      await screen.findByRole("heading", { name: "解除文章关联" }),
    ).toBeVisible();
    expect(
      screen.getByText("分类仍有关联，请先明确解除所有文章关联。"),
    ).toBeVisible();

    await user.click(screen.getByRole("button", { name: "确认解除关联" }));
    expect(
      await screen.findByText("已解除 2 条关联。删除仍需再次确认。"),
    ).toBeVisible();
    expect(onClose).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "确认删除" }));
    expect(onDone).toHaveBeenCalledWith("分类“语法”已删除。");
    expect(onClose).toHaveBeenCalledOnce();
    expect(requestMock.mock.calls.map(([config]) => config.url)).toEqual([
      `/admin/article-categories/${category.id}`,
      `/admin/article-categories/${category.id}/articles`,
      `/admin/article-categories/${category.id}`,
    ]);
  });
});
