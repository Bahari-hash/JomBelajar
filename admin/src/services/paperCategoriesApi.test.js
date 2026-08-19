import { describe, expect, it } from "vitest";
import { paperCategoriesApi } from "@/services/paperCategoriesApi.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

describe("paperCategoriesApi", () => {
  it("loads inactive categories for management", async () => {
    const request = mockHttpClient(() =>
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
    const store = createAppStore();
    await store
      .dispatch(
        paperCategoriesApi.endpoints.getPaperCategories.initiate({
          page: 1,
          pageSize: 20,
          keyword: "",
          includeInactive: true,
        }),
      )
      .unwrap();
    expect(request.mock.calls[0][0].url).toBe(
      "/admin/paper-categories?page=1&pageSize=20&includeInactive=true",
    );
  });
});
