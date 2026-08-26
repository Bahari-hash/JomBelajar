# 单词模块重构与共享音频适配 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除单词发布状态、审计用户和多发音结构，让单词创建后立即有效，并通过一个可空的 `AudioResourceId` 接入共享音频资源和现有单词学习页面。

**Architecture:** 保留现有 `Word` 聚合、释义/例句完整目标集合更新、词头规范化、乐观并发和单词学习会话；将 `WordPronunciation` 替换为 `Word` 上的单向可空音频外键。管理端复用音频库选择与上传基础组件，用户端复用共享播放请求和播放器。数据库迁移明确采用破坏式更新，不兼容现有单词和学习数据。

**Tech Stack:** ASP.NET Core Minimal API、EF Core、PostgreSQL、FluentValidation、xUnit；React 19、RTK Query、Vitest、Testing Library、Tailwind CSS、DaisyUI。

**提交约定：** 每个 Task 完成并验证后停止，由用户手动检查和提交当前 diff；执行代理不运行 `git commit`。

---

## 文件结构与职责

- `server/TinyLang/Entities/Word.cs`：单词聚合根和唯一可空音频外键。
- `server/TinyLang/Database/Configurations/WordConfiguration.cs`：单词索引、并发令牌和音频 Restrict 外键。
- `server/TinyLang/Dtos/WordDtos.cs`、`WordStudyDtos.cs`：新的单音频管理、用户查询和学习契约。
- `server/TinyLang/Dtos/WordValidators.cs`：至少一个释义、可空例句和音频 ID 校验。
- `server/TinyLang/Services/WordService.cs`：立即有效的创建、更新、删除和查询逻辑。
- `server/TinyLang/Services/WordStudyService.cs`、`Policies/WordVisibilityPolicy.cs`：无发布状态的学习候选和单音频投影。
- `admin/src/features/words/WordAudioControl.jsx`：组合共享选择器与上传控件的单词音频表单控件。
- `admin/src/pages/WordEditor.jsx`：单词、释义、例句和单音频编辑。
- `app/src/features/audio/AudioPlaybackButton.tsx`：文章和单词共享的播放按钮，支持默认与图标模式。
- `app/src/features/wordStudy/WordStudyCard.tsx`：单音频、可空例句的学习卡片。

## Task 1：完成后端单词领域、服务与 API 重构

**Files:**

- Modify: `server/TinyLang/Entities/Word.cs`
- Modify: `server/TinyLang/Entities/User.cs`
- Delete: `server/TinyLang/Entities/WordPronunciation.cs`
- Delete: `server/TinyLang/Entities/Enums/WordPublicationStatus.cs`
- Modify: `server/TinyLang/Database/Configurations/WordConfiguration.cs`
- Delete: `server/TinyLang/Database/Configurations/WordPronunciationConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/UserWordProgressConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/WordStudySessionItemConfiguration.cs`
- Modify: `server/TinyLang/Database/ApplicationDbContext.cs`
- Modify: `server/TinyLang/Interfaces/IApplicationDbContext.cs`
- Modify: `server/TinyLang/Dtos/WordDtos.cs`
- Modify: `server/TinyLang/Dtos/WordStudyDtos.cs`
- Modify: `server/TinyLang/Dtos/WordValidators.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Modify: `server/TinyLang/Services/IWordService.cs`
- Modify: `server/TinyLang/Services/WordService.cs`
- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Modify: `server/TinyLang/Policies/WordVisibilityPolicy.cs`
- Modify: `server/TinyLang/Endpoints/WordEndpoints.cs`
- Test: `server/TinyLang.UnitTests/WordModelTests.cs`
- Test: `server/TinyLang.UnitTests/WordValidatorsTests.cs`
- Test: `server/TinyLang.UnitTests/WordServiceTests.cs`
- Test: `server/TinyLang.UnitTests/WordEndpointTests.cs`
- Test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`
- Test: `server/TinyLang.UnitTests/WordStudyWordDeletionTests.cs`
- Test: `server/TinyLang.UnitTests/WordStudyEndpointTests.cs`

- [ ] **Step 1：先把模型和 DTO 测试改成新契约并确认失败。**

在 `WordModelTests` 中断言：

```csharp
typeof(Word).GetProperty("AudioResourceId").Should().NotBeNull();
typeof(Word).GetProperty("AudioResource").Should().NotBeNull();
typeof(Word).GetProperty("Status").Should().BeNull();
typeof(Word).GetProperty("PublishedAt").Should().BeNull();
typeof(Word).GetProperty("ArchivedAt").Should().BeNull();
typeof(Word).GetProperty("CreatedById").Should().BeNull();
typeof(Word).GetProperty("LastEditorId").Should().BeNull();
typeof(Word).GetProperty("Pronunciations").Should().BeNull();
```

同时断言 EF 模型中 `Word.AudioResourceId` 可空、存在索引，外键删除行为为 `Restrict`；`UserWordProgress.WordId` 和 `WordStudySessionItem.WordId` 删除行为为 `Cascade`；不再存在 `WordPronunciation` 实体类型。

在 `WordValidatorsTests` 增加：空释义集合返回 `WordSenseRequired`；一个无例句释义合法；`AudioResourceId = Guid.Empty` 返回 `WordAudioInvalid`。

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests|FullyQualifiedName~WordValidatorsTests"
```

Expected: FAIL，原因是新字段、错误码和删除行为尚未实现。

- [ ] **Step 2：定义最终实体和 EF Core 配置。**

将 `Word` 收敛为：

```csharp
public sealed class Word : BaseAuditableEntity
{
    public Word() => Id = Guid.NewGuid();

    public required string Headword { get; set; }
    public required string NormalizedHeadword { get; set; }
    public Guid? AudioResourceId { get; set; }
    public AudioResource? AudioResource { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<WordSense> Senses { get; set; } = [];
    public ICollection<UserWordProgress> UserProgress { get; set; } = [];
    public ICollection<WordStudySessionItem> StudySessionItems { get; set; } = [];
}
```

在 `WordConfiguration` 中保留规范化词头唯一索引和并发令牌，增加：

```csharp
builder.HasIndex(value => value.AudioResourceId);
builder.HasIndex(value => new { value.UpdatedAt, value.Id });
builder.HasOne(value => value.AudioResource)
    .WithMany()
    .HasForeignKey(value => value.AudioResourceId)
    .OnDelete(DeleteBehavior.Restrict);
```

删除状态索引、审计用户关系、`User.CreatedWords`、`User.EditedWords`、`WordPronunciations` DbSet 和对应配置文件。将两个学习外键的删除行为改成 `Cascade`。

- [ ] **Step 3：定义最终单词写入和响应契约。**

`WordUpsertRequest` 使用：

```csharp
public abstract record WordUpsertRequest
{
    public required string Headword { get; init; }
    public Guid? AudioResourceId { get; init; }
    public IReadOnlyCollection<WordSenseInput> Senses { get; init; } = [];
}
```

删除 `WordPronunciationInput`、`AdminWordPronunciationResponse` 和 `WordPronunciationResponse`。定义：

```csharp
public sealed record DeleteWordRequest
{
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

public sealed record AdminWordAudioResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode);
```

`AdminWordResponse` 包含 `Audio`、`ConcurrencyStamp`、`Senses`、`CreatedAt`、`UpdatedAt`；`AdminWordListItemResponse` 包含 `HasAudio`，不包含状态和审计用户；`WordListItemResponse`、`WordResponse`、`WordStudyNextItemResponse`、`WordStudySessionItemContentResponse` 使用 `Guid? AudioResourceId`。

新增 `WordSenseRequired`、`WordAudioInvalid`，删除只服务于单词状态、多发音和学习历史删除保护的错误码。

- [ ] **Step 4：实现验证器并让模型/验证测试通过。**

为写入请求增加：

```csharp
RuleFor(value => value.AudioResourceId)
    .Must(value => value is null || value != Guid.Empty)
    .WithErrKey(ErrorCodes.WordAudioInvalid);

RuleFor(value => value.Senses)
    .Must(value => value.Count > 0)
    .WithErrKey(ErrorCodes.WordSenseRequired);
```

保留最大 20 个释义、每个释义最多 20 个例句、子项 ID 唯一和排序唯一验证；删除全部发音集合验证和状态筛选验证。删除请求只校验非空并发标识。

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests|FullyQualifiedName~WordValidatorsTests"
```

Expected: PASS。

- [ ] **Step 5：先重写服务测试，覆盖立即有效和共享音频，再确认失败。**

测试至少覆盖：

```csharp
var created = await service.CreateAsync(adminId, request);
created.Audio!.Id.Should().Be(audio.Id);

var publicWord = await service.GetUserByIdAsync(created.Id);
publicWord.AudioResourceId.Should().Be(audio.Id);
publicWord.Senses.Single().Examples.Should().BeEmpty();
```

并覆盖无音频创建、关联 `Uploading`/`Failed` 音频、解除关联、不存在音频返回 `WordAudioInvalid`、更新释义/例句完整目标集合、并发冲突、用户列表按 `UpdatedAt` 稳定排序、删除具有学习记录的单词成功。

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordServiceTests|FullyQualifiedName~WordStudyServiceTests|FullyQualifiedName~WordStudyWordDeletionTests"
```

Expected: FAIL，旧服务仍依赖草稿、发布和多发音。

- [ ] **Step 6：实现新的 `IWordService` 和 `WordService`。**

接口只保留：

```csharp
Task<AdminWordResponse> CreateAsync(Guid adminId, CreateWordRequest request, CancellationToken cancellationToken = default);
Task<AdminWordResponse> UpdateAsync(Guid wordId, Guid adminId, UpdateWordRequest request, CancellationToken cancellationToken = default);
Task DeleteAsync(Guid wordId, Guid adminId, DeleteWordRequest request, CancellationToken cancellationToken = default);
Task<AdminWordResponse> GetAdminByIdAsync(Guid wordId, CancellationToken cancellationToken = default);
Task<PagedResponse<AdminWordListItemResponse>> GetAdminListAsync(AdminWordListRequest request, CancellationToken cancellationToken = default);
Task<WordResponse> GetUserByIdAsync(Guid wordId, CancellationToken cancellationToken = default);
Task<PagedResponse<WordListItemResponse>> GetUserListAsync(WordListRequest request, CancellationToken cancellationToken = default);
```

服务实现要求：

- 创建和更新前调用 `EnsureAudioExistsAsync(Guid?)`，只检查资源存在。
- `ValidateTargetCollections` 要求至少一个释义并删除发音分支。
- `FindWordForEditAsync` 只加载释义和例句。
- 删除发布状态判断和学习历史检查。
- 创建、更新、删除日志仍记录当前管理员 ID，但不持久化审计用户字段。
- 管理详情投影通过 `word.AudioResource == null ? null : new AdminWordAudioResponse(...)` 返回音频摘要。
- 用户列表和详情返回 `AudioResourceId` 与 `UpdatedAt`。
- 保存异常继续映射词头唯一、排序唯一、并发和音频外键错误。

- [ ] **Step 7：更新可见性、学习服务和 endpoints。**

`WordVisibilityPolicy.Apply` 改为：

```csharp
return query.Where(word => word.Senses.Any());
```

`WordStudyService` 的两个内容投影直接返回 `word.AudioResourceId`；顺序选词使用：

```csharp
.OrderByDescending(value => value.UpdatedAt)
.ThenByDescending(value => value.Id)
```

`WordEndpoints` 删除 publish/unpublish/archive 路由和处理函数，创建调用 `CreateAsync`，删除绑定 `DeleteWordRequest`。更新 endpoint 测试，断言生命周期路径不再映射。

- [ ] **Step 8：运行后端单词测试并形成第一个提交边界。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~Word"
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS，生产代码不再引用 `WordPublicationStatus` 或 `WordPronunciation`；此处停止供用户检查并手动提交。

## Task 2：生成并验证破坏式数据库迁移

**Files:**

- Create: `server/TinyLang/Database/Migrations/<timestamp>_RefactorWordModuleForSharedAudio.cs`
- Create: `server/TinyLang/Database/Migrations/<timestamp>_RefactorWordModuleForSharedAudio.Designer.cs`
- Modify: `server/TinyLang/Database/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `server/TinyLang.UnitTests/WordModelTests.cs`

- [ ] **Step 1：确认当前模型测试通过且 migration 尚未生成。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordModelTests"
```

Expected: PASS。

- [ ] **Step 2：生成 EF Core migration。**

Run:

```bash
dotnet ef migrations add RefactorWordModuleForSharedAudio --project server/TinyLang --startup-project server/TinyLang
```

Expected: 生成 migration、designer 和 snapshot 更新。

- [ ] **Step 3：将 migration 明确调整为破坏式单词数据重建。**

在 `Up` 最前面清空依赖顺序明确的开发数据：

```csharp
migrationBuilder.Sql("""
    DELETE FROM word_study_session_items;
    DELETE FROM word_study_sessions;
    DELETE FROM user_word_progress;
    DELETE FROM example_sentences;
    DELETE FROM word_senses;
    DELETE FROM word_pronunciations;
    DELETE FROM words;
    """);
```

随后确认 migration：删除旧状态/审计列和索引、删除 `word_pronunciations`、增加可空 `AudioResourceId`、增加索引和 `Restrict` 外键、将两个学习外键改为 `Cascade`。`Down` 可以在空表前提下恢复旧结构，但不得尝试恢复已删除数据。

- [ ] **Step 4：生成幂等 SQL 并检查影响范围。**

Run:

```bash
dotnet ef migrations script --idempotent --project server/TinyLang --startup-project server/TinyLang --output /tmp/tiny-lang-word-refactor.sql
rg -n "words|word_senses|example_sentences|word_pronunciations|user_word_progress|word_study" /tmp/tiny-lang-word-refactor.sql
rg -n "DROP TABLE.*articles|DROP TABLE.*audio_resources|DROP TABLE.*users|DROP TABLE.*papers|DROP TABLE.*videos" /tmp/tiny-lang-word-refactor.sql
```

Expected: 第一条扫描能看到预期单词结构；第二条扫描无匹配。

- [ ] **Step 5：运行后端测试和构建并形成第二个提交边界。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS；此处停止供用户更新数据库、检查并手动提交。

## Task 3：重构管理端单词列表、契约和删除操作

**Files:**

- Rename: `admin/src/constants/wordStatus.js` -> `admin/src/constants/wordOptions.js`
- Modify: `admin/src/services/wordContracts.js`
- Modify: `admin/src/services/wordsApi.js`
- Modify: `admin/src/lib/wordFilters.js`
- Modify: `admin/src/features/words/WordFilters.jsx`
- Modify: `admin/src/features/words/WordTable.jsx`
- Delete: `admin/src/features/words/WordActionDialog.jsx`
- Create: `admin/src/features/words/WordDeleteDialog.jsx`
- Modify: `admin/src/pages/Words.jsx`
- Test: `admin/src/services/wordsApi.test.js`
- Test: `admin/src/lib/wordFilters.test.js`
- Test: `admin/src/pages/Words.test.jsx`

- [ ] **Step 1：先更新管理端契约/API/列表测试并确认失败。**

测试断言：

- 列表 URL 只编码 `keyword`、`partOfSpeech`、`definition`、`page`、`pageSize`。
- `publishWord`、`unpublishWord`、`archiveWord` endpoints 和 hooks 不存在。
- 列表响应包含 `hasAudio`，不包含 `status`、`createdBy`、`lastEditor`、`publishedAt`、`archivedAt`、`pronunciationCount`。
- 删除仍发送 `{ concurrencyStamp }`。
- 页面只提供编辑和删除，不展示发布状态或生命周期动作。

Run:

```bash
pnpm --dir admin test -- src/services/wordsApi.test.js src/lib/wordFilters.test.js src/pages/Words.test.jsx
```

Expected: FAIL，旧契约和页面仍暴露状态机。

- [ ] **Step 2：实现新的 normalizer 和 RTK Query 契约。**

`normalizeWordListItem` 返回：

```js
{
  id,
  headword,
  primaryPartOfSpeech,
  primaryDefinition,
  senseCount,
  exampleCount,
  hasAudio,
  createdAt,
  updatedAt,
  concurrencyStamp,
}
```

`normalizeAdminWord` 额外返回 `senses` 和可空 `audio`；音频摘要校验 `id`、`name`、共享状态枚举、可空时长和可空失败码。`wordsApi` 删除三个生命周期 mutation，只保留列表、详情、创建、更新和删除。

- [ ] **Step 3：简化筛选、列表和删除对话框。**

将词性常量移到 `wordOptions.js` 并删除全部状态常量。`WordFilters` 删除状态选择器。`WordTable` 使用音频图标或“已关联/未关联”文本表达 `hasAudio`，操作只保留编辑和删除。

创建 `WordDeleteDialog`，内部只调用 `useDeleteWordMutation`，成功提示“单词已删除”，并发冲突触发 `onConflict`。`Words.jsx` 只维护待删除单词，不再维护通用状态动作。

- [ ] **Step 4：运行管理端聚焦测试、lint 和构建并形成第三个提交边界。**

Run:

```bash
pnpm --dir admin test -- src/services/wordsApi.test.js src/lib/wordFilters.test.js src/pages/Words.test.jsx
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS；此处停止供用户检查并手动提交。

## Task 4：重构管理端单词编辑器并接入共享音频上传

**Files:**

- Create: `admin/src/features/words/WordAudioControl.jsx`
- Create: `admin/src/features/words/WordAudioControl.test.jsx`
- Modify: `admin/src/pages/WordEditor.jsx`
- Modify: `admin/src/pages/WordEditor.test.jsx`
- Modify: `admin/src/services/wordContracts.js`

- [ ] **Step 1：先编写 `WordAudioControl` 交互测试并确认失败。**

测试覆盖：

```jsx
render(<WordAudioControl value={null} onChange={onChange} disabled={false} />);
await user.click(screen.getByRole("button", { name: "从资源库选择" }));
expect(screen.getByRole("dialog", { name: "选择音频资源" })).toBeInTheDocument();
```

继续覆盖选择现有音频、本地上传初始化后立即调用 `onChange` 关联 `Uploading` 音频、处理完成后刷新摘要、解除关联、失败状态提示和 disabled 状态。

Run:

```bash
pnpm --dir admin test -- src/features/words/WordAudioControl.test.jsx
```

Expected: FAIL，组件尚不存在。

- [ ] **Step 2：用共享基础组件实现 `WordAudioControl`。**

组件组合：

```jsx
<AudioResourcePickerDialog
  open={pickerOpen}
  value={value}
  onOpenChange={setPickerOpen}
  onSelect={onChange}
/>

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

文案使用“单词读音”，支持从资源库选择、上传新音频和解除关联；处理失败保留关联并提示前往音频资源库处理。

- [ ] **Step 3：先更新编辑器测试为新表单契约并确认失败。**

测试断言创建请求精确为：

```js
{
  headword: "hello",
  audioResourceId: AUDIO_ID,
  senses: [
    {
      partOfSpeech: "Noun",
      definition: "你好",
      usageNote: null,
      sortOrder: 0,
      examples: [],
    },
  ],
}
```

更新请求额外包含 `concurrencyStamp` 和已有子项 ID。页面不展示草稿、发布、下架、归档、IPA、口音或默认发音控件；新建表单至少有一个释义编辑项；创建成功提示“单词已创建”。

Run:

```bash
pnpm --dir admin test -- src/pages/WordEditor.test.jsx
```

Expected: FAIL。

- [ ] **Step 4：实现新的 `WordEditor`。**

表单形状固定为：

```js
{
  headword: "",
  audio: null,
  senses: [createSense()],
}
```

`toPayload` 发送 `audioResourceId: form.audio?.id ?? null`。删除 `pronunciations`、`readOnly`、`statusAction`、发布按钮和 `WordActionDialog`；插入 `WordAudioControl`。信息区只显示创建时间、更新时间和并发标识。新建表单默认创建一个空释义；当表单只剩一个释义时禁用该释义的删除按钮，确保 UI 与服务端最小数量一致。

- [ ] **Step 5：运行管理端编辑器测试和全量回归并形成第四个提交边界。**

Run:

```bash
pnpm --dir admin test -- src/features/words/WordAudioControl.test.jsx src/pages/WordEditor.test.jsx
pnpm --dir admin test
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS；此处停止供用户检查并手动提交。

## Task 5：适配用户端单词学习的单音频播放

**Files:**

- Modify: `app/src/features/audio/AudioPlaybackButton.tsx`
- Modify: `app/src/features/audio/AudioPlaybackButton.test.tsx`
- Delete: `app/src/features/wordStudy/WordStudyAudioButton.tsx`
- Modify: `app/src/features/wordStudy/wordStudyTypes.ts`
- Modify: `app/src/features/wordStudy/wordStudyApi.ts`
- Modify: `app/src/features/wordStudy/wordStudyApi.test.ts`
- Modify: `app/src/features/wordStudy/WordStudyCard.tsx`
- Modify: `app/src/features/wordStudy/WordStudyCard.test.tsx`
- Modify: `app/src/features/wordStudy/WordStudyDirectory.test.tsx`
- Modify: `app/src/pages/WordsPage.test.tsx`

- [ ] **Step 1：先扩展共享播放器测试并确认失败。**

增加图标模式测试：

```tsx
render(
  <AudioPlaybackButton
    audioResourceId={AUDIO_ID}
    label="单词发音"
    variant="icon"
  />,
);

expect(screen.getByRole("button", { name: "播放单词发音" })).toBeInTheDocument();
expect(screen.queryByText("播放朗读")).not.toBeInTheDocument();
```

确认点击仍调用 `/audio/{id}/playback`，播放、暂停、请求取消和 `AudioNotReady` 错误行为与默认模式一致。

Run:

```bash
pnpm --dir app test -- src/features/audio/AudioPlaybackButton.test.tsx
```

Expected: FAIL，组件尚无 `variant`。

- [ ] **Step 2：实现共享播放器 `variant="default" | "icon"`。**

Props 改为：

```tsx
interface AudioPlaybackButtonProps {
  audioResourceId: string;
  label?: string;
  variant?: "default" | "icon";
}
```

默认模式保持文章现有按钮和文案；图标模式使用固定尺寸图标按钮、tooltip/title 和完整 aria-label，不显示可见文字。两种模式复用同一请求、音频实例和错误状态逻辑。

- [ ] **Step 3：先更新单词学习类型和卡片测试并确认失败。**

最终类型为：

```ts
export interface ExampleSentence {
  sentence: string;
  translation: string;
  sortOrder: number;
}

export interface WordStudySessionItemContent {
  headword: string;
  senses: WordSense[];
  audioResourceId: string | null;
}
```

`WordStudyNextItem` 同样使用 `audioResourceId`。卡片测试覆盖有音频显示一个播放按钮、无音频不显示、例句无音频按钮、空例句集合可展开显示。

Run:

```bash
pnpm --dir app test -- src/features/wordStudy/WordStudyCard.test.tsx src/features/wordStudy/wordStudyApi.test.ts
```

Expected: FAIL，旧类型仍使用 `pronunciations` 和 `audioClipId`。

- [ ] **Step 4：切换学习卡片和 API 到共享音频。**

删除 `WordPronunciation` 类型、`AudioPlayback` 重复类型和 `requestWordAudio`。`WordStudyCard` 使用：

```tsx
{content.audioResourceId ? (
  <AudioPlaybackButton
    audioResourceId={content.audioResourceId}
    label="单词发音"
    variant="icon"
  />
) : null}
```

删除例句音频分支和 `WordStudyAudioButton.tsx`。其他学习会话、目录、结果提交和页面逻辑不变。

- [ ] **Step 5：运行用户端聚焦测试和全量回归并形成第五个提交边界。**

Run:

```bash
pnpm --dir app test -- src/features/audio/AudioPlaybackButton.test.tsx src/features/wordStudy/WordStudyCard.test.tsx src/features/wordStudy/wordStudyApi.test.ts src/pages/WordsPage.test.tsx
pnpm --dir app test
pnpm --dir app lint
pnpm --dir app build
```

Expected: PASS；此处停止供用户检查并手动提交。

## Task 6：清理 OpenAPI、遗留引用并执行全量验收

**Files:**

- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs`
- Modify: any remaining directly related test fixture containing retired word fields
- Modify: `docs/superpowers/specs/2026-08-17-word-module-refactor-design.md` only if implementation revealed a confirmed contract correction

- [ ] **Step 1：先更新 OpenAPI 契约测试并确认旧断言失败。**

断言最终 OpenAPI：

- 不包含 `WordPublicationStatus`、`WordPronunciationInput`、`AdminWordPronunciationResponse`、`WordPronunciationResponse`。
- 不包含 `/api/admin/words/{id}/publish`、`unpublish`、`archive`。
- `CreateWordRequest` 和 `UpdateWordRequest` 包含可空 `audioResourceId` 和 `senses`。
- `AdminWordResponse` 包含可空音频摘要。
- 单词学习内容包含可空 `audioResourceId`。

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~OpenApiContractTests"
```

Expected: 首次运行因旧 schema/path 断言而 FAIL，调整生产契约后 PASS。

- [ ] **Step 2：执行遗留引用扫描并清理生产代码。**

Run:

```bash
rg -n "WordPublicationStatus|WordPronunciation|Pronunciations|pronunciations|WordStatusInvalid|WordPublish|WordArchive|WordPublishedDelete|CreatedWords|EditedWords|requestWordAudio|WordStudyAudioButton|audioClipId" server/TinyLang server/TinyLang.UnitTests admin/src app/src
```

Expected: 只允许历史 migration 文件和明确测试旧字段不存在的字符串断言；生产实体、服务、DTO、页面和运行时类型无匹配。

- [ ] **Step 3：运行后端全量验证。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj --no-restore
```

Expected: PASS，0 failed。

- [ ] **Step 4：运行管理端全量验证。**

Run:

```bash
pnpm --dir admin test
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS，无 lint 错误，Vite 构建成功。

- [ ] **Step 5：运行用户端全量验证。**

Run:

```bash
pnpm --dir app test
pnpm --dir app lint
pnpm --dir app build
```

Expected: PASS，无 lint/TypeScript 错误，Vite 构建成功。

- [ ] **Step 6：检查最终 diff 并交付最后一个提交边界。**

Run:

```bash
git status --short
git diff --check
git diff --stat
```

Expected: 只有本计划列出的单词模块、共享播放适配、migration、测试和文档变更；无空白错误。停止供用户最终检查并手动提交。
