import { describe, expect, it } from "vitest";
import {
  readVideoCategoryFilters,
  readVideoFilters,
  writeVideoCategoryFilters,
  writeVideoFilters,
} from "@/lib/videoFilters.js";

describe("video filters", () => {
  it("normalizes every supported list parameter", () => {
    const filters = readVideoFilters(
      new URLSearchParams(
        "page=2&pageSize=50&keyword=French&processingStatus=Ready&publicationStatus=Published&categoryId=77777777-7777-4777-8777-777777777777&createdById=11111111-1111-4111-8111-111111111111",
      ),
    );
    expect(filters).toMatchObject({
      page: 2,
      pageSize: 50,
      processingStatus: "Ready",
      publicationStatus: "Published",
    });
    expect(writeVideoFilters(filters).toString()).toContain(
      "createdById=11111111-1111-4111-8111-111111111111",
    );
  });

  it("drops invalid enums, UUIDs, control characters and paging", () => {
    expect(
      readVideoFilters(
        new URLSearchParams(
          "page=0&pageSize=200&keyword=bad%00value&processingStatus=Done&publicationStatus=Deleted&categoryId=invalid",
        ),
      ),
    ).toEqual({
      page: 1,
      pageSize: 20,
      keyword: "",
      processingStatus: "",
      publicationStatus: "",
      categoryId: "",
      createdById: "",
    });
  });

  it("keeps category defaults compact and shareable", () => {
    const filters = readVideoCategoryFilters(new URLSearchParams());
    expect(filters.includeInactive).toBe(true);
    expect(writeVideoCategoryFilters(filters).toString()).toBe("");
    expect(
      writeVideoCategoryFilters({
        ...filters,
        includeInactive: false,
      }).toString(),
    ).toBe("includeInactive=false");
  });
});
