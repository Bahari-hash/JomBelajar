import { describe, expect, it } from "vitest";
import {
  createArticleSearchParams,
  isGuid,
  parseArticleSearchParams,
} from "@/features/articles/articleSearchParams";

const CATEGORY_ID = "11111111-2222-3333-4444-555555555555";

describe("article search params", () => {
  it("parses valid shareable article state", () => {
    const result = parseArticleSearchParams(
      new URLSearchParams(
        `keyword=%20grammar%20&categoryId=${CATEGORY_ID.toUpperCase()}&page=3`,
      ),
    );

    expect(result.state).toEqual({
      keyword: "grammar",
      categoryId: CATEGORY_ID,
      page: 3,
    });
    expect(result.normalizedParams.toString()).toBe(
      `keyword=grammar&categoryId=${CATEGORY_ID}&page=3`,
    );
    expect(result.needsNormalization).toBe(true);
  });

  it.each(["0", "-2", "1.5", "two", "9007199254740993"])(
    "falls back to page one for %s",
    (page) => {
      const result = parseArticleSearchParams(new URLSearchParams({ page }));
      expect(result.state.page).toBe(1);
      expect(result.normalizedParams.has("page")).toBe(false);
    },
  );

  it("removes invalid filters and unknown parameters", () => {
    const result = parseArticleSearchParams(
      new URLSearchParams({
        keyword: `valid\u0000invalid`,
        categoryId: "not-a-guid",
        tracking: "internal",
      }),
    );

    expect(result.state).toEqual({ keyword: "", categoryId: null, page: 1 });
    expect(result.normalizedParams.toString()).toBe("");
    expect(result.needsNormalization).toBe(true);
  });

  it("rejects overlong keywords and validates GUID shape", () => {
    const result = parseArticleSearchParams(
      new URLSearchParams({ keyword: "a".repeat(201) }),
    );

    expect(result.state.keyword).toBe("");
    expect(isGuid(CATEGORY_ID)).toBe(true);
    expect(isGuid("11111111-2222-3333-4444")).toBe(false);
  });

  it("omits defaults when serializing list state", () => {
    expect(
      createArticleSearchParams({
        keyword: "",
        categoryId: null,
        page: 1,
      }).toString(),
    ).toBe("");
  });
});
