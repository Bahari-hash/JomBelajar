import { describe, expect, it } from "vitest";
import {
  createVideoSearchParams,
  parseVideoSearchParams,
} from "@/features/videos/videoSearchParams";

describe("videoSearchParams", () => {
  it("normalizes invalid list parameters without producing unsafe values", () => {
    const parsed = parseVideoSearchParams(
      new URLSearchParams("page=0&categoryId=nope&keyword=%09"),
    );
    expect(parsed.state).toEqual({ keyword: "", categoryId: null, page: 1 });
    expect(parsed.normalizedParams.toString()).toBe("");
    expect(parsed.needsNormalization).toBe(true);
  });

  it("serializes filters and preserves a valid category id", () => {
    const params = createVideoSearchParams({
      keyword: "  grammar ",
      categoryId: "11111111-2222-3333-4444-555555555555",
      page: 3,
    });
    expect(params.toString()).toBe(
      "keyword=++grammar+&categoryId=11111111-2222-3333-4444-555555555555&page=3",
    );
  });
});
