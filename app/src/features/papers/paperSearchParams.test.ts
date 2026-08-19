import { describe, expect, it } from "vitest";
import {
  createPaperSearchParams,
  isPaperGuid,
  parsePaperSearchParams,
} from "@/features/papers/paperSearchParams";

const CATEGORY_ID = "11111111-2222-3333-4444-555555555555";

describe("paperSearchParams", () => {
  it("normalizes keyword, category and page", () => {
    const result = parsePaperSearchParams(
      new URLSearchParams(
        `keyword=%20grammar%20&categoryId=${CATEGORY_ID}&page=3`,
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
  });

  it("drops invalid values", () => {
    expect(
      parsePaperSearchParams(
        new URLSearchParams("page=0&categoryId=bad&keyword=bad%00value"),
      ).state,
    ).toEqual({ keyword: "", categoryId: null, page: 1 });
  });

  it("writes meaningful values", () => {
    expect(
      createPaperSearchParams({
        keyword: " grammar ",
        categoryId: CATEGORY_ID,
        page: 1,
      }).toString(),
    ).toBe(`keyword=grammar&categoryId=${CATEGORY_ID}`);
  });

  it("validates route identifiers", () => {
    expect(isPaperGuid(CATEGORY_ID)).toBe(true);
    expect(isPaperGuid("bad")).toBe(false);
  });
});
