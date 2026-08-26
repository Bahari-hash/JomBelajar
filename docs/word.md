# 单词模块

## JSON 批量导入功能

单词导入示例:

```json
{
"words": [
    {
    "headword": "hello",
    "audioFileName": "hello.mp3",
    "senses": [
        {
        "partOfSpeech": "Interjection",
        "definition": "你好；喂",
        "usageNote": null,
        "sortOrder": 0,
        "examples": [
            {
            "sentence": "Hello, how are you?",
            "translation": "你好，你怎么样？",
            "audioFileName": "hello-example-1.mp3",
            "sortOrder": 0
            }
        ]
        }
    ]
    }
]
}
```

词性 (partOfSpeech):

```json
[
    "Noun",
    "Verb",
    "Adjective",
    "Adverb",
    "Pronoun",
    "Determiner",
    "Preposition",
    "Conjunction",
    "Interjection",
    "Numeral",
    "Particle",
    "Other"
]
```
