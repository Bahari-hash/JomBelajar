import { describe, expect, it } from "vitest";
import {
  readWordBatchFile,
  WORD_BATCH_EXAMPLE,
  WORD_BATCH_EXAMPLE_TEXT,
  WORD_BATCH_MAX_FILE_SIZE,
} from "@/features/words/wordBatchFile.js";

function jsonFile(value, name = "words.json") {
  return new File([typeof value === "string" ? value : JSON.stringify(value)], name, {
    type: "application/json",
  });
}

describe("wordBatchFile", () => {
  it("returns a valid JSON object without duplicating business validation", async () => {
    const value = {
      words: [{ headword: "hello", unexpectedFutureField: true }],
    };

    await expect(readWordBatchFile(jsonFile(value))).resolves.toEqual(value);
  });

  it.each([
    ["non-json extension", jsonFile({ words: [] }, "words.txt"), "请选择 JSON 文件。"],
    ["empty file", new File([], "words.json", { type: "application/json" }), "JSON 文件不能为空。"],
  ])("rejects a %s", async (_case, file, message) => {
    await expect(readWordBatchFile(file)).rejects.toThrow(message);
  });

  it("rejects files larger than 20 MB before reading", async () => {
    const file = jsonFile({ words: [] });
    Object.defineProperty(file, "size", {
      configurable: true,
      value: WORD_BATCH_MAX_FILE_SIZE + 1,
    });

    await expect(readWordBatchFile(file)).rejects.toThrow(
      "JSON 文件不能超过 20 MB。",
    );
  });

  it.each([
    ["invalid syntax", "{", "JSON 语法无效，请检查括号、引号和逗号。"],
    ["array root", [], "JSON 根节点必须是对象。"],
    ["missing words", {}, "JSON 根节点必须包含 words 数组。"],
  ])("rejects %s", async (_case, value, message) => {
    await expect(readWordBatchFile(jsonFile(value))).rejects.toThrow(message);
  });

  it("publishes a contract-compatible example", () => {
    const row = WORD_BATCH_EXAMPLE.words[0];
    expect(row.audioFileName).toBe("hello.mp3");
    expect(row.senses[0]).toMatchObject({
      partOfSpeech: "Interjection",
      usageNote: null,
    });
    expect(row.senses[0].examples[0].audioFileName).toBe(
      "hello-example-1.mp3",
    );
    expect(JSON.parse(WORD_BATCH_EXAMPLE_TEXT)).toEqual(WORD_BATCH_EXAMPLE);
    expect(WORD_BATCH_EXAMPLE_TEXT).not.toContain("audioResourceId");
  });
});
