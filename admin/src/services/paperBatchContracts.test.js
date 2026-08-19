import { describe, expect, it } from "vitest";
import {
  normalizePaperBatchImport,
  normalizePaperBatchValidation,
} from "@/services/paperBatchContracts.js";

describe("paperBatchContracts", () => {
  it("normalizes validation preview and matching counts", () => {
    const response = {
      isValid: true,
      summary: {
        paperCount: 1,
        questionCount: 1,
        dictationQuestionCount: 1,
        dictationBlankCount: 2,
        categoryReferenceCount: 1,
        matchedCategoryReferenceCount: 1,
        audioReferenceCount: 1,
        matchedAudioReferenceCount: 1,
      },
      papers: [
        {
          paperIndex: 0,
          title: "听写",
          isValid: true,
          questionCount: 1,
          categoryNames: ["听力"],
          matchedCategoryCount: 1,
          audioFileNames: ["hello.mp3"],
          matchedAudioCount: 1,
        },
      ],
      errors: [],
    };

    expect(normalizePaperBatchValidation(response)).toEqual(response);
  });

  it("requires the imported count to match created items", () => {
    expect(() =>
      normalizePaperBatchImport({
        importedCount: 2,
        items: [{ paperIndex: 0, paperId: "paper-id" }],
      }),
    ).toThrow("imported count");
  });
});
