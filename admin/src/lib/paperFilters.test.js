import { describe, expect, it } from "vitest";
import { readPaperFilters, writePaperFilters } from "@/lib/paperFilters.js";

describe("paper filters", () => {
  it("round trips all supported administrator filters", () => {
    const filters = {
      page: 3,
      pageSize: 50,
      keyword: "beginner",
      status: "Published",
      categoryId: "11111111-1111-4111-8111-111111111111",
    };
    expect(readPaperFilters(writePaperFilters(filters))).toEqual(filters);
  });

  it("accepts only UUID category URL state", () => {
    const filters = readPaperFilters(
      new URLSearchParams(
        "categoryId=11111111-1111-4111-8111-111111111111&page=2",
      ),
    );

    expect(filters.categoryId).toBe("11111111-1111-4111-8111-111111111111");
    expect(writePaperFilters(filters).toString()).toBe(
      "page=2&categoryId=11111111-1111-4111-8111-111111111111",
    );
    expect(
      readPaperFilters(new URLSearchParams("categoryId=bad")).categoryId,
    ).toBe("");
  });

  it("replaces unsafe or unsupported values with defaults", () => {
    expect(
      readPaperFilters(
        new URLSearchParams(
          "page=0&pageSize=500&keyword=bad%0Avalue&categoryId=bad&status=Deleted",
        ),
      ),
    ).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      status: "",
      categoryId: "",
    });
  });
});
