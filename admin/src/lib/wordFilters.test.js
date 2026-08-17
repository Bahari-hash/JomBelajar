import { describe, expect, it } from "vitest";
import { readWordFilters, writeWordFilters } from "@/lib/wordFilters.js";

describe("word filters", () => {
  it("round trips only supported administrator filters", () => {
    const filters = {
      page: 3,
      pageSize: 50,
      keyword: "bonjour",
      partOfSpeech: "Noun",
      definition: "问候",
    };
    expect(readWordFilters(writeWordFilters(filters))).toEqual(filters);
  });

  it("drops retired and unsafe query values", () => {
    const filters = readWordFilters(
      new URLSearchParams(
        "page=0&pageSize=500&keyword=bad%0Avalue&language=fr&status=Published&partOfSpeech=Article",
      ),
    );
    expect(filters).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      partOfSpeech: "",
      definition: "",
    });
    expect(writeWordFilters(filters).toString()).toBe("");
  });
});
