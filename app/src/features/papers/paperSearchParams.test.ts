import { describe, expect, it } from "vitest";
import {
  MAX_PAPER_KEYWORD_LENGTH,
  createPaperSearchParams,
  hasPaperControlCharacters,
  isPaperGuid,
  parsePaperSearchParams,
} from "@/features/papers/paperSearchParams";

describe("paperSearchParams", () => {
  it.each([
    ["", { keyword: "", page: 1 }, "", false],
    [
      "keyword=%20grammar%20&page=3",
      { keyword: "grammar", page: 3 },
      "keyword=grammar&page=3",
      true,
    ],
    ["page=0", { keyword: "", page: 1 }, "", true],
    ["page=1.5", { keyword: "", page: 1 }, "", true],
    ["page=-1", { keyword: "", page: 1 }, "", true],
    [
      "keyword=grammar&keyword=vocabulary",
      { keyword: "grammar", page: 1 },
      "keyword=grammar",
      true,
    ],
    ["page=2&page=3", { keyword: "", page: 2 }, "page=2", true],
    ["keyword=bad%00value", { keyword: "", page: 1 }, "", true],
    [
      `keyword=${"a".repeat(MAX_PAPER_KEYWORD_LENGTH + 1)}`,
      { keyword: "", page: 1 },
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

    expect(result.state).toEqual({ keyword: "grammar", page: 2 });
  });

  it("writes only trimmed meaningful values", () => {
    expect(
      createPaperSearchParams({ keyword: " grammar ", page: 1 }).toString(),
    ).toBe("keyword=grammar");
    expect(createPaperSearchParams({ keyword: "", page: 1 }).toString()).toBe(
      "",
    );
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
