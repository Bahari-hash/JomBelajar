# 例句共享音频支持 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为每条例句增加一个可空的共享音频关联，使管理员能够从音频库选择或本地上传，登录用户能够在单词学习卡片中播放例句音频。

**Architecture:** 在 `ExampleSentence` 上增加单向可空 `AudioResourceId` Restrict 外键，继续通过 `WordService` 的完整目标集合写入例句，并批量验证请求中的例句音频资源。管理端新增例句专用组合控件，用户端复用共享 `AudioPlaybackButton`；数据库迁移只破坏性清空单词域和学习数据。

**Tech Stack:** ASP.NET Core Minimal API、EF Core、PostgreSQL、FluentValidation、xUnit；React 19、RTK Query、Vitest、Testing Library、Tailwind CSS、DaisyUI。

**提交约定：** 每个 Task 完成并验证后停止，由用户手动检查和提交当前 diff；执行代理不运行 `git commit`。

---

## 文件结构与职责

- `server/TinyLang/Entities/ExampleSentence.cs`：例句实体和唯一可空音频外键。
- `server/TinyLang/Database/Configurations/ExampleSentenceConfiguration.cs`：例句音频索引和 Restrict 外键。
- `server/TinyLang/Dtos/WordDtos.cs`：例句写入、管理摘要和用户播放契约。
- `server/TinyLang/Dtos/WordValidators.cs`：例句音频空 GUID 验证。
- `server/TinyLang/Services/WordService.cs`：批量校验例句音频、同步目标集合并投影响应。
- `server/TinyLang/Services/WordStudyService.cs`：将例句音频 ID 投影到学习会话内容。
- `admin/src/features/words/ExampleSentenceAudioControl.jsx`：例句音频选择、上传、状态和解除关联控件。
- `admin/src/pages/WordEditor.jsx`：例句表单音频状态、payload 和编辑区域接入。
- `admin/src/services/wordContracts.js`：管理端例句音频摘要运行时校验。
- `app/src/features/wordStudy/WordStudyCard.tsx`：例句原文旁的共享播放按钮。
- `app/src/features/wordStudy/wordStudyTypes.ts`：用户端例句音频 ID 类型。

## Task 1：完成后端例句音频领域、服务和 API 契约

**Files:**

- Modify: `server/TinyLang/Entities/ExampleSentence.cs`
- Modify: `server/TinyLang/Database/Configurations/ExampleSentenceConfiguration.cs`
- Modify: `server/TinyLang/Dtos/WordDtos.cs`
- Modify: `server/TinyLang/Dtos/WordValidators.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Modify: `server/TinyLang/Services/WordService.cs`
- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Test: `server/TinyLang.UnitTests/WordModelTests.cs`
- Test: `server/TinyLang.UnitTests/WordValidatorsTests.cs`
- Test: `server/TinyLang.UnitTests/WordServiceTests.cs`
- Test: `server/TinyLang.UnitTests/WordEndpointTests.cs`
- Test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`
- Test: `server/TinyLang.UnitTests/WordStudyEndpointTests.cs`
- Test: `server/TinyLang.UnitTests/AudioResourceServiceTests.cs`

- [ ] **Step 1：先为 EF 模型和验证器编写新契约测试。**

在 `WordModelTests` 中增加：

```csharp
var example = db.Model.FindEntityType(typeof(ExampleSentence))!;
example.FindProperty(nameof(ExampleSentence.AudioResourceId))!
    .IsNullable.Should().BeTrue();
example.GetIndexes().Should().Contain(index =>
    index.Properties.Select(value => value.Name).SequenceEqual(
        new[] { nameof(ExampleSentence.AudioResourceId) }));
example.GetForeignKeys().Single(value =>
        value.PrincipalEntityType.ClrType == typeof(AudioResource))
    .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
```

同时断言实体公开 `AudioResourceId` 和 `AudioResource`。在 `WordValidatorsTests` 增加：

```csharp
var input = new ExampleSentenceInput
{
    Sentence = "Hello there.",
    Translation = "你好。",
    AudioResourceId = Guid.Empty,
    SortOrder = 0
};
var result = await new ExampleSentenceInputValidator().ValidateAsync(input);
result.Errors.Should().Contain(error =>
    error.ErrorCode == nameof(ErrorCodes.WordExampleAudioInvalid));
```

并断言 `AudioResourceId = null` 合法、`ErrorCodes.WordExampleAudioInvalid` 存在。

- [ ] **Step 2：运行模型和验证器测试并确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests|FullyQualifiedName~WordValidatorsTests"
```

Expected: FAIL，原因是例句实体、DTO 和错误码尚无音频契约。

- [ ] **Step 3：实现实体、EF 配置、DTO 和验证器。**

在 `ExampleSentence` 增加：

```csharp
public Guid? AudioResourceId { get; set; }
public AudioResource? AudioResource { get; set; }
```

在 `ExampleSentenceConfiguration` 增加：

```csharp
builder.HasIndex(value => value.AudioResourceId);
builder.HasOne(value => value.AudioResource)
    .WithMany()
    .HasForeignKey(value => value.AudioResourceId)
    .OnDelete(DeleteBehavior.Restrict);
```

在 `WordDtos.cs` 中定义：

```csharp
public sealed record AdminExampleSentenceAudioResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode);
```

并调整三个例句契约：

```csharp
public sealed record ExampleSentenceInput
{
    public Guid? Id { get; init; }
    public required string Sentence { get; init; }
    public required string Translation { get; init; }
    public Guid? AudioResourceId { get; init; }
    public int SortOrder { get; init; }
}

public sealed record AdminExampleSentenceResponse(
    Guid Id,
    string Sentence,
    string Translation,
    AdminExampleSentenceAudioResponse? Audio,
    int SortOrder);

public sealed record ExampleSentenceResponse(
    string Sentence,
    string Translation,
    Guid? AudioResourceId,
    int SortOrder);
```

在 `ErrorCodes` 的单词错误区域增加：

```csharp
[Description("例句音频资源无效.")]
WordExampleAudioInvalid,
```

在 `ExampleSentenceInputValidator` 增加：

```csharp
RuleFor(value => value.AudioResourceId)
    .Must(value => value is null || value != Guid.Empty)
    .WithErrKey(ErrorCodes.WordExampleAudioInvalid);
```

更新 `WordEndpointTests`、`WordStudyEndpointTests` 中直接构造响应 DTO 的位置，显式传入 `null` 音频。

- [ ] **Step 4：运行模型和验证器测试并确认 GREEN。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests|FullyQualifiedName~WordValidatorsTests|FullyQualifiedName~WordEndpointTests|FullyQualifiedName~WordStudyEndpointTests"
```

Expected: PASS。

- [ ] **Step 5：先编写 WordService 例句音频行为测试。**

在 `WordServiceTests` 至少覆盖：

```csharp
var audio = AudioResource.Create(adminId, "example.mp3", media.Id);
var request = CreateValidRequest() with
{
    Senses =
    [
        CreateSense(0) with
        {
            Examples =
            [
                CreateExample(0) with { AudioResourceId = audio.Id },
                CreateExample(1) with { AudioResourceId = audio.Id }
            ]
        }
    ]
};

var created = await service.CreateAsync(adminId, request);
created.Senses.Single().Examples.Should().OnlyContain(example =>
    example.Audio!.Id == audio.Id);
```

并分别测试：

- 无音频例句正常创建。
- `Uploading` 音频可以关联。
- 不存在的例句音频返回 `WordExampleAudioInvalid`。
- 更新已有例句能够保留、替换和解除音频。
- 删除例句不会删除 `AudioResource`。
- 未修改例句在完整目标集合更新后保留音频关联。
- 管理详情返回名称、状态、时长和失败代码。
- 用户详情返回 `AudioResourceId`。
- 请求中重复的同一音频 ID 合法。

增加保存竞态测试，构造约束名为：

```text
FK_example_sentences_audio_resources_AudioResourceId
```

的 PostgreSQL 外键异常，断言映射为 `WordExampleAudioInvalid`；其他外键异常继续向外抛出。

- [ ] **Step 6：运行 WordService 测试并确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordServiceTests"
```

Expected: FAIL，原因是服务尚未批量验证、保存或投影例句音频。

- [ ] **Step 7：实现批量存在性校验和目标集合同步。**

在 `WordService` 增加约束名：

```csharp
private const string ExampleAudioResourceForeignKey =
    "FK_example_sentences_audio_resources_AudioResourceId";
```

在创建和更新保存前调用：

```csharp
await EnsureExampleAudiosExistAsync(request, cancellationToken);
```

实现一次查询校验：

```csharp
private async Task EnsureExampleAudiosExistAsync(
    WordUpsertRequest request,
    CancellationToken cancellationToken)
{
    var ids = request.Senses
        .SelectMany(sense => sense.Examples)
        .Where(example => example.AudioResourceId.HasValue)
        .Select(example => example.AudioResourceId.GetValueOrDefault())
        .Distinct()
        .ToArray();
    if (ids.Length == 0) return;
    if (ids.Contains(Guid.Empty) ||
        await _db.AudioResources.AsNoTracking()
            .CountAsync(value => ids.Contains(value.Id), cancellationToken) != ids.Length)
    {
        throw NotFoundException.Create(ErrorCodes.WordExampleAudioInvalid);
    }
}
```

在 `ApplyExampleValues` 同步：

```csharp
example.AudioResourceId = input.AudioResourceId;
```

管理投影使用：

```csharp
example.AudioResource == null
    ? null
    : new AdminExampleSentenceAudioResponse(
        example.AudioResource.Id,
        example.AudioResource.Name,
        example.AudioResource.Status,
        example.AudioResource.DurationSeconds,
        example.AudioResource.LastFailureCode)
```

用户投影传递 `example.AudioResourceId`。在 `SaveWordChangesAsync` 中只对 `ExampleAudioResourceForeignKey` 映射：

```csharp
catch (DbUpdateException exception) when (
    _databaseExceptionClassifier.IsForeignKeyConstraintViolation(
        exception,
        ExampleAudioResourceForeignKey))
{
    throw NotFoundException.Create(ErrorCodes.WordExampleAudioInvalid);
}
```

- [ ] **Step 8：扩展单词学习投影和测试。**

在 `WordStudyServiceTests` 为 `GetSessionItemsAsync` 和 `GetNextItemAsync` 的真实服务场景设置：

```csharp
example.AudioResourceId = audio.Id;
example.AudioResource = audio;
```

断言：

```csharp
result[0].Content!.Senses[0].Examples[0].AudioResourceId
    .Should().Be(audio.Id);
next!.Senses[0].Examples[0].AudioResourceId
    .Should().Be(audio.Id);
```

在 `WordStudyService` 两个 `ExampleSentenceResponse` 投影中加入 `example.AudioResourceId`。

- [ ] **Step 9：验证音频删除仍统一映射 AudioInUse。**

将 `AudioResourceServiceTests.DeleteShouldMapPostgresRestrictViolationToAudioInUse` 改为 theory 或增加独立测试，使约束名覆盖：

```text
FK_example_sentences_audio_resources_AudioResourceId
```

断言删除事务不提交并返回 `ErrorCodes.AudioInUse`。`AudioResourceService` 已按 Restrict 外键统一映射，若测试通过则不修改生产服务。

- [ ] **Step 10：运行后端聚焦测试和构建，形成第一个提交边界。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~Word|FullyQualifiedName~AudioResourceServiceTests"
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS，例句音频契约和服务逻辑完整；此处停止供用户检查并手动提交。

## Task 2：生成并验证破坏式数据库 migration

**Files:**

- Create: generated migration source named `AddExampleSentenceAudio`
- Create: generated migration designer named `AddExampleSentenceAudio`
- Modify: `server/TinyLang/Database/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1：确认模型测试通过且工作区只有 Task 1 diff。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests"
git status --short
```

Expected: 模型测试 PASS；用户已提交 Task 1 后，当前 tracked diff 为空。

- [ ] **Step 2：生成 migration。**

Run:

```bash
dotnet tool run dotnet-ef migrations add AddExampleSentenceAudio --project TinyLang --startup-project TinyLang
```

Working directory:

```text
server
```

Expected: 生成 migration、designer 和 snapshot 更新。

- [ ] **Step 3：将 Up 明确调整为单词域破坏式更新。**

在 `Up` 最前面加入：

```csharp
migrationBuilder.Sql("""
    DELETE FROM word_study_session_items;
    DELETE FROM word_study_sessions;
    DELETE FROM user_word_progress;
    DELETE FROM example_sentences;
    DELETE FROM word_senses;
    DELETE FROM words;
    """);
```

确认生成内容包含：

```csharp
migrationBuilder.AddColumn<Guid>(
    name: "AudioResourceId",
    table: "example_sentences",
    type: "uuid",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_example_sentences_AudioResourceId",
    table: "example_sentences",
    column: "AudioResourceId");

migrationBuilder.AddForeignKey(
    name: "FK_example_sentences_audio_resources_AudioResourceId",
    table: "example_sentences",
    column: "AudioResourceId",
    principalTable: "audio_resources",
    principalColumn: "Id",
    onDelete: ReferentialAction.Restrict);
```

- [ ] **Step 4：让 Down 在移除结构前清空单词域数据。**

在 `Down` 最前面加入与 `Up` 相同的数据清理 SQL，然后按生成顺序删除外键、索引和 `AudioResourceId` 列。不得删除 `audio_resources` 或其他模块数据。

- [ ] **Step 5：生成升级、降级和幂等 SQL。**

Run from `server`：

```bash
dotnet tool run dotnet-ef migrations script --idempotent --project TinyLang --startup-project TinyLang --output /tmp/tiny-lang-example-audio.sql
dotnet tool run dotnet-ef migrations script AddExampleSentenceAudio RefactorWordModuleForSharedAudio --project TinyLang --startup-project TinyLang --output /tmp/tiny-lang-example-audio-down.sql
```

检查：

```bash
rg -n "DELETE FROM|example_sentences|AudioResourceId|FK_example_sentences_audio_resources" /tmp/tiny-lang-example-audio.sql /tmp/tiny-lang-example-audio-down.sql
rg -n "DROP TABLE.*articles|DROP TABLE.*audio_resources|DROP TABLE.*users|DROP TABLE.*papers|DROP TABLE.*videos|DELETE FROM (articles|audio_resources|users|papers|videos)" /tmp/tiny-lang-example-audio.sql /tmp/tiny-lang-example-audio-down.sql
```

Expected: 第一条包含目标单词域清理和例句音频结构；第二条无匹配。

- [ ] **Step 6：运行后端全量测试和构建，形成第二个提交边界。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS；此处停止供用户检查 migration 并手动提交。

## Task 3：实现管理端例句音频选择、上传和表单接入

**Files:**

- Create: `admin/src/features/words/ExampleSentenceAudioControl.jsx`
- Create: `admin/src/features/words/ExampleSentenceAudioControl.test.jsx`
- Modify: `admin/src/services/wordContracts.js`
- Modify: `admin/src/services/wordsApi.test.js`
- Modify: `admin/src/pages/WordEditor.jsx`
- Modify: `admin/src/pages/WordEditor.test.jsx`

- [ ] **Step 1：先编写 ExampleSentenceAudioControl 交互测试。**

以 `WordAudioControl.test.jsx` 的共享组件 mock 为基础，测试：

```jsx
render(
  <ExampleSentenceAudioControl
    value={null}
    onChange={onChange}
    disabled={false}
  />,
);
```

必须覆盖：

- 从资源库选择后调用 `onChange(EXISTING_AUDIO)`。
- 上传初始化后写入 `Uploading` 摘要。
- 上传完成后写入完整摘要。
- 解除关联调用 `onChange(null)`。
- `Failed` 显示处理失败提示。
- `disabled` 禁用三个操作按钮。
- 先展开选择器和上传区，再切换 `disabled`，两个区域均关闭且不触发 `onChange`。

- [ ] **Step 2：运行控件测试并确认 RED。**

Run:

```bash
pnpm --dir admin exec vitest run src/features/words/ExampleSentenceAudioControl.test.jsx
```

Expected: FAIL，组件尚不存在。

- [ ] **Step 3：实现 ExampleSentenceAudioControl。**

组件组合：

```jsx
export function ExampleSentenceAudioControl({ value, onChange, disabled }) {
  const [pickerOpen, setPickerOpen] = useState(false);
  const [uploadOpen, setUploadOpen] = useState(false);

  useEffect(() => {
    if (!disabled) return;
    setPickerOpen(false);
    setUploadOpen(false);
  }, [disabled]);
}
```

组件返回结构明确包含：音频名称与状态徽标、三个关联操作按钮、仅在失败状态出现的错误提示、仅在 `uploadOpen` 时挂载的 `AudioUploadControl`，以及始终挂载但由 `pickerOpen` 控制显示的 `AudioResourcePickerDialog`。三个操作按钮全部绑定 `disabled`；选择资源后先调用 `onChange(audio)` 再关闭选择器。

固定文案：

- 空状态：`未关联例句音频`
- 资源库按钮：`从资源库选择`
- 上传按钮：`上传新音频`
- 解除按钮：`解除关联`
- 失败提示：`当前例句音频处理失败，关联仍会保留。请前往音频资源库重新上传或重新处理。`

上传行为：

```jsx
<AudioUploadControl
  waitForProcessing={false}
  onStarted={(id, originalName) =>
    onChange({
      id,
      name: originalName,
      status: "Uploading",
      durationSeconds: null,
      lastFailureCode: null,
    })
  }
  onCompleted={onChange}
/>
```

- [ ] **Step 4：运行控件测试并确认 GREEN。**

Run:

```bash
pnpm --dir admin exec vitest run src/features/words/ExampleSentenceAudioControl.test.jsx
```

Expected: PASS。

- [ ] **Step 5：先更新管理契约和编辑器测试。**

在 `wordsApi.test.js` 的例句 fixture 增加：

```js
audio: audio({ id: IDS.exampleAudio, name: "example.mp3" }),
```

断言合法摘要被规范化，`audio: null` 合法，未知状态或空 UUID 返回 contract error。

在 `WordEditor.test.jsx` 覆盖：

- 服务端已有例句音频时显示名称和“已关联音频”。
- 新例句初始 `audio` 为 `null`。
- 通过 mocked `ExampleSentenceAudioControl` 更新指定例句，不影响其他例句。
- 创建和更新 payload 包含：

```js
audioResourceId: EXAMPLE_AUDIO_ID
```

- 解除关联后 payload 为 `audioResourceId: null`。
- 删除例句后没有音频删除 API 调用。
- 保存 pending 时给例句音频控件传递 `disabled`。

- [ ] **Step 6：运行管理契约和编辑器测试并确认 RED。**

Run:

```bash
pnpm --dir admin exec vitest run src/services/wordsApi.test.js src/pages/WordEditor.test.jsx
```

Expected: FAIL，normalizer、表单和编辑器尚未处理例句音频。

- [ ] **Step 7：实现管理契约和 WordEditor 接入。**

在 `wordContracts.js` 的例句 normalizer 增加：

```js
audio: audio(source.audio),
```

在 `WordEditor.jsx`：

```js
const createExample = () => ({
  _key: draftKey("example"),
  id: null,
  sentence: "",
  translation: "",
  audio: null,
});
```

`formFromWord` 保留服务端 `example.audio`；`toPayload` 增加：

```js
audioResourceId: example.audio?.id ?? null,
```

在展开例句的译文输入框之后渲染：

```jsx
<ExampleSentenceAudioControl
  value={example.audio}
  disabled={pending}
  onChange={(audio) =>
    updateSense(sense._key, (item) => ({
      ...item,
      examples: item.examples.map((value) =>
        value._key === example._key ? { ...value, audio } : value,
      ),
    }))
  }
/>
```

折叠标题在 `example.audio` 非空时显示 `Badge` 文案 `已关联音频`。不要增加音频删除 mutation。

- [ ] **Step 8：运行管理端全量验证并形成第三个提交边界。**

Run:

```bash
pnpm --dir admin test
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS；此处停止供用户检查并手动提交。

## Task 4：在用户端单词学习卡片播放例句音频

**Files:**

- Modify: `app/src/features/wordStudy/wordStudyTypes.ts`
- Modify: `app/src/features/wordStudy/wordStudyApi.test.ts`
- Modify: `app/src/features/wordStudy/WordStudyCard.tsx`
- Modify: `app/src/features/wordStudy/WordStudyCard.test.tsx`

- [ ] **Step 1：先更新 API fixture 和 TypeScript 例句 fixture。**

在 `wordStudyApi.test.ts` 的真实响应 fixture 中加入一条非空例句音频和一条空音频，断言 ID 原样保留。在 `WordStudyCard.test.tsx` 的 `ExampleSentence` fixture 中显式加入 `audioResourceId`：现有纯文本场景使用 `null`。此时不要修改 `wordStudyTypes.ts` 或生产卡片。

- [ ] **Step 2：运行 API、卡片测试和类型检查并确认 RED。**

Run:

```bash
pnpm --dir app exec vitest run src/features/wordStudy/wordStudyApi.test.ts
pnpm --dir app exec tsc -b --pretty false
```

Expected: FAIL，原因是 `ExampleSentence` 类型尚未声明 `audioResourceId`。

- [ ] **Step 3：实现用户端类型。**

将 `ExampleSentence` 改为：

```ts
export interface ExampleSentence {
  sentence: string;
  translation: string;
  sortOrder: number;
  audioResourceId: string | null;
}
```

同步 `WordStudyCard.test.tsx` 中所有例句对象：有关联场景传真实 UUID，其余传 `null`。

Run:

```bash
pnpm --dir app exec vitest run src/features/wordStudy/wordStudyApi.test.ts
pnpm --dir app exec tsc -b --pretty false
```

Expected: PASS。

- [ ] **Step 4：编写例句播放按钮测试并确认 RED。**

扩展 `WordStudyCard.test.tsx` 的 `AudioPlaybackButton` mock，保留：

```tsx
data-audio-resource-id={audioResourceId}
data-variant={variant}
```

构造两条例句：第一条 `audioResourceId = EXAMPLE_AUDIO_ID`，第二条为 `null`。展开释义后断言：

```tsx
const playback = screen.getByRole("button", {
  name: "播放例句 1 音频",
});
expect(playback).toHaveAttribute(
  "data-audio-resource-id",
  EXAMPLE_AUDIO_ID,
);
expect(playback).toHaveAttribute("data-variant", "icon");
expect(
  screen.queryByRole("button", { name: "播放例句 2 音频" }),
).not.toBeInTheDocument();
```

另加多条例句均有音频时按钮标签和资源 ID 一一对应的测试，确保单词自身的“播放单词发音”按钮仍存在且不混淆。

Run:

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudyCard.test.tsx
```

Expected: FAIL，例句尚未渲染共享播放按钮。

- [ ] **Step 5：实现例句播放布局。**

将例句 map 改为带索引：

```tsx
{sense.examples.map((example, exampleIndex) => (
  <blockquote key={`${example.sortOrder}-${example.sentence}`}>
    <div className="flex items-start gap-2">
      <p className="min-w-0 flex-1 wrap-break-word">
        {example.sentence}
      </p>
      {example.audioResourceId ? (
        <AudioPlaybackButton
          audioResourceId={example.audioResourceId}
          label={`例句 ${exampleIndex + 1} 音频`}
          variant="icon"
        />
      ) : null}
    </div>
    <p className="mt-1 wrap-break-word text-base-content/60">
      {example.translation}
    </p>
  </blockquote>
))}
```

保持按钮固定尺寸、原文可换行、译文不被按钮挤压；不增加预加载或例句专属播放请求。

- [ ] **Step 6：运行用户端全量验证并形成第四个提交边界。**

Run:

```bash
pnpm --dir app test
pnpm --dir app lint
pnpm --dir app build
```

Expected: PASS；Vite 现有 chunk 体积警告允许保留；此处停止供用户检查并手动提交。

## Task 5：完善 OpenAPI、遗留扫描并执行三端全量验收

**Files:**

- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs`

- [ ] **Step 1：更新并运行 OpenAPI 契约测试。**

在 `OpenApiContractTests` 断言：

```csharp
var exampleInput = GetSchema(schemas, "ExampleSentenceInput")
    .GetProperty("properties");
AssertNullableUuid(exampleInput.GetProperty("audioResourceId"), schemas);

var adminExample = GetSchema(schemas, "AdminExampleSentenceResponse")
    .GetProperty("properties");
var adminExampleAudio = adminExample.GetProperty("audio");
IsNullable(adminExampleAudio).Should().BeTrue();
GetReferencedSchemas(adminExampleAudio, schemas)
    .Should().Contain("AdminExampleSentenceAudioResponse")
    .And.Contain("AudioResourceStatus");

var publicExample = GetSchema(schemas, "ExampleSentenceResponse")
    .GetProperty("properties");
AssertNullableUuid(publicExample.GetProperty("audioResourceId"), schemas);
```

同时断言三个例句 schema 均不包含 `audioClipId`、对象存储字段、预签名 URL 或上传字段；例句音频摘要属性只包含：

```text
id, name, status, durationSeconds, lastFailureCode
```

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~OpenApiContractTests"
```

Expected: PASS；测试能够防止后续删除例句音频字段、改变 nullability 或泄漏上传/存储字段。

- [ ] **Step 2：执行遗留引用扫描。**

Run:

```bash
rg -n "audioClipId|AudioClipId|ExampleSentenceAudio|WordExampleAudioInvalid|AudioResourceId" server/TinyLang server/TinyLang.UnitTests admin/src app/src
```

人工确认：

- `audioClipId` / `AudioClipId` 只允许出现在历史 migration 和明确断言旧字段不存在的测试字符串中。
- `ExampleSentenceAudio` 只用于新的管理摘要和控件名称，不存在独立上传或播放 API。
- 生产例句 DTO、实体、服务和用户类型均使用 `AudioResourceId`。

- [ ] **Step 3：运行后端全量验证。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS，0 failed、0 build errors。

- [ ] **Step 4：运行管理端全量验证。**

Run:

```bash
pnpm --dir admin test
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS，无 lint 错误，生产构建成功。

- [ ] **Step 5：运行用户端全量验证。**

Run:

```bash
pnpm --dir app test
pnpm --dir app lint
pnpm --dir app build
```

Expected: PASS，无测试、lint 或 TypeScript 错误；现有 chunk 体积警告不视为回归。

- [ ] **Step 6：检查最终 diff 并交付最后一个提交边界。**

Run:

```bash
git status --short
git diff --check
git diff --stat
```

Expected: 只有例句共享音频后端、migration、管理端、用户端、测试和必要文档变更；`docs/` 与 `word.md` 等用户原有未跟踪文件保持不被删除。停止供用户最终检查并手动提交。
