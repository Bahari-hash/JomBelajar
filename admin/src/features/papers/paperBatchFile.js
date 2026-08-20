export const PAPER_BATCH_MAX_FILE_SIZE = 20 * 1024 * 1024;

export const PAPER_BATCH_EXAMPLE = Object.freeze({
  papers: [
    {
      title: "基础题型与听力测试",
      description: "批量导入的草稿试卷",
      instructions: "请按题目要求作答。",
      categoryNames: ["基础练习", "听力"],
      passingScorePercentage: 60,
      questions: [
        {
          type: "SingleChoice",
          prompt: "Which word means ‘你好’ in English?",
          explanation: "Hello 表示‘你好’。",
          points: 2,
          sortOrder: 0,
          options: [
            { text: "Hello", isCorrect: true, sortOrder: 0 },
            { text: "Thanks", isCorrect: false, sortOrder: 1 },
            { text: "Goodbye", isCorrect: false, sortOrder: 2 },
          ],
        },
        {
          type: "TrueFalse",
          prompt: "‘World’ 的意思是‘世界’。",
          explanation: "World 可以表示‘世界’。",
          points: 1,
          sortOrder: 1,
          correctBoolean: true,
        },
        {
          type: "FillBlank",
          prompt: "Complete the sentence: Hello, ___!",
          explanation: "此处可以填写 world。",
          points: 2,
          sortOrder: 2,
          fillBlankCaseSensitive: false,
          acceptedAnswers: [
            { text: "world", sortOrder: 0 },
            { text: "the world", sortOrder: 1 },
          ],
        },
        {
          type: "Dictation",
          prompt: "听音频，填写缺失内容。",
          explanation: "答案需要完整填写。",
          points: 2,
          sortOrder: 3,
          audioFileName: "hello.mp3",
          blanks: [
            { answer: "hello", sortOrder: 0 },
            { answer: "world", sortOrder: 1 },
          ],
        },
      ],
    },
  ],
});

export const PAPER_BATCH_EXAMPLE_TEXT = JSON.stringify(
  PAPER_BATCH_EXAMPLE,
  null,
  2,
);
export const PAPER_BATCH_EXAMPLE_URL = `data:application/json;charset=utf-8,${encodeURIComponent(PAPER_BATCH_EXAMPLE_TEXT)}`;

export async function readPaperBatchFile(file) {
  if (!(file instanceof File) || !file.name.toLowerCase().endsWith(".json"))
    throw new Error("请选择 JSON 文件。");
  if (file.size <= 0) throw new Error("JSON 文件不能为空。");
  if (file.size > PAPER_BATCH_MAX_FILE_SIZE)
    throw new Error("JSON 文件不能超过 20 MB。");
  let value;
  try {
    value = JSON.parse(await file.text());
  } catch {
    throw new Error("JSON 语法无效，请检查括号、引号和逗号。");
  }
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("JSON 根节点必须是对象。");
  if (!Array.isArray(value.papers))
    throw new Error("JSON 根节点必须包含 papers 数组。");
  return value;
}
