import { describe, expect, it } from "vitest";
import {
  MAX_PAPER_KEYWORD_LENGTH,
  MAX_PAPER_TAG_LENGTH,
  createPaperSearchParams,
  hasPaperControlCharacters,
  isPaperGuid,
  parsePaperSearchParams,
} from "@/features/papers/paperSearchParams";

describe("paperSearchParams", () => {
  it.each([
    ["", { keyword: "", tag: null, page: 1 }, "", false],
    [
      "keyword=%20grammar%20&page=3",
      { keyword: "grammar", tag: null, page: 3 },
      "keyword=grammar&page=3",
      true,
    ],
    ["page=0", { keyword: "", tag: null, page: 1 }, "", true],
    ["page=1.5", { keyword: "", tag: null, page: 1 }, "", true],
    ["page=-1", { keyword: "", tag: null, page: 1 }, "", true],
    [
      "keyword=grammar&keyword=vocabulary",
      { keyword: "grammar", tag: null, page: 1 },
      "keyword=grammar",
      true,
    ],
    ["page=2&page=3", { keyword: "", tag: null, page: 2 }, "page=2", true],
    ["keyword=bad%00value", { keyword: "", tag: null, page: 1 }, "", true],
    [
      `keyword=${"a".repeat(MAX_PAPER_KEYWORD_LENGTH + 1)}`,
      { keyword: "", tag: null, page: 1 },
      "",
      true,
    ],
  ])(
    "parses and canonicalizes %s",
    (query, state, normalizedQuery, needsNormalization) => {
      const result = parsePaperSearchParams(new URLSearchParams(query));

      expect(result.state).toEqual(state);
      expect(result.normalizedParams.toString()).toBe(normalizedQuery);
      expect(result.needsNormalization).toBe(needsNormalization);
    },
  );

  it("uses the first safe value when duplicated values include an invalid one", () => {
    const result = parsePaperSearchParams(
      new URLSearchParams("keyword=grammar&page=invalid&page=2"),
    );

    expect(result.state).toEqual({ keyword: "grammar", tag: null, page: 2 });
  });

  it("normalizes a single Paper tag in URL state", () => {
    const result = parsePaperSearchParams(
      new URLSearchParams("tag=%20CET-4%20&keyword=grammar&page=3"),
    );

    expect(result.state).toEqual({
      keyword: "grammar",
      tag: "cet-4",
      page: 3,
    });
    expect(result.normalizedParams.toString()).toBe(
      "keyword=grammar&tag=cet-4&page=3",
    );
  });

  it("uses the first safe tag and removes invalid Paper tags", () => {
    const duplicated = parsePaperSearchParams(
      new URLSearchParams("tag=bad%00tag&tag=%20CET-6%20"),
    );
    const tooLong = parsePaperSearchParams(
      new URLSearchParams(`tag=${"a".repeat(MAX_PAPER_TAG_LENGTH + 1)}`),
    );

    expect(duplicated.state.tag).toBe("cet-6");
    expect(duplicated.normalizedParams.toString()).toBe("tag=cet-6");
    expect(tooLong.state.tag).toBeNull();
    expect(tooLong.normalizedParams.toString()).toBe("");
  });

  it("writes only trimmed meaningful values", () => {
    expect(
      createPaperSearchParams({
        keyword: " grammar ",
        tag: " CET-4 ",
        page: 1,
      }).toString(),
    ).toBe("keyword=grammar&tag=cet-4");
    expect(
      createPaperSearchParams({ keyword: "", tag: null, page: 1 }).toString(),
    ).toBe("");
  });

  it("detects ASCII control characters", () => {
    expect(hasPaperControlCharacters("line\nbreak")).toBe(true);
    expect(hasPaperControlCharacters("delete\u007f")).toBe(true);
    expect(hasPaperControlCharacters("普通文本")).toBe(false);
  });

  it.each([
    ["11111111-2222-3333-4444-555555555555", true],
    ["AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", true],
    ["paper-1", false],
    ["11111111222233334444555555555555", false],
    ["{11111111-2222-3333-4444-555555555555}", false],
    [null, false],
    [undefined, false],
  ])("validates route identifier %s", (value, expected) => {
    expect(isPaperGuid(value)).toBe(expected);
  });
});
