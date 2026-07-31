import { describe, expect, it } from "vitest";
import {
  readArticleCategoryFilters,
  readArticleFilters,
  writeArticleCategoryFilters,
  writeArticleFilters,
} from "@/lib/articleFilters.js";

describe("article filters", () => {
  it("normalizes article search parameters and writes canonical values", () => {
    const filters = readArticleFilters(
      new URLSearchParams(
        "page=2&pageSize=50&keyword= grammar &categoryId=44444444-4444-4444-8444-444444444444&status=Published",
      ),
    );
    expect(filters).toEqual({
      page: 2,
      pageSize: 50,
      keyword: "grammar",
      categoryId: "44444444-4444-4444-8444-444444444444",
      status: "Published",
    });
    expect(writeArticleFilters(filters).toString()).toBe(
      "page=2&pageSize=50&keyword=grammar&categoryId=44444444-4444-4444-8444-444444444444&status=Published",
    );
  });

  it("uses safe defaults for invalid article and category values", () => {
    expect(
      readArticleFilters(
        new URLSearchParams(
          "page=0&pageSize=101&status=1&categoryId=nope&keyword=%00",
        ),
      ),
    ).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      categoryId: "",
      status: "",
    });
    const categories = readArticleCategoryFilters(
      new URLSearchParams("includeInactive=false&page=bad"),
    );
    expect(categories).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      includeInactive: false,
    });
    expect(writeArticleCategoryFilters(categories).toString()).toBe(
      "includeInactive=false",
    );
  });

  it("preserves non-empty standard Guid category identifiers", () => {
    const categoryId = "0198c8d0-1234-7abc-8def-0123456789ab";

    expect(
      readArticleFilters(new URLSearchParams({ categoryId })).categoryId,
    ).toBe(categoryId);
    expect(
      readArticleFilters(
        new URLSearchParams({
          categoryId: "00000000-0000-0000-0000-000000000000",
        }),
      ).categoryId,
    ).toBe("");
  });
});
