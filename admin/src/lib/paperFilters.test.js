import { describe, expect, it } from "vitest";
import { readPaperFilters, writePaperFilters } from "@/lib/paperFilters.js";

describe("paper filters", () => {
  it("round trips all supported administrator filters", () => {
    const filters = {
      page: 3,
      pageSize: 50,
      keyword: "beginner",
      language: "en",
      status: "Published",
      tag: "cet-4",
    };
    expect(readPaperFilters(writePaperFilters(filters))).toEqual(filters);
  });

  it("normalizes Paper tag URL state", () => {
    const filters = readPaperFilters(
      new URLSearchParams("tag=%20CET-4%20&page=2"),
    );

    expect(filters.tag).toBe("cet-4");
    expect(writePaperFilters(filters).toString()).toBe("page=2&tag=cet-4");
    expect(
      readPaperFilters(new URLSearchParams(`tag=${"a".repeat(31)}`)).tag,
    ).toBe("");
  });

  it("replaces unsafe or unsupported values with defaults", () => {
    expect(
      readPaperFilters(
        new URLSearchParams(
          "page=0&pageSize=500&keyword=bad%0Avalue&language=toolong-language-tag-that-exceeds-thirty-five-characters&status=Deleted",
        ),
      ),
    ).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      language: "",
      status: "",
      tag: "",
    });
  });
});
