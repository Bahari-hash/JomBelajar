import { describe, expect, it } from "vitest";
import {
  PAPER_BATCH_EXAMPLE,
  readPaperBatchFile,
} from "@/features/papers/paperBatchFile.js";

describe("paperBatchFile", () => {
  it("provides import examples for every supported question type", () => {
    const questions = PAPER_BATCH_EXAMPLE.papers.flatMap(
      (paper) => paper.questions,
    );
    const byType = new Map(questions.map((question) => [question.type, question]));

    expect([...byType.keys()]).toEqual([
      "SingleChoice",
      "TrueFalse",
      "FillBlank",
      "Dictation",
    ]);
    expect(byType.get("SingleChoice")).toMatchObject({
      options: expect.arrayContaining([
        { text: expect.any(String), isCorrect: true, sortOrder: 0 },
      ]),
    });
    expect(byType.get("TrueFalse")).toMatchObject({
      correctBoolean: expect.any(Boolean),
    });
    expect(byType.get("FillBlank")).toMatchObject({
      acceptedAnswers: expect.arrayContaining([
        { text: expect.any(String), sortOrder: 0 },
      ]),
    });
    expect(byType.get("Dictation")).toMatchObject({
      audioFileName: expect.any(String),
      blanks: expect.arrayContaining([
        { answer: expect.any(String), sortOrder: 0 },
      ]),
    });
  });

  it("reads a paper batch JSON object", async () => {
    const file = new File(
      [JSON.stringify(PAPER_BATCH_EXAMPLE)],
      "papers.json",
      {
        type: "application/json",
      },
    );

    await expect(readPaperBatchFile(file)).resolves.toEqual(
      PAPER_BATCH_EXAMPLE,
    );
  });

  it("rejects a JSON root without papers", async () => {
    const file = new File(["{}"], "papers.json", {
      type: "application/json",
    });

    await expect(readPaperBatchFile(file)).rejects.toThrow("papers 数组");
  });
});
