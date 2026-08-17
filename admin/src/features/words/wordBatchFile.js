export const WORD_BATCH_MAX_FILE_SIZE = 20 * 1024 * 1024;

export const WORD_BATCH_EXAMPLE = Object.freeze({
  words: [
    {
      headword: "hello",
      audioFileName: "hello.mp3",
      senses: [
        {
          partOfSpeech: "Interjection",
          definition: "你好；喂",
          usageNote: null,
          sortOrder: 0,
          examples: [
            {
              sentence: "Hello, how are you?",
              translation: "你好，你怎么样？",
              audioFileName: "hello-example-1.mp3",
              sortOrder: 0,
            },
          ],
        },
      ],
    },
  ],
});

export const WORD_BATCH_EXAMPLE_TEXT = JSON.stringify(
  WORD_BATCH_EXAMPLE,
  null,
  2,
);
export const WORD_BATCH_EXAMPLE_URL = `data:application/json;charset=utf-8,${encodeURIComponent(WORD_BATCH_EXAMPLE_TEXT)}`;

/** Reads only the file envelope and JSON root needed before server validation. */
export async function readWordBatchFile(file) {
  if (!(file instanceof File) || !file.name.toLowerCase().endsWith(".json"))
    throw new Error("请选择 JSON 文件。");
  if (file.size <= 0) throw new Error("JSON 文件不能为空。");
  if (file.size > WORD_BATCH_MAX_FILE_SIZE)
    throw new Error("JSON 文件不能超过 20 MB。");

  const text = await file.text();
  let value;
  try {
    value = JSON.parse(text);
  } catch {
    throw new Error("JSON 语法无效，请检查括号、引号和逗号。");
  }
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("JSON 根节点必须是对象。");
  if (!Array.isArray(value.words))
    throw new Error("JSON 根节点必须包含 words 数组。");
  return value;
}
