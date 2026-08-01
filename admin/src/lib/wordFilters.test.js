import { describe, expect, it } from "vitest";
import { readWordFilters, writeWordFilters } from "@/lib/wordFilters.js";

describe("word filters", () => {
  it("round trips all supported administrator filters", () => {
    const filters = {
      page: 3,
      pageSize: 50,
      keyword: "bonjour",
      language: "fr",
      status: "Published",
      partOfSpeech: "Noun",
      definition: "问候",
    };
    expect(readWordFilters(writeWordFilters(filters))).toEqual(filters);
  });

  it("replaces unsafe or unsupported values with defaults", () => {
    const filters = readWordFilters(
      new URLSearchParams(
        "page=0&pageSize=500&keyword=bad%0Avalue&language=toolong-language-tag-that-exceeds-thirty-five-characters&status=Deleted&partOfSpeech=Article",
      ),
    );
    expect(filters).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      language: "",
      status: "",
      partOfSpeech: "",
      definition: "",
    });
  });
});
