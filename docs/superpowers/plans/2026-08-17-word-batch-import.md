# 单词批量导入与音频批量上传 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为管理员增加音频库多文件上传和 JSON 单词批量导入，使最多 1000 个包含释义、例句及共享音频关联的单词可以在预览校验后原子创建。

**Architecture:** 后端以独立 `WordBatchService` 完成有界校验、音频文件名批量解析和事务导入，并抽取单词聚合构建器供单条与批量创建共享；校验接口不写入，导入接口重新校验并一次保存。管理端抽取可并发调用的单文件音频上传 runner，音频库在其上建立双文件并发队列；单词批量页面只做文件预检，业务校验完全依赖后端结构化响应。

**Tech Stack:** ASP.NET Core Minimal API、EF Core、PostgreSQL、FluentValidation、xUnit、Moq、FluentAssertions；React 19、React Router 7、Redux Toolkit Query、Axios、Vitest、Testing Library、Tailwind CSS、DaisyUI。

**提交约定：** 每个 Task 完成并验证后由执行代理自行创建 Git commit。只能显式 `git add` 当前 Task 的业务代码和测试文件，禁止使用 `git add .`、`git add -A`，禁止把 `docs/`、`word.md` 或其他用户未跟踪文件加入提交。

---

## 文件结构与职责

### 后端

- `server/TinyLang/Services/WordAggregateBuilder.cs`：规范化词头并从已验证的 `CreateWordRequest` 构建完整 `Word` 聚合，不访问数据库、不保存。
- `server/TinyLang/Dtos/WordBatchDtos.cs`：批量 JSON 输入、统计、预览、逐行错误和导入成功响应。
- `server/TinyLang/Services/IWordBatchService.cs`：批量校验和导入用例边界。
- `server/TinyLang/Services/WordBatchService.cs`：有界结构校验、单条规则复用、批内/数据库重复检查、音频名称批量解析、原子写入和竞态映射。
- `server/TinyLang/Endpoints/WordEndpoints.cs`：管理员批量校验和导入 endpoints，20 MB 请求限制及 `200/422` 响应。
- `server/TinyLang/Services/WordService.cs`：改为复用聚合构建器，单条创建/编辑语义保持不变。

### 管理端

- `admin/src/features/audio/useAudioUploadRunner.js`：封装一个音频文件从初始化到确认、处理轮询、失败清理和取消的无界面流程，允许多个调用独立并发。
- `admin/src/features/audio/AudioUploadControl.jsx`：保留现有单文件 UI，改用共享 runner。
- `admin/src/features/audio/AudioBatchUploadControl.jsx`：多选、最多两个文件并发、逐文件状态、取消和重试。
- `admin/src/services/wordBatchContracts.js`：批量校验和导入响应运行时校验。
- `admin/src/services/wordsApi.js`：批量校验与导入 RTK Query mutations。
- `admin/src/features/words/wordBatchFile.js`：20 MB 文件预检、JSON 读取和示例 JSON 常量。
- `admin/src/pages/WordBatchImport.jsx`：文件选择、校验预览、错误展示和确认导入页面。
- `admin/src/pages/Words.jsx`：批量导入入口和成功通知。

---

## Task 1：抽取共享单词聚合构建器

**Files:**

- Create: `server/TinyLang/Services/WordAggregateBuilder.cs`
- Modify: `server/TinyLang/Services/WordService.cs`
- Create: `server/TinyLang.UnitTests/WordAggregateBuilderTests.cs`
- Verify: `server/TinyLang.UnitTests/WordServiceTests.cs`

- [ ] **Step 1：编写聚合构建器测试。**

创建 `WordAggregateBuilderTests.cs`，先固定新建聚合所需的规范化和嵌套映射行为：

```csharp
public sealed class WordAggregateBuilderTests
{
    [Fact]
    public void CreateShouldNormalizeIdentityAndMapCompleteTarget()
    {
        var wordAudioId = Guid.NewGuid();
        var exampleAudioId = Guid.NewGuid();
        var request = new CreateWordRequest
        {
            Headword = "  Cafe\u0301  ",
            AudioResourceId = wordAudioId,
            Senses =
            [
                new WordSenseInput
                {
                    PartOfSpeech = PartOfSpeech.Noun,
                    Definition = "  a coffee shop  ",
                    UsageNote = "  informal  ",
                    SortOrder = 0,
                    Examples =
                    [
                        new ExampleSentenceInput
                        {
                            Sentence = "  Meet me at the cafe.  ",
                            Translation = "  在咖啡馆见。  ",
                            AudioResourceId = exampleAudioId,
                            SortOrder = 0
                        }
                    ]
                }
            ]
        };

        var word = WordAggregateBuilder.Create(request);

        word.Headword.Should().Be("Caf\u00e9");
        word.NormalizedHeadword.Should().Be("CAF\u00c9");
        word.AudioResourceId.Should().Be(wordAudioId);
        word.Senses.Single().Definition.Should().Be("a coffee shop");
        word.Senses.Single().UsageNote.Should().Be("informal");
        word.Senses.Single().Examples.Single().Sentence.Should()
            .Be("Meet me at the cafe.");
        word.Senses.Single().Examples.Single().AudioResourceId.Should()
            .Be(exampleAudioId);
    }
}
```

再测试空白词头和超长规范化词头分别抛出 `WordHeadwordRequired`、`WordHeadwordLengthLimit`。

- [ ] **Step 2：运行新测试确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordAggregateBuilderTests"
```

Expected: FAIL，原因是 `WordAggregateBuilder` 尚不存在。

- [ ] **Step 3：实现无数据库依赖的构建器。**

新增以下边界：

```csharp
internal static class WordAggregateBuilder
{
    public static Word Create(CreateWordRequest request)
    {
        var identity = NormalizeIdentity(request.Headword);
        var word = new Word
        {
            Headword = identity.Headword,
            NormalizedHeadword = identity.NormalizedHeadword,
            AudioResourceId = request.AudioResourceId
        };
        foreach (var sense in request.Senses)
            word.Senses.Add(CreateSense(word, sense));
        return word;
    }

    public static WordIdentity NormalizeIdentity(string headword)
    {
        if (string.IsNullOrWhiteSpace(headword))
            throw new RequestValidationException(ErrorCodes.WordHeadwordRequired);

        var display = WordTextNormalizer.NormalizeHeadwordForDisplay(headword);
        var comparisonKey = WordTextNormalizer.CreateHeadwordComparisonKey(headword);
        if (display.Length > WordConstraints.MaxHeadwordLength ||
            comparisonKey.Length > WordConstraints.MaxHeadwordLength)
            throw new RequestValidationException(
                ErrorCodes.WordHeadwordLengthLimit);
        return new WordIdentity(display, comparisonKey);
    }

    public static WordSense CreateSense(Word word, WordSenseInput input)
    {
        var sense = new WordSense
        {
            WordId = word.Id,
            Word = word,
            Definition = string.Empty
        };
        ApplySenseValues(sense, input);
        foreach (var exampleInput in input.Examples)
            sense.Examples.Add(CreateExample(sense, exampleInput));
        return sense;
    }

    public static void ApplySenseValues(WordSense sense, WordSenseInput input)
    {
        sense.PartOfSpeech = input.PartOfSpeech;
        sense.Definition = input.Definition.Trim();
        sense.UsageNote = string.IsNullOrWhiteSpace(input.UsageNote)
            ? null
            : input.UsageNote.Trim();
        sense.SortOrder = input.SortOrder;
    }

    public static ExampleSentence CreateExample(
        WordSense sense,
        ExampleSentenceInput input)
    {
        var example = new ExampleSentence
        {
            WordSenseId = sense.Id,
            WordSense = sense,
            Sentence = string.Empty,
            Translation = string.Empty
        };
        ApplyExampleValues(example, input);
        return example;
    }

    public static void ApplyExampleValues(
        ExampleSentence example,
        ExampleSentenceInput input)
    {
        example.AudioResourceId = input.AudioResourceId;
        example.Sentence = input.Sentence.Trim();
        example.Translation = input.Translation.Trim();
        example.SortOrder = input.SortOrder;
    }

    internal readonly record struct WordIdentity(
        string Headword,
        string NormalizedHeadword);
}
```

这些方法体来自 `WordService` 当前已经通过测试的实现，不改变 trim、Unicode 规范化、排序或音频 ID 映射。将 `WordService.CreateAsync` 改为：

```csharp
var word = WordAggregateBuilder.Create(request);
await EnsureHeadwordUniqueAsync(word.NormalizedHeadword, null, cancellationToken);
_db.Words.Add(word);
await SaveWordChangesAsync(cancellationToken);
```

更新流程中的新释义、新例句和字段应用也改用构建器公开的内部方法，删除 `WordService` 中对应重复私有方法和 `WordIdentity`。

- [ ] **Step 4：运行聚合与现有单词服务测试确认 GREEN。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordAggregateBuilderTests|FullyQualifiedName~WordServiceTests"
```

Expected: PASS，单条创建、更新、例句音频和数据库异常映射行为不变。

- [ ] **Step 5：提交 Task 1。**

```bash
git add server/TinyLang/Services/WordAggregateBuilder.cs server/TinyLang/Services/WordService.cs server/TinyLang.UnitTests/WordAggregateBuilderTests.cs
git commit -m "refactor(server): extract word aggregate builder"
```

---

## Task 2：建立批量 JSON 和响应契约

**Files:**

- Create: `server/TinyLang/Dtos/WordBatchDtos.cs`
- Modify: `server/TinyLang/Dtos/WordDtos.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Create: `server/TinyLang.UnitTests/WordBatchContractTests.cs`
- Modify: `server/TinyLang.UnitTests/HttpJsonContractTests.cs`

- [ ] **Step 1：编写批量契约和 JSON 反序列化测试。**

测试以下请求可以反序列化，并保留字符串词性和文件名：

```csharp
const string json = """
{
  "words": [{
    "headword": "hello",
    "audioFileName": "hello.mp3",
    "senses": [{
      "partOfSpeech": "Interjection",
      "definition": "你好",
      "usageNote": null,
      "sortOrder": 0,
      "examples": [{
        "sentence": "Hello there.",
        "translation": "你好。",
        "audioFileName": "hello-example.mp3",
        "sortOrder": 0
      }]
    }]
  }]
}
""";

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var request = JsonSerializer.Deserialize<BatchWordRequest>(json, options)!;
request.Words.Single().Senses.Single().PartOfSpeech.Should()
    .Be("Interjection");
```

同时用反射断言批量输入不包含 `Id`、`AudioResourceId`、`ConcurrencyStamp` 或状态字段；断言 `MaxBatchWordCount == 1000`、`MaxBatchRequestBodyBytes == 20 * 1024 * 1024`。

- [ ] **Step 2：运行契约测试确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchContractTests|FullyQualifiedName~HttpJsonContractTests"
```

Expected: FAIL，批量 DTO、限制和错误码尚不存在。

- [ ] **Step 3：定义批量请求和响应类型。**

在 `WordBatchDtos.cs` 定义：

```csharp
public sealed record BatchWordRequest
{
    public IReadOnlyCollection<BatchWordRowRequest> Words { get; init; } = [];
}

public sealed record BatchWordRowRequest
{
    public string? Headword { get; init; }
    public string? AudioFileName { get; init; }
    public IReadOnlyCollection<BatchWordSenseInput> Senses { get; init; } = [];
}

public sealed record BatchWordSenseInput
{
    [AllowedValues(
        "Noun", "Verb", "Adjective", "Adverb", "Pronoun", "Determiner",
        "Preposition", "Conjunction", "Interjection", "Numeral", "Particle",
        "Other")]
    public string? PartOfSpeech { get; init; }
    public string? Definition { get; init; }
    public string? UsageNote { get; init; }
    public int SortOrder { get; init; }
    public IReadOnlyCollection<BatchExampleSentenceInput> Examples { get; init; } = [];
}

public sealed record BatchExampleSentenceInput
{
    public string? Sentence { get; init; }
    public string? Translation { get; init; }
    public string? AudioFileName { get; init; }
    public int SortOrder { get; init; }
}
```

`WordBatchDtos.cs` 引入 `System.ComponentModel.DataAnnotations`，`AllowedValues` 只用于生成清晰的 OpenAPI 允许值；服务层仍自行逐行解析，不能依赖 DataAnnotations 在模型绑定阶段拒绝整个请求。

响应类型固定为：

```csharp
public sealed record BatchWordSummaryResponse(
    int WordCount,
    int SenseCount,
    int ExampleCount,
    int WordAudioReferenceCount,
    int ExampleAudioReferenceCount,
    int MatchedAudioReferenceCount);

public sealed record BatchWordRowValidationResponse(
    int RowNumber,
    string? Headword,
    string? NormalizedHeadword,
    string? WordAudioName,
    int SenseCount,
    int ExampleCount,
    int AudioReferenceCount,
    int MatchedAudioCount);

public sealed record BatchWordValidationErrorResponse(
    int? RowNumber,
    string Field,
    ErrorCodes ErrorCode,
    string Message);

public sealed record BatchWordValidationResponse(
    bool IsValid,
    BatchWordSummaryResponse Summary,
    IReadOnlyList<BatchWordRowValidationResponse> Rows,
    IReadOnlyList<BatchWordValidationErrorResponse> Errors);

public sealed record BatchWordCreatedItemResponse(int RowNumber, Guid WordId);
public sealed record BatchWordImportResponse(
    int CreatedCount,
    IReadOnlyList<BatchWordCreatedItemResponse> Items);
```

在 `WordConstraints` 增加明确上限：

```csharp
public const int MaxBatchWordCount = 1_000;
public const int MaxBatchSenseCount = 10_000;
public const int MaxBatchExampleCount = 50_000;
public const int MaxBatchTextCharacterCount = 10_000_000;
public const long MaxBatchRequestBodyBytes = 20L * 1024 * 1024;
```

新增错误码：`WordBatchRequired`、`WordBatchCountLimit`、`WordBatchChildCountLimit`、`WordBatchTextLengthLimit`、`WordBatchAudioNotFound`、`WordBatchAudioFailed`、`WordBatchConflict`，每项提供明确中文 `Description`。

- [ ] **Step 4：运行契约测试确认 GREEN。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchContractTests|FullyQualifiedName~HttpJsonContractTests"
```

Expected: PASS，未知词性仍能作为字符串进入服务层逐行校验，枚举数字 JSON 无法绑定为字符串。

- [ ] **Step 5：提交 Task 2。**

```bash
git add server/TinyLang/Dtos/WordBatchDtos.cs server/TinyLang/Dtos/WordDtos.cs server/TinyLang/Exceptions/ErrorCodes.cs server/TinyLang.UnitTests/WordBatchContractTests.cs server/TinyLang.UnitTests/HttpJsonContractTests.cs
git commit -m "feat(server): add word batch contracts"
```

---

## Task 3：实现完整批量校验和音频名称解析

**Files:**

- Create: `server/TinyLang/Services/IWordBatchService.cs`
- Create: `server/TinyLang/Services/WordBatchService.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Create: `server/TinyLang.UnitTests/WordBatchServiceTests.cs`

- [ ] **Step 1：编写结构、重复和音频规则测试。**

在 `WordBatchServiceTests` 使用 InMemory `ApplicationDbContext` 覆盖：

```csharp
[Fact]
public async Task ValidateShouldResolveNormalizedAudioNamesAndCollectAllErrors()
{
    // 数据库准备 hello.mp3(Ready)、pending.mp3(Processing)、failed.mp3(Failed)
    // 请求同时包含：大小写不同的合法文件名、不存在文件名、Failed 文件名、
    // 批内重复词头、数据库已有词头和未知 partOfSpeech。
    var result = await service.ValidateAsync(request, cancellationToken);

    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(error =>
        error.Field == "words[1].audioFileName" &&
        error.ErrorCode == ErrorCodes.WordBatchAudioNotFound);
    result.Errors.Should().Contain(error =>
        error.ErrorCode == ErrorCodes.WordBatchAudioFailed);
    result.Errors.Should().Contain(error =>
        error.ErrorCode == ErrorCodes.WordDuplicate);
    result.Errors.Should().Contain(error =>
        error.ErrorCode == ErrorCodes.WordPartOfSpeechInvalid);
}
```

再分别测试：

- `words` 为 `null`、空数组、1001 项。
- 总释义、总例句和总文本量超限。
- 音频字段省略、`null`、`""` 和纯空白均表示无关联。
- 名称匹配忽略大小写和首尾空格。
- `Uploading`、`Queued`、`Processing`、`Ready` 可以关联，`Failed` 不可关联。
- 同一音频被多个字段引用时每个引用都计入统计，但只执行集合查询。
- 校验接口不向 DbContext 添加 `Word`。
- 错误路径使用 `words[index]...`，`rowNumber` 从 1 开始。

- [ ] **Step 2：运行批量服务测试确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchServiceTests"
```

Expected: FAIL，服务接口和实现尚不存在。

- [ ] **Step 3：实现服务接口和内部校验结果。**

接口先暴露校验用例：

```csharp
public interface IWordBatchService
{
    Task<BatchWordValidationResponse> ValidateAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken = default);
}
```

`WordBatchService` 注入 `IApplicationDbContext`、`IValidator<CreateWordRequest>`、`IDatabaseExceptionClassifier` 和 logger。内部使用：

```csharp
private sealed record NormalizedBatchRow(
    int RowNumber,
    CreateWordRequest Request,
    WordAggregateBuilder.WordIdentity Identity);

private sealed record BatchValidationResult(
    BatchWordValidationResponse Response,
    IReadOnlyList<NormalizedBatchRow> Rows);
```

`ValidateAsync` 只返回 `BuildValidationAsync(...).Response`，不执行 `Add` 或 `SaveChangesAsync`。

- [ ] **Step 4：实现有界校验和单条规则复用。**

`BuildValidationAsync` 必须：

1. 先用 `long` 累加根集合、释义、例句和文本长度，避免整数溢出。
2. 对每个批量行安全转换为 `CreateWordRequest`；`partOfSpeech` 使用 `Enum.TryParse(value, ignoreCase: false, out PartOfSpeech parsed)`。
3. 使用注入的 `IValidator<CreateWordRequest>` 获取现有字段错误，并把 `Senses[0].Examples[1].Sentence` 转为 `words[0].senses[0].examples[1].sentence`。
4. 使用 `WordAggregateBuilder.NormalizeIdentity` 生成展示词头和规范化键。
5. 对规范化键分组，给批内每个重复行添加 `WordDuplicate`。
6. 一次查询数据库已有规范化词头，并给对应行添加 `WordDuplicate`。

错误创建统一使用：

```csharp
private static BatchWordValidationErrorResponse Error(
    int? rowNumber,
    string field,
    ErrorCodes code)
    => new(rowNumber, field, code, code.GetDescription());
```

- [ ] **Step 5：实现音频文件名集合解析。**

收集所有非空文件名并调用 `AudioResource.NormalizeName`，随后只执行一次查询：

```csharp
var audioByName = await _db.AudioResources.AsNoTracking()
    .Where(audio => normalizedNames.Contains(audio.NormalizedName))
    .Select(audio => new
    {
        audio.Id,
        audio.Name,
        audio.NormalizedName,
        audio.Status
    })
    .ToDictionaryAsync(audio => audio.NormalizedName, cancellationToken);
```

不存在时添加 `WordBatchAudioNotFound`；`Failed` 时添加 `WordBatchAudioFailed`；其他状态将解析到的 ID 写入内部 `CreateWordRequest.AudioResourceId` 或 `ExampleSentenceInput.AudioResourceId`。返回预览时只暴露最终名称、数量和匹配统计，不暴露对象存储路径。

- [ ] **Step 6：注册服务并运行测试确认 GREEN。**

在 `AddBusinessServices` 增加：

```csharp
services.AddScoped<IWordBatchService, WordBatchService>();
```

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchServiceTests|FullyQualifiedName~WordValidatorsTests"
```

Expected: PASS，所有规则在一次响应中返回，数据库中没有新增单词。

- [ ] **Step 7：提交 Task 3。**

```bash
git add server/TinyLang/Services/IWordBatchService.cs server/TinyLang/Services/WordBatchService.cs server/TinyLang/Services/DependencyInjection.cs server/TinyLang.UnitTests/WordBatchServiceTests.cs
git commit -m "feat(server): validate word batch imports"
```

---

## Task 4：实现原子导入 endpoints 和竞态处理

**Files:**

- Modify: `server/TinyLang/Services/IWordBatchService.cs`
- Modify: `server/TinyLang/Services/WordBatchService.cs`
- Modify: `server/TinyLang/Endpoints/WordEndpoints.cs`
- Modify: `server/TinyLang.UnitTests/WordBatchServiceTests.cs`
- Modify: `server/TinyLang.UnitTests/WordEndpointTests.cs`
- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs`

- [ ] **Step 1：先编写原子导入和回滚测试。**

新增测试：

```csharp
[Fact]
public async Task ImportShouldCreateEveryAggregateInOneTransaction()
{
    var result = await service.ImportAsync(admin.Id, ValidBatch(2), cancellationToken);

    result.Imported.Should().NotBeNull();
    result.Validation.Should().BeNull();
    result.Imported!.CreatedCount.Should().Be(2);
    (await db.Words.CountAsync(cancellationToken)).Should().Be(2);
    (await db.WordSenses.CountAsync(cancellationToken)).Should().Be(2);
    (await db.ExampleSentences.CountAsync(cancellationToken)).Should().Be(2);
}
```

再测试：

- 任意行无效时 `Imported == null` 且数据库零写入。
- `SaveChangesAsync` 抛出目标单词唯一约束后，清理 tracked 状态、重新校验并返回冲突行。
- 单词音频或例句音频外键竞态后，重新校验返回音频不存在错误。
- 非目标 `DbUpdateException` 原样抛出。
- `CommitAsync` 只在保存成功后调用一次。

- [ ] **Step 2：编写 endpoint 与 OpenAPI RED 测试。**

将 `WordEndpointTests.RetiredBatchEndpointsShouldReturnNotFound` 替换为：

- 两条路径必须存在并要求管理员策略。
- validate 调用 `IWordBatchService.ValidateAsync` 并返回 `200`。
- import 成功返回 `200`。
- import 校验失败返回 `422` 和 `BatchWordValidationResponse`。
- 两个 endpoint 都具有 `RequestSizeLimitMetadata.MaxRequestBodySize == WordConstraints.MaxBatchRequestBodyBytes`。

OpenAPI 断言两个 path、请求/响应 schema、字符串 `partOfSpeech` 和 `422` 响应存在。
更新 `OpenApiContractTests.CreateAppAsync`，同时注册 `Mock.Of<IWordBatchService>()`；更新 `WordEndpointTests` 的 metadata/HTTP 测试应用，使 `IWordService` 与 `IWordBatchService` 分别注入，避免批量用例重新塞回单条服务 mock。

- [ ] **Step 3：运行服务和 endpoint 测试确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchServiceTests|FullyQualifiedName~WordEndpointTests|FullyQualifiedName~OpenApiContractTests"
```

Expected: FAIL，导入成员和 routes 尚不存在，旧 404 断言不再成立。

- [ ] **Step 4：实现导入结果和事务。**

定义服务层结果：

```csharp
public sealed record WordBatchImportResult(
    BatchWordImportResponse? Imported,
    BatchWordValidationResponse? Validation);
```

接口增加：

```csharp
Task<WordBatchImportResult> ImportAsync(
    Guid adminId,
    BatchWordRequest request,
    CancellationToken cancellationToken = default);
```

导入流程固定为：

```csharp
var validation = await BuildValidationAsync(request, cancellationToken);
if (!validation.Response.IsValid)
    return new(null, validation.Response);

try
{
    await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
    var words = validation.Rows.Select(row =>
        (row.RowNumber, Word: WordAggregateBuilder.Create(row.Request))).ToArray();
    _db.Words.AddRange(words.Select(item => item.Word));
    await _db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new(
        new BatchWordImportResponse(
            words.Length,
            words.Select(item => new BatchWordCreatedItemResponse(
                item.RowNumber,
                item.Word.Id)).ToArray()),
        null);
}
catch (DbUpdateException exception) when (IsBatchRace(exception))
{
    _db.ClearTrackedChanges();
    var refreshed = await BuildValidationAsync(request, cancellationToken);
    if (refreshed.Response.IsValid)
    {
        var errors = refreshed.Response.Errors.Append(Error(
            null,
            "words",
            ErrorCodes.WordBatchConflict)).ToArray();
        return new(null, refreshed.Response with
        {
            IsValid = false,
            Errors = errors
        });
    }
    return new(null, refreshed.Response);
}
```

`IsBatchRace` 只识别三个明确约束：

```csharp
private bool IsBatchRace(DbUpdateException exception)
    => _databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "IX_words_NormalizedHeadword") ||
        _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
            exception,
            "FK_words_audio_resources_AudioResourceId") ||
        _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
            exception,
            "FK_example_sentences_audio_resources_AudioResourceId");
```

命中目标约束时清除 tracked 状态并重新运行 `BuildValidationAsync`；若重新校验仍未定位错误，追加根级 `WordBatchConflict`。其他异常继续抛出。

- [ ] **Step 5：映射两个管理员 endpoints。**

新增：

```csharp
adminGroup.MapPost("/words/batch/validate", ValidateWordBatchAsync);
adminGroup.MapPost("/words/batch", ImportWordBatchAsync);
```

处理器签名：

```csharp
[RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
public static async Task<Ok<BatchWordValidationResponse>> ValidateWordBatchAsync(
    BatchWordRequest request,
    IWordBatchService service,
    CancellationToken cancellationToken)
    => TypedResults.Ok(await service.ValidateAsync(request, cancellationToken));

[RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
public static async Task<Results<Ok<BatchWordImportResponse>,
    UnprocessableEntity<BatchWordValidationResponse>>> ImportWordBatchAsync(
    BatchWordRequest request,
    ClaimsPrincipal principal,
    IWordBatchService service,
    CancellationToken cancellationToken)
{
    var result = await service.ImportAsync(
        EndpointIdentity.GetUserId(principal), request, cancellationToken);
    return result.Imported is { } imported
        ? TypedResults.Ok(imported)
        : TypedResults.UnprocessableEntity(result.Validation!);
}
```

- [ ] **Step 6：运行 focused server tests 和 build。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordBatchServiceTests|FullyQualifiedName~WordEndpointTests|FullyQualifiedName~OpenApiContractTests"
dotnet build server/TinyLang/TinyLang.csproj
```

Expected: PASS；OpenAPI 暴露两个管理员接口；不产生 migration。

- [ ] **Step 7：提交 Task 4。**

```bash
git add server/TinyLang/Services/IWordBatchService.cs server/TinyLang/Services/WordBatchService.cs server/TinyLang/Endpoints/WordEndpoints.cs server/TinyLang.UnitTests/WordBatchServiceTests.cs server/TinyLang.UnitTests/WordEndpointTests.cs server/TinyLang.UnitTests/OpenApiContractTests.cs
git commit -m "feat(server): import word batches atomically"
```

---

## Task 5：建立管理端批量 API 和 422 数据契约

**Files:**

- Modify: `admin/src/services/problemDetails.js`
- Modify: `admin/src/services/httpTransport.test.js`
- Create: `admin/src/services/wordBatchContracts.js`
- Modify: `admin/src/services/wordsApi.js`
- Modify: `admin/src/services/wordsApi.test.js`

- [ ] **Step 1：编写非 2xx 原始 JSON 保留测试。**

在 `httpTransport.test.js` 增加 `422` 用例：

```js
await expect(requestApi({ path: "/admin/words/batch", method: "POST" }))
  .rejects.toMatchObject({
    status: 422,
    data: { isValid: false, errors: [] },
  });
```

该 `data` 只能保存已解析 JSON 对象，不改变 `detail`、`errorCode` 和 `fieldErrors` 的现有安全归一化。

- [ ] **Step 2：编写批量 normalizer 和 RTK mutation 测试。**

测试合法响应可归一化，以下情况产生 `CUSTOM_ERROR`：

- `rowNumber` 小于 1。
- 未知错误码或未知音频状态。
- 统计为负数或非整数。
- `createdCount` 与 `items.length` 不一致。
- `422` 正文不是合法 `BatchWordValidationResponse`。

更新旧断言：`validateWordBatch`、`importWordBatch` 现在必须存在，其余已退役生命周期和旧音频 endpoint 仍必须不存在。

- [ ] **Step 3：运行 focused admin service tests 确认 RED。**

Run:

```bash
pnpm --dir admin test -- src/services/httpTransport.test.js src/services/wordsApi.test.js
```

Expected: FAIL，错误对象没有 `data`，批量 normalizer 和 mutations 尚不存在。

- [ ] **Step 4：扩展安全错误形状。**

`ApiError` 和 RTK 错误增加可选 `data`：

```js
export class ApiError extends Error {
  constructor(message, options = {}) {
    super(message);
    // 保留现有字段
    this.data = options.data ?? null;
  }
}
```

`createResponseError` 只在 `response.data` 是非数组对象时保存它；`toRtkQueryError` 返回 `data: apiError.data`。不得在日志或错误消息中字符串化整个响应。

- [ ] **Step 5：实现批量响应 normalizer。**

在 `wordBatchContracts.js` 导出：

```js
export function normalizeWordBatchValidation(value) {
  const source = object(value, "word batch validation");
  const summarySource = object(source.summary, "word batch summary");
  return {
    isValid: boolean(source.isValid, "word batch validation state"),
    summary: {
      wordCount: integer(summarySource.wordCount, "word batch word count"),
      senseCount: integer(summarySource.senseCount, "word batch sense count"),
      exampleCount: integer(
        summarySource.exampleCount,
        "word batch example count",
      ),
      wordAudioReferenceCount: integer(
        summarySource.wordAudioReferenceCount,
        "word batch word audio reference count",
      ),
      exampleAudioReferenceCount: integer(
        summarySource.exampleAudioReferenceCount,
        "word batch example audio reference count",
      ),
      matchedAudioReferenceCount: integer(
        summarySource.matchedAudioReferenceCount,
        "word batch matched audio reference count",
      ),
    },
    rows: array(source.rows, normalizeRow, "word batch rows"),
    errors: array(source.errors, normalizeError, "word batch errors"),
  };
}

export function normalizeWordBatchImport(value) {
  const source = object(value, "word batch import");
  const items = array(
    source.items,
    (item) => {
      const created = object(item, "word batch created item");
      return {
        rowNumber: positiveInteger(
          created.rowNumber,
          "word batch created row number",
        ),
        wordId: uuid(created.wordId, "word batch created word id"),
      };
    },
    "word batch created items",
  );
  const createdCount = integer(source.createdCount, "word batch created count");
  if (createdCount !== items.length) invalid("word batch created count");
  return { createdCount, items };
}
```

同文件定义 `object`、`array`、`string`、`boolean`、`integer`、`positiveInteger`、`uuid` 和 `invalid`，行为与现有 `wordContracts.js` 的安全 helper 一致。`normalizeRow` 严格读取 `rowNumber`、可空 `headword`、可空 `normalizedHeadword`、可空 `wordAudioName`、`senseCount`、`exampleCount`、`audioReferenceCount` 和 `matchedAudioCount`；`normalizeError` 严格读取可空正整数 `rowNumber`、非空 `field`、允许集合内的 `errorCode` 和非空 `message`。

`WORD_BATCH_ERROR_CODES` 明确列出：`WordBatchRequired`、`WordBatchCountLimit`、`WordBatchChildCountLimit`、`WordBatchTextLengthLimit`、`WordBatchAudioNotFound`、`WordBatchAudioFailed`、`WordBatchConflict`、`WordHeadwordRequired`、`WordHeadwordLengthLimit`、`WordPartOfSpeechInvalid`、`WordDefinitionRequired`、`WordDefinitionLengthLimit`、`WordUsageNoteLengthLimit`、`WordSentenceRequired`、`WordSentenceLengthLimit`、`WordTranslationRequired`、`WordTranslationLengthLimit`、`WordSenseRequired`、`WordChildCollectionInvalid`、`WordChildCountLimit`、`WordSortOrderInvalid`、`WordSortOrderConflict` 和 `WordDuplicate`。未知错误码按契约错误处理。

- [ ] **Step 6：增加 RTK Query mutations。**

```js
validateWordBatch: builder.mutation({
  queryFn: normalizedQuery(
    (body) => ({ url: "/admin/words/batch/validate", method: "POST", body }),
    normalizeWordBatchValidation,
  ),
}),
importWordBatch: builder.mutation({
  async queryFn(body, _api, _extraOptions, baseQuery) {
    const result = await baseQuery({
      url: "/admin/words/batch",
      method: "POST",
      body,
    });
    if (result.error?.status === 422) {
      try {
        return {
          error: {
            ...result.error,
            data: normalizeWordBatchValidation(result.error.data),
          },
        };
      } catch {
        return { error: CONTRACT_ERROR };
      }
    }
    if (result.error) return result;
    try {
      return { data: normalizeWordBatchImport(result.data) };
    } catch {
      return { error: CONTRACT_ERROR };
    }
  },
  invalidatesTags: (_result, error) =>
    error ? [] : [{ type: "Word", id: "LIST" }],
}),
```

导出 `useValidateWordBatchMutation` 和 `useImportWordBatchMutation`。

- [ ] **Step 7：运行 tests 和 lint，提交 Task 5。**

Run:

```bash
pnpm --dir admin test -- src/services/httpTransport.test.js src/services/wordsApi.test.js
pnpm --dir admin lint
```

Expected: PASS。

```bash
git add admin/src/services/problemDetails.js admin/src/services/httpTransport.test.js admin/src/services/wordBatchContracts.js admin/src/services/wordsApi.js admin/src/services/wordsApi.test.js
git commit -m "feat(admin): add word batch api contracts"
```

---

## Task 6：抽取可并发复用的音频上传 runner

**Files:**

- Create: `admin/src/features/audio/useAudioUploadRunner.js`
- Modify: `admin/src/features/audio/AudioUploadControl.jsx`
- Modify: `admin/src/features/audio/AudioUploadControl.test.jsx`

- [ ] **Step 1：补充单文件上传回归测试。**

在现有测试中明确断言：

- 简单上传按“初始化 -> OSS PUT -> confirm -> detail polling”顺序执行。
- 分片失败会调用 abort multipart。
- 外部取消只取消当前调用。
- `waitForProcessing=false` 在 confirm 后只读取一次详情。
- `onStarted` 和 `onCompleted` 参数保持现有契约。

- [ ] **Step 2：运行现有控件测试建立 GREEN 基线。**

Run:

```bash
pnpm --dir admin test -- src/features/audio/AudioUploadControl.test.jsx
```

Expected: PASS；若新增回归断言暴露当前缺口，先只修测试夹具，不改变行为。

- [ ] **Step 3：实现 runner hook。**

`useAudioUploadRunner` 返回 capability 状态和一个可并发调用的函数：

```js
const {
  capability,
  capabilityError,
  capabilityLoading,
  refetchCapability,
  uploadAudio,
} = useAudioUploadRunner();

const audio = await uploadAudio({
  file,
  resource,
  waitForProcessing,
  signal,
  onStarted,
  onProgress,
  onStage,
});
```

每次 `uploadAudio` 调用的当前 RTK request、multipart session 和进度回调必须存放在函数局部变量中，不能放在 hook 共享 ref 中，否则两个文件并发时会互相覆盖。取消时只 abort 当前调用；发生分片错误时只清理当前 session。

从 `AudioUploadControl` 搬移并导出纯函数 `validateAudioFile`、`formatAudioFileSize`；runner 内复用当前 `MAX_CONCURRENT_PARTS = 3`、轮询间隔和最大轮询次数。

- [ ] **Step 4：简化单文件 UI 并运行回归。**

`AudioUploadControl` 只保留文件选择和组件状态，`handleUpload` 调用 runner：

```js
const audio = await uploadAudio({
  file,
  resource,
  waitForProcessing,
  signal: controller.signal,
  onStarted: (id) => onStarted?.(id, file.name),
  onProgress: setProgress,
  onStage: transitionTo,
});
finishUpload(audio);
```

Run:

```bash
pnpm --dir admin test -- src/features/audio/AudioUploadControl.test.jsx
pnpm --dir admin lint
```

Expected: PASS，单文件 UI 和上传协议无行为变化。

- [ ] **Step 5：提交 Task 6。**

```bash
git add admin/src/features/audio/useAudioUploadRunner.js admin/src/features/audio/AudioUploadControl.jsx admin/src/features/audio/AudioUploadControl.test.jsx
git commit -m "refactor(admin): extract audio upload runner"
```

---

## Task 7：实现音频库多文件上传

**Files:**

- Create: `admin/src/features/audio/AudioBatchUploadControl.jsx`
- Create: `admin/src/features/audio/AudioBatchUploadControl.test.jsx`
- Modify: `admin/src/pages/AudioLibrary.jsx`
- Modify: `admin/src/pages/AudioLibrary.test.jsx`

- [ ] **Step 1：编写多选、并发和部分失败测试。**

测试场景：选择 3 个 MP3，前两个初始化请求保持 pending，断言第三个尚未初始化；释放一个请求后第三个开始，证明文件并发上限为 2。

再覆盖：

- 文件 input 具有 `multiple` 和 capability 派生的 `accept`。
- 非法文件逐项标记失败，不阻止合法文件。
- 一个上传失败，其他文件继续完成。
- 成功项展示 `hello.mp3 -> hello (2).mp3`。
- 初始化后失败的条目重试时使用现有 `audioResourceId` 调用 retry-upload。
- 取消一个条目不取消其他条目。
- 完成后刷新音频列表，但结果列表保持可见。

- [ ] **Step 2：运行新控件测试确认 RED。**

Run:

```bash
pnpm --dir admin test -- src/features/audio/AudioBatchUploadControl.test.jsx src/pages/AudioLibrary.test.jsx
```

Expected: FAIL，批量控件不存在，音频库仍渲染单文件新建控件。

- [ ] **Step 3：实现有界文件队列。**

条目形状固定为：

```js
{
  id,
  file,
  stage: "waiting",
  progress: 0,
  audioResourceId: null,
  finalName: null,
  error: null,
  controller: null,
}
```

使用 `MAX_CONCURRENT_FILES = 2` 的 worker pool。条目 ID 使用文件名、大小、lastModified 和本次选择序号组合，不能只用文件名。每个 worker 创建独立 `AbortController`，调用 Task 6 的 `uploadAudio` 并仅更新对应条目。

失败重试规则：已有 `audioResourceId` 时传入 `{ id: audioResourceId }` 触发现有 retry-upload；初始化前失败则创建新资源。处理结果为 `Failed` 时保留最终名称和资源 ID，并允许用原文件重试上传。

- [ ] **Step 4：接入音频库。**

用 `AudioBatchUploadControl` 替换音频库页面用于“新建资源”的 `AudioUploadControl`；重新上传已有失败资源的 Dialog 继续使用单文件控件。`onStarted` 和每个终态触发 `refetch`，但批量控件内部条目不随父列表刷新重置。

- [ ] **Step 5：运行 focused tests、lint 和 build。**

Run:

```bash
pnpm --dir admin test -- src/features/audio/AudioBatchUploadControl.test.jsx src/features/audio/AudioUploadControl.test.jsx src/pages/AudioLibrary.test.jsx
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS，三个文件中最多两个同时上传，部分失败不影响成功项。

- [ ] **Step 6：提交 Task 7。**

```bash
git add admin/src/features/audio/AudioBatchUploadControl.jsx admin/src/features/audio/AudioBatchUploadControl.test.jsx admin/src/pages/AudioLibrary.jsx admin/src/pages/AudioLibrary.test.jsx
git commit -m "feat(admin): support batch audio uploads"
```

---

## Task 8：实现单词 JSON 文件预检和导入页面

**Files:**

- Create: `admin/src/features/words/wordBatchFile.js`
- Create: `admin/src/features/words/wordBatchFile.test.js`
- Create: `admin/src/pages/WordBatchImport.jsx`
- Create: `admin/src/pages/WordBatchImport.test.jsx`
- Modify: `admin/src/pages/Words.jsx`
- Modify: `admin/src/pages/Words.test.jsx`
- Modify: `admin/src/router/index.jsx`
- Modify: `admin/src/router/router.test.jsx`
- Modify: `admin/src/features/words/WordUnsavedChangesDialog.jsx`

- [ ] **Step 1：编写文件预检纯函数测试。**

`readWordBatchFile(file)` 必须：

- 拒绝非 `.json` 扩展名。
- 拒绝空文件和超过 `20 * 1024 * 1024` 字节的文件。
- 捕获 `JSON.parse` 错误并返回“JSON 语法无效，请检查括号、引号和逗号。”。
- 拒绝非对象根节点和缺少 `words` 数组。
- 返回原始对象，不在前端复制词性、字段长度或音频业务校验。

示例常量必须包含单词音频、例句音频、`Interjection` 和可空 `usageNote`。

- [ ] **Step 2：编写页面 RED 测试。**

覆盖：

- `/words` 显示“批量导入”入口。
- `/words/batch` 不再是 404。
- 选择合法文件后调用 `/admin/words/batch/validate`。
- 显示 summary、逐行预览、`rowNumber`、字段路径和错误消息。
- `isValid=false` 时确认按钮禁用。
- `isValid=true` 时确认调用 `/admin/words/batch`，提交期间文件和按钮禁用。
- `422` 时使用 `error.data` 替换当前校验结果并保留文件。
- 成功后跳转 `/words` 并显示“已批量创建 N 个单词”。
- 示例下载链接包含 `download="tiny-lang-word-import-example.json"`。

- [ ] **Step 3：运行文件和页面测试确认 RED。**

Run:

```bash
pnpm --dir admin test -- src/features/words/wordBatchFile.test.js src/pages/WordBatchImport.test.jsx src/pages/Words.test.jsx src/router/router.test.jsx
```

Expected: FAIL，文件 helper、页面、入口和 route 尚不存在。

- [ ] **Step 4：实现文件 helper 和示例下载。**

```js
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
export const WORD_BATCH_EXAMPLE_URL =
  `data:application/json;charset=utf-8,${encodeURIComponent(WORD_BATCH_EXAMPLE_TEXT)}`;

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
```

示例数据必须与后端 DTO 字段逐项一致，不能包含 ID、状态或 `audioResourceId`。

- [ ] **Step 5：实现两阶段导入页面。**

页面状态至少包括：

```js
const [file, setFile] = useState(null);
const [payload, setPayload] = useState(null);
const [validation, setValidation] = useState(null);
const [fileError, setFileError] = useState(null);
const [requestError, setRequestError] = useState(null);
```

选择文件后读取并自动调用 validate。任何新文件都清除旧 validation。确认条件为：

```js
const canImport = Boolean(
  payload && validation?.isValid && !validateState.isLoading &&
  !importState.isLoading,
);
```

导入捕获 `error.status === 422 && error.data` 时执行：

```js
setValidation(error.data);
setRequestError(null);
```

其他网络或权限错误通过 `getErrorMessage` 显示。页面不提供 JSON 编辑器。使用 `useUnsavedChanges(Boolean(payload) && !completed, allowNavigationRef)` 防止误离开；将 `WordUnsavedChangesDialog` 扩展为可传入 `title` 和 `description`，单词编辑器默认文案保持不变。

- [ ] **Step 6：接入入口、路由和成功通知。**

`Words.jsx` 增加带 `FileJson` 图标的 `/words/batch` 链接。路由顺序必须把静态 `words/batch` 放在动态 `words/:wordId` 之前。

成功后：

```js
allowNavigationRef.current = true;
navigate("/words", {
  replace: true,
  state: { notice: `已批量创建 ${result.createdCount} 个单词。` },
});
```

`Words` 读取一次 `location.state?.notice`，显示现有 notice Alert，并立即用 replace 清除 history state，防止刷新重复显示。

- [ ] **Step 7：运行 focused tests、lint 和 build。**

Run:

```bash
pnpm --dir admin test -- src/features/words/wordBatchFile.test.js src/pages/WordBatchImport.test.jsx src/pages/Words.test.jsx src/router/router.test.jsx src/services/wordsApi.test.js
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS，管理员可以预览合法/非法批次，只有全量通过后才能确认导入。

- [ ] **Step 8：提交 Task 8。**

```bash
git add admin/src/features/words/wordBatchFile.js admin/src/features/words/wordBatchFile.test.js admin/src/pages/WordBatchImport.jsx admin/src/pages/WordBatchImport.test.jsx admin/src/pages/Words.jsx admin/src/pages/Words.test.jsx admin/src/router/index.jsx admin/src/router/router.test.jsx admin/src/features/words/WordUnsavedChangesDialog.jsx
git commit -m "feat(admin): add word batch import workflow"
```

---

## Task 9：完成跨模块验证和遗留扫描

**Files:**

- Verify: `server/TinyLang`
- Verify: `server/TinyLang.UnitTests`
- Verify: `admin/src`
- Verify: Git staging area
- Modify: 仅限验证发现的本功能缺陷；仍不得修改或提交 `docs/`

- [ ] **Step 1：扫描旧契约和意外 schema 变化。**

Run:

```bash
rg -n "audioClipId|pronunciations|BatchId|WordBatchImport.*404|RetiredBatchEndpoints" server/TinyLang server/TinyLang.UnitTests admin/src
rg -n "audioResourceId" server/TinyLang/Dtos/WordBatchDtos.cs admin/src/features/words/wordBatchFile.js
git status --short
```

Expected:

- 新批量 JSON 不包含旧 `audioClipId`、`pronunciations` 或无业务用途的 `BatchId`。
- 批量输入不暴露 `audioResourceId`。
- 旧 404 测试已被新 endpoint 测试替换。
- 没有 migration 文件。

- [ ] **Step 2：运行完整后端验证。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj
```

Expected: 全部测试通过，build exit code 0。

- [ ] **Step 3：运行完整管理端验证。**

Run:

```bash
pnpm --dir admin lint
pnpm --dir admin test
pnpm --dir admin build
```

Expected: lint、Vitest 和生产构建全部 exit code 0。

- [ ] **Step 4：核对提交边界。**

Run:

```bash
git diff --check
git status --short
git diff --cached --name-only
```

Expected: staging area 为空；`docs/` 和 `word.md` 可以继续保持用户原有未跟踪状态，但不出现在任何实现 commit 中。

- [ ] **Step 5：仅在验证修复产生新 diff 时提交。**

先运行 `git diff --name-only`，逐个检查并显式 `git add` 列出的非 `docs` 修复文件，然后执行 `git commit -m "test: complete word batch import verification"`。若完整验证未产生新 diff，则不创建空提交。

---

## 最终验收

1. 音频库支持一次选择多个文件，并且最多两个文件同时执行完整上传流程。
2. 每个音频文件独立显示状态、进度、失败原因、重试入口和最终资源名称。
3. JSON 使用 `words` 根数组、字符串词性和最终音频文件名，最多 1000 个单词、20 MB。
4. 校验接口返回完整统计、预览和逐行错误，且不产生数据库写入。
5. 导入接口重新校验并在单个事务中创建全部单词；任意错误或保存失败时零部分数据。
6. 音频名称不存在或状态为 `Failed` 时整批失败；其他现有音频状态可以关联。
7. 与已有单词或批内规范化重复时整批失败，不跳过、不覆盖。
8. `/app`、数据库 schema 和 migration 保持不变。
9. 每个实现 Task 均形成独立业务 commit，所有 commit 都排除 `docs/` 和 `word.md`。
