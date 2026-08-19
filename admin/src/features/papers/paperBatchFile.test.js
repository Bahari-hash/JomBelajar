import { describe, expect, it } from "vitest";
import {
  PAPER_BATCH_EXAMPLE,
  readPaperBatchFile,
} from "@/features/papers/paperBatchFile.js";

describe("paperBatchFile", () => {
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
