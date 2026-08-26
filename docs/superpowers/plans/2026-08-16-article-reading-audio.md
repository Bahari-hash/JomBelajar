# 文章朗读音频支持实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为文章增加一个可选的朗读音频关联，让管理员能够选择共享音频资源或上传新音频，并让登录用户在文章详情页按需播放已就绪的朗读音频。

**Architecture:** `Article` 只保存一个可空的 `ReadingAudioResourceId` 单向外键，音频状态和处理生命周期继续完全由 `AudioResource` 模块负责。文章保存、编辑和发布只校验音频资源存在，不要求 `Ready`；用户点击播放时复用 `/api/audio/{id}/playback`，由音频模块返回短期地址或 `AudioNotReady`。管理端复用现有音频上传 API，并将上传控件扩展为“源文件确认后即完成”的文章模式，不等待后台编解码。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/PostgreSQL、React 19、Redux Toolkit Query、React Router、TypeScript、Vitest/Testing Library、xUnit/FluentAssertions。

---

## 已确认的实施解释

- 文章朗读音频是可选字段；没有朗读音频的文章仍可正常创建、发布和阅读。
- 单篇文章最多关联一个朗读音频，字段统一命名为 `ReadingAudioResourceId`。
- `Uploading`、`Queued`、`Processing`、`Ready`、`Failed` 五种状态都允许被文章关联，也都不阻止文章发布。
- 管理员从本地上传时，音频初始化接口返回 ID 后立即写入文章表单；保存文章不等待文件上传或编解码完成。
- 上传控件仍负责把浏览器中的源文件上传并确认，但文章模式在确认完成后停止轮询，不等待 `Ready`。
- 用户端只在点击播放时请求短期播放地址；非 `Ready` 显示“音频暂时不可用，请稍后再试”。
- 解除文章关联、删除文章都不会删除音频资源；删除仍被文章引用的音频由 Restrict FK 阻止并继续映射为 `AudioInUse`。
- 本阶段不修改文章列表筛选，不增加自动播放、播放进度记忆、逐句同步、高亮或听写功能。

## 文件地图

### 后端

- 修改 `server/TinyLang/Entities/Article.cs`：增加可空朗读音频外键和导航。
- 修改 `server/TinyLang/Database/Configurations/ArticleConfiguration.cs`：增加索引和 Restrict 外键。
- 修改 `server/TinyLang/Dtos/ArticleDtos.cs`、`ArticleValidators.cs`：增加写入字段、管理员摘要和公开文章字段。
- 修改 `server/TinyLang/Exceptions/ErrorCodes.cs`：增加稳定的文章朗读音频错误码。
- 修改 `server/TinyLang/Services/ArticleService.cs`：验证、保存、查询和映射朗读音频关联。
- 新建 EF migration，并更新 `ApplicationDbContextModelSnapshot.cs`。
- 修改文章领域、验证器、服务、endpoint 和 OpenAPI 测试。

### 管理端

- 修改 `admin/src/features/audio/AudioUploadControl.jsx`：支持确认上传后结束、不等待转码的模式。
- 创建 `admin/src/features/audio/AudioResourcePickerDialog.jsx`：通用音频资源选择器。
- 创建 `admin/src/features/articles/ArticleReadingAudioControl.jsx`：文章朗读音频选择、上传、查看和解除关联。
- 修改 `admin/src/pages/ArticleEditor.jsx`、文章/音频 contracts、API 测试和页面测试。

### 用户端

- 创建 `app/src/features/audio/audioPlayback.ts`：共享音频播放请求契约。
- 创建 `app/src/features/audio/AudioPlaybackButton.tsx`：可复用播放/暂停/错误状态控件。
- 修改文章类型、详情页和测试。

## Task 1: 建立文章朗读音频领域关系和 HTTP 契约

**Files:**

- Modify: `server/TinyLang/Entities/Article.cs`
- Modify: `server/TinyLang/Database/Configurations/ArticleConfiguration.cs`
- Modify: `server/TinyLang/Dtos/ArticleDtos.cs`
- Modify: `server/TinyLang/Dtos/ArticleValidators.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Test: `server/TinyLang.UnitTests/ArticleModelTests.cs`
- Test: `server/TinyLang.UnitTests/ArticleValidatorsTests.cs`

- [ ] **Step 1: 写失败的 EF 模型测试。**

在 `ArticleModelTests.cs` 中断言文章字段可空、外键指向 `AudioResource` 且删除行为为 `Restrict`：

```csharp
[Fact]
public void ReadingAudioShouldBeOptionalAndRestrictAudioDeletion()
{
    using var db = CreateDbContext();
    var article = db.Model.FindEntityType(typeof(Article))!;
    var property = article.FindProperty(nameof(Article.ReadingAudioResourceId));
    var foreignKey = article.GetForeignKeys().Single(value =>
        value.PrincipalEntityType.ClrType == typeof(AudioResource));

    property.Should().NotBeNull();
    property!.IsNullable.Should().BeTrue();
    foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
}
```

- [ ] **Step 2: 写失败的请求验证测试。**

```csharp
[Fact]
public async Task CreateArticleShouldRejectEmptyReadingAudioId()
{
    var request = new CreateArticleRequest
    {
        Title = "Article",
        ContentMarkdown = "Content",
        ReadingAudioResourceId = Guid.Empty
    };

    var result = await new CreateArticleRequestValidator().ValidateAsync(request);

    result.Errors.Should().Contain(error =>
        error.PropertyName == nameof(request.ReadingAudioResourceId) &&
        error.ErrorCode == ErrorCodes.ArticleReadingAudioInvalid.ToString());
}
```

- [ ] **Step 3: 运行测试确认新字段尚不存在。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~ArticleModelTests|FullyQualifiedName~ArticleValidatorsTests"
```

Expected: FAIL，提示 `ReadingAudioResourceId` 或 `ArticleReadingAudioInvalid` 尚未定义。

- [ ] **Step 4: 增加实体和 EF 配置。**

`Article` 增加：

```csharp
public Guid? ReadingAudioResourceId { get; set; }
public AudioResource? ReadingAudioResource { get; set; }
```

`ArticleConfiguration` 增加：

```csharp
builder.HasIndex(value => value.ReadingAudioResourceId);
builder.HasOne(value => value.ReadingAudioResource)
    .WithMany()
    .HasForeignKey(value => value.ReadingAudioResourceId)
    .OnDelete(DeleteBehavior.Restrict);
```

不得向 `AudioResource` 添加文章反向集合。

- [ ] **Step 5: 增加 DTO 和错误契约。**

在 `ArticleUpsertRequest` 增加：

```csharp
public Guid? ReadingAudioResourceId { get; init; }
```

新增管理员摘要：

```csharp
public sealed record ArticleReadingAudioResponse(
    Guid Id,
    string Name,
    AudioResourceStatus Status,
    double? DurationSeconds,
    string? LastFailureCode);
```

`AdminArticleResponse` 增加 `ArticleReadingAudioResponse? ReadingAudio`；`PublicArticleResponse` 增加 `Guid? ReadingAudioResourceId`。文章列表响应不增加音频字段。

在 `ErrorCodes` 的 Article 区域增加：

```csharp
[Description("文章朗读音频资源无效或不存在.")]
ArticleReadingAudioInvalid,
```

- [ ] **Step 6: 增加共享验证规则。**

```csharp
RuleFor(value => value.ReadingAudioResourceId)
    .Must(value => value is null || value != Guid.Empty)
    .WithErrKey(ErrorCodes.ArticleReadingAudioInvalid);
```

- [ ] **Step 7: 运行领域和验证器测试。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~ArticleModelTests|FullyQualifiedName~ArticleValidatorsTests"
```

Expected: PASS。

- [ ] **Step 8: 提交 Task 1。**

```bash
git add server/TinyLang/Entities/Article.cs server/TinyLang/Database/Configurations/ArticleConfiguration.cs server/TinyLang/Dtos/ArticleDtos.cs server/TinyLang/Dtos/ArticleValidators.cs server/TinyLang/Exceptions/ErrorCodes.cs server/TinyLang.UnitTests/ArticleModelTests.cs server/TinyLang.UnitTests/ArticleValidatorsTests.cs
git commit -m "feat(server): add article reading audio contract"
```

## Task 2: 实现文章服务关联、发布和响应映射

**Files:**

- Modify: `server/TinyLang/Services/ArticleService.cs`
- Test: `server/TinyLang.UnitTests/ArticleServiceTests.cs`
- Test: `server/TinyLang.UnitTests/ArticleEndpointTests.cs`

- [ ] **Step 1: 写创建、更新和解除关联测试。**

测试至少覆盖：

```csharp
[Theory]
[InlineData(AudioResourceStatus.Uploading)]
[InlineData(AudioResourceStatus.Queued)]
[InlineData(AudioResourceStatus.Processing)]
[InlineData(AudioResourceStatus.Ready)]
[InlineData(AudioResourceStatus.Failed)]
public async Task DraftShouldAcceptReadingAudioInEveryLifecycleState(
    AudioResourceStatus status)
{
    // Arrange: 创建管理员、源 MediaResource 和指定状态的 AudioResource。
    // Act: CreateDraftAsync(... ReadingAudioResourceId = audio.Id)。
    // Assert: 保存并返回相同音频 ID、名称和状态。
}
```

另增加：

- 更新文章可从音频 A 切换到音频 B。
- `ReadingAudioResourceId = null` 会解除关联，但不会删除 `AudioResource`。
- 随机或空 GUID 返回 `ArticleReadingAudioInvalid`。
- 无音频文章仍保持原有行为。

- [ ] **Step 2: 写发布不等待音频处理的失败测试。**

```csharp
[Theory]
[InlineData(AudioResourceStatus.Uploading)]
[InlineData(AudioResourceStatus.Processing)]
[InlineData(AudioResourceStatus.Failed)]
public async Task PublishShouldNotRequireReadyReadingAudio(
    AudioResourceStatus status)
{
    // Arrange: 文章已关联指定状态音频并满足分类、Markdown 和图片要求。
    // Act: PublishAsync。
    // Assert: ArticleStatus.Published，ReadingAudio 仍为原资源。
}
```

- [ ] **Step 3: 写 endpoint 响应契约测试。**

管理员详情应返回：

```json
{
  "readingAudio": {
    "id": "...",
    "name": "lesson.mp3",
    "status": "Processing",
    "durationSeconds": null,
    "lastFailureCode": null
  }
}
```

公开详情只返回 `readingAudioResourceId`，不返回对象名、输出路径或上传信息。

- [ ] **Step 4: 运行测试确认服务尚未保存关联。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~ArticleServiceTests|FullyQualifiedName~ArticleEndpointTests"
```

Expected: FAIL，响应和持久化结果缺少朗读音频。

- [ ] **Step 5: 实现存在性验证。**

在 `ValidateDraftInputAsync` 中调用：

```csharp
private async Task EnsureReadingAudioExistsAsync(
    Guid? audioResourceId,
    CancellationToken cancellationToken)
{
    if (audioResourceId is null)
    {
        return;
    }
    if (audioResourceId == Guid.Empty ||
        !await db.AudioResources.AsNoTracking().AnyAsync(
            value => value.Id == audioResourceId,
            cancellationToken))
    {
        throw NotFoundException.Create(ErrorCodes.ArticleReadingAudioInvalid);
    }
}
```

只验证存在性，不读取或限制 `AudioResourceStatus`。

- [ ] **Step 6: 保存和查询关联。**

创建和更新时分别设置：

```csharp
ReadingAudioResourceId = request.ReadingAudioResourceId
```

```csharp
article.ReadingAudioResourceId = request.ReadingAudioResourceId;
```

`DetailsQuery()` 增加 `.Include(value => value.ReadingAudioResource)`；发布逻辑不得新增 Ready 检查。

- [ ] **Step 7: 映射管理员和公开响应。**

管理员映射：

```csharp
article.ReadingAudioResource is null
    ? null
    : new ArticleReadingAudioResponse(
        article.ReadingAudioResource.Id,
        article.ReadingAudioResource.Name,
        article.ReadingAudioResource.Status,
        article.ReadingAudioResource.DurationSeconds,
        article.ReadingAudioResource.LastFailureCode)
```

公开映射只传递 `article.ReadingAudioResourceId`。

- [ ] **Step 8: 运行文章后端测试。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~Article"
```

Expected: PASS。

- [ ] **Step 9: 提交 Task 2。**

```bash
git add server/TinyLang/Services/ArticleService.cs server/TinyLang.UnitTests/ArticleServiceTests.cs server/TinyLang.UnitTests/ArticleEndpointTests.cs
git commit -m "feat(server): associate reading audio with articles"
```

## Task 3: 创建文章朗读音频 migration

**Files:**

- Create: `server/TinyLang/Database/Migrations/<timestamp>_AddArticleReadingAudio.cs`
- Create: `server/TinyLang/Database/Migrations/<timestamp>_AddArticleReadingAudio.Designer.cs`
- Modify: `server/TinyLang/Database/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: 生成 migration。**

Run:

```bash
dotnet ef migrations add AddArticleReadingAudio --project server/TinyLang/TinyLang.csproj --startup-project server/TinyLang/TinyLang.csproj --output-dir Database/Migrations
```

Expected: 只向 `articles` 增加 `reading_audio_resource_id`、索引及指向 `audio_resources.id` 的 Restrict FK。

- [ ] **Step 2: 检查 Up/Down。**

`Up` 必须等价于：

```csharp
migrationBuilder.AddColumn<Guid>(
    name: "reading_audio_resource_id",
    table: "articles",
    type: "uuid",
    nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_articles_reading_audio_resource_id",
    table: "articles",
    column: "reading_audio_resource_id");

migrationBuilder.AddForeignKey(
    name: "FK_articles_audio_resources_reading_audio_resource_id",
    table: "articles",
    column: "reading_audio_resource_id",
    principalTable: "audio_resources",
    principalColumn: "id",
    onDelete: ReferentialAction.Restrict);
```

`Down` 按 FK、索引、列的顺序删除。不得修改文章图片、音频处理任务或旧 migration。

- [ ] **Step 3: 应用 migration 并检查数据库。**

Run:

```bash
dotnet ef database update --project server/TinyLang/TinyLang.csproj --startup-project server/TinyLang/TinyLang.csproj
```

Expected: 已有文章保持 `NULL`，新 FK 和索引存在。

- [ ] **Step 4: 提交 Task 3。**

```bash
git add server/TinyLang/Database/Migrations
git commit -m "feat(server): add article reading audio migration"
```

## Task 4: 让共享上传控件支持后台处理模式

**Files:**

- Modify: `admin/src/features/audio/AudioUploadControl.jsx`
- Modify: `admin/src/features/audio/AudioUploadControl.test.jsx`

- [ ] **Step 1: 写文章模式上传测试。**

```jsx
it("returns after upload confirmation without waiting for Ready", async () => {
  const onStarted = vi.fn();
  const onCompleted = vi.fn();
  render(
    <AudioUploadControl
      waitForProcessing={false}
      onStarted={onStarted}
      onCompleted={onCompleted}
    />,
  );

  // 选择文件并上传。
  // 断言 onStarted 在对象存储 PUT 前收到 audioResourceId 和完整文件名。
  // 断言 confirm 后只查询一次资源并以 Queued/Processing 完成。
  // 断言不继续轮询 Ready。
});
```

保留现有默认模式测试，确认音频库仍等待 `Ready/Failed`。

- [ ] **Step 2: 运行测试确认当前控件仍持续轮询。**

Run:

```bash
pnpm --dir admin test AudioUploadControl
```

Expected: 新测试 FAIL，当前实现会继续调用详情接口等待终态。

- [ ] **Step 3: 增加模式参数和完成状态。**

组件签名改为：

```jsx
export function AudioUploadControl({
  resource = null,
  waitForProcessing = true,
  onStarted,
  onCompleted,
})
```

初始化成功后的回调携带 ID 和本地完整文件名：

```jsx
onStarted?.(initialized.audioResourceId, file.name);
```

现有音频库使用的无参数回调可以忽略这两个参数，不需要修改行为。

把现有局部轮询函数从 `waitForProcessing` 重命名为 `waitForProcessingResult`，避免与布尔参数同名。

确认上传后：

```jsx
requestRef.current = confirmUpload(initialized.audioResourceId);
await requestRef.current.unwrap();

if (!waitForProcessing) {
  requestRef.current = getAudioResource(initialized.audioResourceId, false);
  const audio = await requestRef.current.unwrap();
  transitionTo(audio.status === "Failed" ? "failed" : "queued");
  finishUpload(audio);
  return;
}

transitionTo("processing");
const audio = await waitForProcessingResult(
  initialized.audioResourceId,
  controller.signal,
);
```

把清理文件、进度和 `onCompleted` 提取为局部 `finishUpload(audio)`，避免两种模式复制逻辑。`queued` 文案为“源文件上传完成，后台处理中”。

- [ ] **Step 4: 运行上传控件测试。**

Run:

```bash
pnpm --dir admin test AudioUploadControl
```

Expected: PASS。

- [ ] **Step 5: 提交 Task 4。**

```bash
git add admin/src/features/audio/AudioUploadControl.jsx admin/src/features/audio/AudioUploadControl.test.jsx
git commit -m "refactor(admin): support background audio processing"
```

## Task 5: 构建可复用音频选择器和文章朗读控件

**Files:**

- Create: `admin/src/features/audio/AudioResourcePickerDialog.jsx`
- Create: `admin/src/features/audio/AudioResourcePickerDialog.test.jsx`
- Create: `admin/src/features/articles/ArticleReadingAudioControl.jsx`
- Create: `admin/src/features/articles/ArticleReadingAudioControl.test.jsx`
- Modify: `admin/src/services/audioContracts.js`
- Modify: `admin/src/features/audio/AudioTable.jsx`

- [ ] **Step 1: 将状态标签移到音频契约模块。**

```js
export const AUDIO_STATUS_LABELS = Object.freeze({
  Uploading: "上传中",
  Queued: "等待处理",
  Processing: "处理中",
  Ready: "可播放",
  Failed: "处理失败",
});
```

`AudioTable` 和新选择器统一导入该映射，避免选择器依赖管理表格组件。

- [ ] **Step 2: 写选择器失败测试。**

覆盖：

- 名称搜索、状态筛选和分页继续调用 `getAdminAudioResources`。
- 五种状态都可以选中，不能只显示 `Ready`。
- 当前资源有明确选中状态。
- 确认后调用 `onSelect(audio)`，关闭不会修改选择。

```jsx
await user.click(screen.getByRole("radio", { name: /lesson\.mp3/ }));
await user.click(screen.getByRole("button", { name: "确认选择" }));
expect(onSelect).toHaveBeenCalledWith(
  expect.objectContaining({ id: AUDIO_ID, status: "Processing" }),
);
```

- [ ] **Step 3: 实现 `AudioResourcePickerDialog`。**

使用现有 `Dialog`、`Input`、`Select` 和分页按钮；每一行使用 radio 选择，展示完整文件名和状态。不得在选择器内提供重命名、删除、重处理或播放等管理动作。

- [ ] **Step 4: 写文章朗读控件失败测试。**

覆盖：

- 选择已有资源后立即调用 `onChange(audio)`。
- 本地上传初始化时，`onStarted(id, originalName)` 立即写入包含 ID、文件名和 `Uploading` 状态的表单值，不等待 `Ready`。
- 上传确认后以服务端返回的 Queued/Processing 摘要更新显示。
- 点击“解除关联”只调用 `onChange(null)`，不调用音频删除 API。
- 当前音频状态为 Failed 时仍显示已关联，并提示可到音频资源库处理。

- [ ] **Step 5: 实现 `ArticleReadingAudioControl`。**

组件接口：

```jsx
export function ArticleReadingAudioControl({ value, onChange, disabled })
```

界面包含当前资源名称、状态标签、“从资源库选择”、“上传新音频”和“解除关联”。本地上传区域使用：

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
  onCompleted={(audio) => onChange(audio)}
/>
```

不得自动删除被替换或解除关联的音频资源。

- [ ] **Step 6: 运行组件测试。**

Run:

```bash
pnpm --dir admin test AudioResourcePickerDialog ArticleReadingAudioControl AudioUploadControl
```

Expected: PASS。

- [ ] **Step 7: 提交 Task 5。**

```bash
git add admin/src/features/audio admin/src/features/articles/ArticleReadingAudioControl.jsx admin/src/features/articles/ArticleReadingAudioControl.test.jsx admin/src/services/audioContracts.js
git commit -m "feat(admin): add reusable audio resource picker"
```

## Task 6: 接入文章编辑器和管理端 API 契约

**Files:**

- Modify: `admin/src/pages/ArticleEditor.jsx`
- Modify: `admin/src/pages/ArticleEditor.test.jsx`
- Modify: `admin/src/services/articleContracts.js`
- Modify: `admin/src/services/articlesApi.test.js`
- Modify: `admin/src/test/http.js`

- [ ] **Step 1: 写文章 normalizer 和请求体测试。**

管理员响应 normalizer 必须接受 `readingAudio: null` 或严格摘要：

```js
readingAudio: {
  id: AUDIO_ID,
  name: "lesson.mp3",
  status: "Processing",
  durationSeconds: null,
  lastFailureCode: null,
}
```

创建和更新请求体必须包含：

```js
readingAudioResourceId: form.readingAudio?.id ?? null;
```

- [ ] **Step 2: 写编辑器用户流程测试。**

至少覆盖：

- 新建文章不选择音频时发送 `readingAudioResourceId: null`。
- 从资源库选择 Processing 音频后可立即保存。
- 上传初始化回调产生 ID 后可立即保存，不等待处理完成。
- 编辑现有文章可以替换或解除关联。
- 已关联音频未 Ready 不阻止发布动作。

- [ ] **Step 3: 运行测试确认字段尚未进入表单。**

Run:

```bash
pnpm --dir admin test ArticleEditor articlesApi articleContracts
```

Expected: FAIL，请求体或 normalizer 缺少朗读音频。

- [ ] **Step 4: 更新表单状态和 payload。**

`emptyForm()` 增加：

```js
readingAudio: null,
```

`formFromArticle(article)` 增加：

```js
readingAudio: article.readingAudio,
```

`toPayload(form, concurrencyStamp)` 增加：

```js
readingAudioResourceId: form.readingAudio?.id ?? null,
```

该字段自动参与现有 dirty/baseline 比较和未保存变更保护。

- [ ] **Step 5: 在文章编辑器中加入朗读音频区域。**

将控件放在标题/摘要和正文编辑区域之间的独立全宽区段：

```jsx
<ArticleReadingAudioControl
  value={form.readingAudio}
  disabled={readOnly || pending}
  onChange={(readingAudio) => {
    setForm((current) => ({ ...current, readingAudio }));
    setFormError(null);
  }}
/>
```

不得把音频控件嵌入封面图片控件，也不得在文章导航中新增音频子页面。

- [ ] **Step 6: 运行管理端文章测试。**

Run:

```bash
pnpm --dir admin test ArticleEditor articlesApi articleContracts
```

Expected: PASS。

- [ ] **Step 7: 提交 Task 6。**

```bash
git add admin/src/pages/ArticleEditor.jsx admin/src/pages/ArticleEditor.test.jsx admin/src/services/articleContracts.js admin/src/services/articlesApi.test.js admin/src/test/http.js
git commit -m "feat(admin): manage article reading audio"
```

## Task 7: 在用户端文章详情页提供朗读播放

**Files:**

- Create: `app/src/features/audio/audioPlayback.ts`
- Create: `app/src/features/audio/audioPlayback.test.ts`
- Create: `app/src/features/audio/AudioPlaybackButton.tsx`
- Create: `app/src/features/audio/AudioPlaybackButton.test.tsx`
- Modify: `app/src/features/articles/articleTypes.ts`
- Modify: `app/src/features/articles/articleApi.test.ts`
- Modify: `app/src/pages/ArticleDetailPage.tsx`
- Modify: `app/src/pages/ArticleDetailPage.test.tsx`

- [ ] **Step 1: 写播放请求契约测试。**

```ts
it("requests the shared audio playback endpoint without caching the URL", async () => {
  const playback = await requestAudioPlayback(AUDIO_ID);

  expect(request).toMatchObject({
    url: `/audio/${AUDIO_ID}/playback`,
    method: "POST",
  });
  expect(playback).toEqual({
    url: "https://media.example/audio.mp3",
    expiresAt: null,
    durationSeconds: 12.5,
  });
});
```

- [ ] **Step 2: 实现共享播放请求。**

```ts
export interface AudioPlayback {
  url: string;
  expiresAt: string | null;
  durationSeconds: number;
}

export async function requestAudioPlayback(
  audioResourceId: string,
  signal?: AbortSignal,
) {
  const response = await httpClient.post<AudioPlayback>(
    `/audio/${audioResourceId}/playback`,
    undefined,
    { signal },
  );
  return response.data;
}
```

短期 URL 保持在组件局部状态，不进入 Redux cache。

- [ ] **Step 3: 写播放控件状态测试。**

覆盖：

- 首次点击请求播放 URL 并调用 `Audio.play()`。
- 播放中点击会暂停。
- `ended` 后恢复为“播放朗读”。
- 卸载或文章 ID 变化时中止请求、暂停并释放 Audio 实例。
- `AudioNotReady` 显示“音频暂时不可用，请稍后再试”。
- 其他网络/播放失败显示安全通用错误，不暴露后端 detail。

- [ ] **Step 4: 实现 `AudioPlaybackButton`。**

```tsx
interface AudioPlaybackButtonProps {
  audioResourceId: string;
  label?: string;
}
```

使用 Lucide `Volume2`、`Pause` 和 loading spinner；按钮文字为“播放朗读”/“暂停朗读”。用 `toApiRequestError` 读取稳定错误码：

```ts
const apiError = toApiRequestError(error, "音频加载失败，请重试。");
setError(
  apiError.code === "AudioNotReady"
    ? "音频暂时不可用，请稍后再试。"
    : apiError.message,
);
```

- [ ] **Step 5: 扩展公开文章类型和详情页。**

`PublicArticle` 增加：

```ts
readingAudioResourceId: string | null;
```

在文章标题和作者/日期元数据区域之间渲染：

```tsx
{
  article.readingAudioResourceId ? (
    <AudioPlaybackButton
      audioResourceId={article.readingAudioResourceId}
      label="文章朗读"
    />
  ) : null;
}
```

没有关联时不显示空占位；不得自动播放。

- [ ] **Step 6: 运行用户端文章和音频测试。**

Run:

```bash
pnpm --dir app test AudioPlaybackButton audioPlayback ArticleDetailPage articleApi
```

Expected: PASS。

- [ ] **Step 7: 提交 Task 7。**

```bash
git add app/src/features/audio app/src/features/articles/articleTypes.ts app/src/features/articles/articleApi.test.ts app/src/pages/ArticleDetailPage.tsx app/src/pages/ArticleDetailPage.test.tsx
git commit -m "feat(app): play article reading audio"
```

## Task 8: OpenAPI、回归测试和范围收尾

**Files:**

- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs`
- Modify: `server/TinyLang.UnitTests/AudioResourceServiceTests.cs`（仅补引用保护覆盖时）
- Modify: `admin/README.md`（仅在需要补充文章音频测试命令时）

- [ ] **Step 1: 冻结 OpenAPI 文章音频契约。**

增加结构化断言：

- `CreateArticleRequest`、`UpdateArticleRequest` 有可空 `readingAudioResourceId`。
- `AdminArticleResponse` 引用 `ArticleReadingAudioResponse`。
- `PublicArticleResponse` 有可空 `readingAudioResourceId`。
- `ArticleReadingAudioResponse.status` 引用五状态 `AudioResourceStatus`。
- 不新增 `/api/admin/articles/**/audio/upload`、文章专属播放 endpoint 或对象存储字段。

- [ ] **Step 2: 验证删除引用保护。**

模型测试确认 Restrict FK；如测试基础设施能够稳定模拟数据库 FK violation，再在 `AudioResourceServiceTests` 增加文章引用导致 `AudioInUse` 的测试。不得为通过 InMemory 测试而在 `AudioResource` 中增加文章反向集合或服务层硬编码文章查询。

- [ ] **Step 3: 运行后端全量测试和构建。**

Run:

```bash
dotnet test server/tiny-lang.slnx
dotnet build server/tiny-lang.slnx --no-restore
```

Expected: 所有可发现测试通过，构建 0 error。

- [ ] **Step 4: 运行管理端全量验证。**

Run:

```bash
pnpm --dir admin test -- --run
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: 全部通过。

- [ ] **Step 5: 运行用户端全量验证。**

Run:

```bash
pnpm --dir app test -- --run
pnpm --dir app lint
pnpm --dir app build
```

Expected: 全部通过。

- [ ] **Step 6: 做最终范围扫描。**

Run:

```bash
rg -n "ReadingAudioResourceId|readingAudioResourceId|ArticleReadingAudio" server/TinyLang server/TinyLang.UnitTests admin/src app/src
rg -n "articles/.*/audio|ArticleAudioUpload|ReadingAudio.*ObjectName" server/TinyLang admin/src app/src
```

Expected:

- 第一条匹配只出现在文章实体、DTO、服务、migration、管理端表单、用户端详情和对应测试。
- 第二条无匹配；上传和播放继续使用共享音频 endpoint。
- 没有听写、逐句同步、自动播放或多个朗读音频字段。

- [ ] **Step 7: 检查工作区并提交 Task 8。**

Run:

```bash
git diff --check
git status --short
```

提交：

```bash
git add server/TinyLang.UnitTests/OpenApiContractTests.cs server/TinyLang.UnitTests/AudioResourceServiceTests.cs admin/README.md
git commit -m "test: complete article reading audio regression"
```

只添加实际发生修改的文件，不得把 `article.md`、其他未跟踪文档或构建产物误加入提交。

## 最终验收清单

- [ ] 每篇文章最多有一个可空 `ReadingAudioResourceId`。
- [ ] 管理员可以选择任何状态的共享音频资源。
- [ ] 本地上传在初始化后立即产生可保存的文章关联。
- [ ] 浏览器源文件确认完成后，文章编辑器不等待后台编解码。
- [ ] 音频未 Ready 或 Failed 不阻止文章保存和发布。
- [ ] 公开文章只暴露朗读音频 ID，不暴露音频管理或对象存储信息。
- [ ] 登录用户点击播放才请求短期 URL，非 Ready 有明确提示。
- [ ] 解除关联和删除文章不删除共享音频。
- [ ] 被文章引用的音频不能删除，并返回 `AudioInUse`。
- [ ] 原有文章图片、Markdown、分类、发布和并发控制行为保持通过。
- [ ] 没有提前实现听写、逐句同步、自动播放或多音频功能。
