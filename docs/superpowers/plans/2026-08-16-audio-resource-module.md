# 独立音频资源模块实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有与单词、例句耦合的 `AudioClip` 重构为可被未来文章朗读和听写业务共享的独立 `AudioResource` 模块，并保留管理员异步上传处理、登录用户播放和通用对象存储能力。

**Architecture:** `AudioResource` 是只保存媒体源、处理状态和输出元数据的聚合根，不保存文章、单词或例句反向集合。管理员通过音频专用上传 API 在上传开始时创建资源，复用 `MediaResource`/简单 PUT/Multipart 基础设施；确认后由现有 dispatcher、RabbitMQ consumer 和 ffprobe/ffmpeg worker 完成幂等处理。业务内容本阶段不增加音频字段，单词和例句的旧音频关系及管理端控件移除；登录用户仅能按 ID 获取 `Ready` 音频的短期播放地址。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/PostgreSQL、MassTransit/RabbitMQ、S3-compatible object storage、ffprobe/ffmpeg、React、React Router、Redux Toolkit Query、Vitest/React Testing Library、xUnit/FluentAssertions。

---

## 文件地图

后续任务只修改下列职责边界内的文件；历史 migration 保留不改。

- 创建 `server/TinyLang/Entities/AudioResource.cs`、`server/TinyLang/Entities/Enums/AudioResourceStatus.cs`、`server/TinyLang/Database/Configurations/AudioResourceConfiguration.cs`：独立音频聚合、状态和 EF 映射。
- 修改 `server/TinyLang/Entities/AudioProcessingJob.cs`、`server/TinyLang/Database/Configurations/AudioProcessingJobConfiguration.cs`、`server/TinyLang/Database/ApplicationDbContext.cs`、`server/TinyLang/Interfaces/IApplicationDbContext.cs`：处理任务改指向 `AudioResource`。
- 修改/重命名 `server/TinyLang/Dtos/AudioDtos.cs`、`server/TinyLang/Dtos/AudioValidators.cs`、`server/TinyLang/Exceptions/ErrorCodes.cs`、`server/TinyLang/Endpoints/AudioEndpoints.cs`、`server/TinyLang/Services/AudioClipService.cs`、`server/TinyLang/Services/IAudioClipService.cs`：新的音频资源契约、服务和路由。
- 修改 `server/TinyLang/Endpoints/UploadEndpoints.cs`、`server/TinyLang/Services/IMediaResourceService.cs`、`server/TinyLang/Services/MediaResourceService.cs` 及对应模型/验证器：让音频初始化流程能同时创建 `MediaResource` 和 `AudioResource`，但不破坏头像、文章图片和视频上传。
- 修改 `server/TinyLang/Services/AudioProcessingService.cs`、`server/TinyLang/Services/IAudioProcessingService.cs`、`server/TinyLang/Services/AudioProcessingDispatcher.cs`、`server/TinyLang/Workers/AudioProcessingWorker.cs`、`server/TinyLang/Infrastructure/MassTransitAudioProcessingQueue.cs`：切换到新聚合和状态机。
- 修改 `server/TinyLang/Entities/WordPronunciation.cs`、`server/TinyLang/Entities/ExampleSentence.cs`、`server/TinyLang/Dtos/WordDtos.cs`、`server/TinyLang/Dtos/WordValidators.cs`、`server/TinyLang/Services/WordService.cs`、`server/TinyLang/Services/WordStudyService.cs`、`server/TinyLang/Policies/WordVisibilityPolicy.cs` 及相关测试：移除单词/例句音频能力，保留词条、释义、例句文本和学习流程。
- 创建新的 EF migration `server/TinyLang/Database/Migrations/20260816170000_RebuildAudioResourceModule.cs` 及对应 Designer/Snapshot 更新：删除旧音频结构并创建新表/索引/FK；若 EF CLI 生成的时间戳不同，以实际生成的同名 migration 文件为准。
- 创建 `admin/src/services/audioApi.js`、`admin/src/services/audioContracts.js`、`admin/src/pages/AudioLibrary.jsx`、`admin/src/features/audio/AudioUploadControl.jsx`、`admin/src/features/audio/AudioTable.jsx`：独立管理端音频库。
- 修改 `admin/src/router/index.jsx`、`admin/src/router/navigation.js`、`admin/src/services/wordsApi.js`、`admin/src/services/wordContracts.js`、`admin/src/pages/WordEditor.jsx` 及删除 `admin/src/features/words/WordAudioPicker.jsx`、`admin/src/features/words/WordAudioUploadControl.jsx`：移除单词专用音频 UI/API。
- 修改 `server/TinyLang.UnitTests/AudioClipServiceTests.cs`、`AudioEndpointTests.cs`、`AudioProcessingServiceTests.cs`、`AudioProcessingMessagingTests.cs`、`AudioValidatorsTests.cs`、`Word*Tests.cs`、`OpenApiContractTests.cs` 和新增/重命名契约测试；修改 `admin/src/features/words/*.test.jsx`、`admin/src/services/wordsApi.test.js` 并新增音频库测试。

## Task 1: 建立 `AudioResource` 领域模型和公共错误契约

> 执行边界：为保持每个中间任务可构建，旧 `AudioClip` 类型、DbSet 和处理任务关系在本任务暂时保留为过渡兼容层；Task 2、Task 4 和 Task 6 完成服务/worker/单词切换后必须统一删除，最终 OpenAPI、编译和引用扫描不得再出现旧运行时代码。

**Files:**
- Create: `server/TinyLang/Entities/AudioResource.cs`
- Create: `server/TinyLang/Entities/Enums/AudioResourceStatus.cs`
- Create: `server/TinyLang/Database/Configurations/AudioResourceConfiguration.cs`
- Modify: `server/TinyLang/Entities/AudioProcessingJob.cs`
- Modify: `server/TinyLang/Database/Configurations/AudioProcessingJobConfiguration.cs`
- Modify: `server/TinyLang/Database/ApplicationDbContext.cs`
- Modify: `server/TinyLang/Interfaces/IApplicationDbContext.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Delete: `server/TinyLang/Entities/AudioClip.cs`, `server/TinyLang/Entities/Enums/AudioClipKind.cs`, `server/TinyLang/Entities/Enums/AudioPublicationStatus.cs`

- [ ] **Step 1: 先写领域模型测试，锁定状态和名称规范化行为。**

```csharp
[Fact]
public void NewResourceShouldStartUploadingWithNormalizedName()
{
    var resource = AudioResource.Create(Guid.NewGuid(), "Lesson.MP3", Guid.NewGuid());

    resource.Status.Should().Be(AudioResourceStatus.Uploading);
    resource.Name.Should().Be("Lesson.MP3");
    resource.NormalizedName.Should().Be("lesson.mp3");
}
```

测试文件为 `server/TinyLang.UnitTests/AudioResourceModelTests.cs`。

- [ ] **Step 2: 运行测试确认类型尚不存在。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~AudioResourceModelTests`

Expected: FAIL，提示 `AudioResource` 或 `AudioResourceStatus` 未定义。

- [ ] **Step 3: 实现新实体和状态枚举。**

实体必须包含 `Id`、审计用户/时间、`Name`、`NormalizedName`、`SourceMediaResourceId`、`Status`、处理元数据、`CurrentOutputVersion`、`OutputObjectName`、`LastFailureCode`、`ConcurrencyStamp`。状态只允许 `Uploading`、`Queued`、`Processing`、`Ready`、`Failed`；提供以下工厂和规范化实现：

```csharp
public static AudioResource Create(Guid adminId, string name, Guid sourceMediaResourceId)
{
    var displayName = name.Trim();
    if (displayName.Length == 0) throw new ArgumentException("Audio name is required.", nameof(name));
    return new AudioResource
    {
        CreatedById = adminId,
        LastEditorId = adminId,
        Name = displayName,
        NormalizedName = NormalizeName(displayName),
        SourceMediaResourceId = sourceMediaResourceId,
        Status = AudioResourceStatus.Uploading
    };
}

public static string NormalizeName(string value)
    => value.Trim().ToUpperInvariant();
```

`AudioProcessingJob` 将 `AudioClipId/AudioClip` 改为 `AudioResourceId/AudioResource`，处理任务的状态枚举继续沿用现有 job 状态。

- [ ] **Step 4: 更新 EF 配置、DbContext 和错误码。**

为 `Name` 建普通显示列，为 `NormalizedName` 建全局唯一索引；`SourceMediaResourceId` 对 `MediaResource` 使用 Restrict；处理任务对 `AudioResource` 使用 Cascade。`IApplicationDbContext` 暴露 `DbSet<AudioResource> AudioResources`，删除 `AudioClips`。错误码新增/保留并映射：`AudioNameConflict`、`AudioNotFound`、`AudioInUse`、`AudioUploadIncomplete`、`AudioNotReady`、`AudioProcessingFailed`、`AudioStatusConflict`，删除发布、下架、语言和用途专用错误。

- [ ] **Step 5: 运行领域和编译测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~AudioResourceModelTests`

Expected: PASS。

## Task 2: 重写 DTO、验证器和音频资源服务契约

**Files:**
- Modify: `server/TinyLang/Dtos/AudioDtos.cs`
- Modify: `server/TinyLang/Dtos/AudioValidators.cs`
- Rename: `server/TinyLang/Services/IAudioClipService.cs` -> `server/TinyLang/Services/IAudioResourceService.cs`
- Rename: `server/TinyLang/Services/AudioClipService.cs` -> `server/TinyLang/Services/AudioResourceService.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Modify: `server/TinyLang/Endpoints/AudioEndpoints.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodeExtension.cs`、`server/TinyLang/Middlewares/GlobalExceptionHandler.cs`（仅在错误映射需要时）

- [ ] **Step 1: 写 DTO 验证测试。**

在 `server/TinyLang.UnitTests/AudioValidatorsTests.cs` 添加以下契约断言：空文件名、超过 200 字符、控制字符、非法状态筛选失败；合法扩展名完整文件名通过；播放请求不接受标题、类型或语言字段。

```csharp
[Fact]
public async Task RenameShouldRejectBlankOrControlCharacters()
{
    var validator = new RenameAudioResourceRequestValidator();
    (await validator.ValidateAsync(new RenameAudioResourceRequest { Name = " " })).IsValid.Should().BeFalse();
    (await validator.ValidateAsync(new RenameAudioResourceRequest { Name = "lesson\n.mp3" })).IsValid.Should().BeFalse();
    (await validator.ValidateAsync(new RenameAudioResourceRequest { Name = "lesson (2).mp3" })).IsValid.Should().BeTrue();
}
```

- [ ] **Step 2: 定义新 DTO。**

使用 `AudioResourceStatus? Status` 作为列表筛选；列表/详情响应只返回 `Id`、`Name`、`Status`、处理元数据、失败码、审计时间和 `ConcurrencyStamp`。定义 `InitializeAudioUploadRequest`（原始文件名、扩展名、大小、ContentType、上传方式）、`AudioUploadInitializationResponse`（AudioResourceId、MediaResourceId、预签名/Multipart 应用信息）、`RenameAudioResourceRequest`、`AudioResourcePlaybackResponse`。不要在任何 DTO 中出现 `AudioClipKind`、`AudioPublicationStatus`、`PublishedAt`、对象存储路径或 provider upload ID。

- [ ] **Step 3: 定义服务接口和状态操作。**

`IAudioResourceService` 至少提供：

```csharp
Task<AudioUploadInitializationResponse> InitializeSimpleUploadAsync(Guid adminId, InitializeAudioUploadRequest request, CancellationToken ct = default);
Task<AudioUploadInitializationResponse> InitializeMultipartUploadAsync(Guid adminId, InitializeAudioUploadRequest request, CancellationToken ct = default);
Task ConfirmUploadAsync(Guid audioResourceId, Guid adminId, CancellationToken ct = default);
Task<PagedResponse<AdminAudioResourceListItemResponse>> GetAdminListAsync(AdminAudioResourceListRequest request, CancellationToken ct = default);
Task<AdminAudioResourceResponse> GetAdminByIdAsync(Guid id, CancellationToken ct = default);
Task<AdminAudioResourceResponse> RenameAsync(Guid id, Guid adminId, RenameAudioResourceRequest request, CancellationToken ct = default);
Task<AdminAudioResourceResponse> RetryUploadAsync(Guid id, Guid adminId, InitializeAudioUploadRequest request, CancellationToken ct = default);
Task<AdminAudioResourceResponse> ReprocessAsync(Guid id, Guid adminId, CancellationToken ct = default);
Task DeleteAsync(Guid id, CancellationToken ct = default);
Task<AudioResourcePlaybackResponse> GetPlaybackAsync(Guid id, CancellationToken ct = default);
```

- [ ] **Step 4: 先让验证和契约测试失败，再运行。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~AudioValidatorsTests`

Expected: FAIL，直到 DTO/validator 实现完成。

- [ ] **Step 5: 实现验证器、服务注册和基础异常映射。**

服务层所有名称比较使用 `NormalizedName`；重命名冲突返回 `AudioNameConflict`，并发唯一索引冲突重新读取候选名后重试；其他状态冲突统一映射 `AudioStatusConflict`。`Failed -> Queued` 只允许已有源对象，`Failed -> Uploading` 由重新上传流程处理。

- [ ] **Step 6: 运行服务契约测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~AudioValidatorsTests`

Expected: PASS。

## Task 3: 接入两阶段简单上传和 Multipart 上传

**Files:**
- Modify: `server/TinyLang/Endpoints/UploadEndpoints.cs`
- Modify: `server/TinyLang/Services/IMediaResourceService.cs`
- Modify: `server/TinyLang/Services/MediaResourceService.cs`
- Modify: `server/TinyLang/Models/MediaResourcePresignResult.cs`、`server/TinyLang/Models/MultipartUploadResults.cs`
- Modify: `server/TinyLang/Policies/MediaUploadPolicy.cs`
- Modify: `server/TinyLang/Dtos/MediaResourceDtos.cs`、`server/TinyLang/Dtos/MediaResourceValidators.cs`
- Modify: `server/TinyLang/Endpoints/AudioEndpoints.cs`

- [ ] **Step 1: 添加上传初始化/确认测试。**

在 `server/TinyLang.UnitTests/AudioEndpointTests.cs` 和 `UploadEndpointTests.cs` 添加断言：管理员才能初始化音频上传；初始化响应同时包含 `AudioResourceId` 和 `MediaResourceId`；简单 PUT 确认后状态为 `Queued` 并有一个待处理 job；Multipart 完成后同样进入 `Queued`；确认未完成对象返回 `AudioUploadIncomplete`。

- [ ] **Step 2: 定义专用路由和请求模型。**

保留头像和文章/视频通用路由不变，新增管理员路由：

```text
POST /api/admin/audio/uploads/simple
POST /api/admin/audio/uploads/multipart
PUT  /api/admin/audio/{id}/upload/confirm
POST /api/admin/audio/multipart/{sessionId}/parts/presign
GET  /api/admin/audio/multipart/{sessionId}
POST /api/admin/audio/multipart/{sessionId}/complete
DELETE /api/admin/audio/multipart/{sessionId}
```

所有初始化、分片签名、完成、终止和确认路由使用 `RequireAdmin`；登录用户不能通过这些路由创建或枚举媒体资源。确认接口调用 `IAudioResourceService.ConfirmUploadAsync`，不再要求前端先创建 `AudioClip`。

- [ ] **Step 3: 复用媒体资源存储逻辑并创建音频聚合。**

在 `MediaResourceService` 保留现有校验、对象命名、预签名、Multipart 断点续传和取消逻辑；新增一个只供音频服务调用的组合方法，事务内创建 `MediaResource(Module = Audio, Status = Pending)` 和 `AudioResource(Status = Uploading)`。对象确认成功后激活 `MediaResource`，把 `AudioResource.Status` 置为 `Queued` 并插入新的 `AudioProcessingJob`。

- [ ] **Step 4: 实现全局名称分配。**

从完整原始文件名拆分扩展名，候选顺序为 `lesson.mp3`、`lesson (2).mp3`、`lesson (3).mp3`；显示名称始终保留扩展名。服务层先查询 `NormalizedName`，保存时捕获唯一约束 `IX_audio_resources_NormalizedName` 并重新分配，最多在当前请求内继续递增，不覆盖已有资源。

- [ ] **Step 5: 运行上传测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~UploadEndpointTests|FullyQualifiedName~AudioEndpointTests"`

Expected: PASS。

## Task 4: 切换异步处理、失败和重试状态机

**Files:**
- Modify: `server/TinyLang/Services/AudioProcessingService.cs`
- Modify: `server/TinyLang/Services/IAudioProcessingService.cs`
- Modify: `server/TinyLang/Services/AudioProcessingDispatcher.cs`
- Modify: `server/TinyLang/Workers/AudioProcessingWorker.cs`
- Modify: `server/TinyLang/Workers/AudioProcessingDispatchWorker.cs`
- Modify: `server/TinyLang/Infrastructure/MassTransitAudioProcessingQueue.cs`
- Modify: `server/TinyLang/Models/AudioProcessingRequested.cs`、`server/TinyLang/Models/AudioProcessingFailure.cs`
- Modify: `server/TinyLang/Services/AudioResourceService.cs`

- [ ] **Step 1: 添加状态转换和幂等测试。**

在 `AudioProcessingServiceTests.cs` 覆盖：`Queued -> Processing -> Ready`、探测/转码异常进入 `Failed` 并记录稳定失败码、旧 output version 完成消息不能覆盖新版本、重复 RabbitMQ 消息不会再次处理、`Failed -> Queued` 重处理和 `Failed -> Uploading` 重新上传。

```csharp
[Fact]
public async Task StaleOutputVersionMustNotReplaceCurrentReadyOutput()
{
    // Arrange: current job has version B, then process a stale version A.
    // Assert: OutputObjectName and CurrentOutputVersion remain version B.
}
```

测试中的 fixture 必须实际创建两个不同 `OutputVersion` 的 `AudioProcessingJob`，不能只断言 mock 调用次数。

- [ ] **Step 2: 将 worker 查询和租约逻辑改为 `AudioResource`。**

所有 `Include(value => value.AudioClip)`、`job.AudioClip.*` 和日志键改为 `AudioResource`；输出前缀使用 `audio-resource-id` 和 `OutputVersion`。处理成功必须一次性更新探测元数据、MP3 输出对象名、`CurrentOutputVersion`、`Status = Ready` 并清除失败码；失败必须按现有可重试策略保存 `LastFailureCode`，最终状态为 `Failed`。

- [ ] **Step 3: 实现重处理和重新上传边界。**

已有有效源对象时 `ReprocessAsync` 只新建 queued job；源对象失效时返回 `AudioUploadIncomplete`，`RetryUploadAsync` 创建新的 pending `MediaResource` 并把同一个 `AudioResource` 置为 `Uploading`，旧输出不被删除直到新版本成功。旧输出清理必须限定在当前资源和 output version 前缀内。

- [ ] **Step 4: 运行处理和消息测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~AudioProcessingServiceTests|FullyQualifiedName~AudioProcessingMessagingTests"`

Expected: PASS。

## Task 5: 完成管理员 API、播放 API、重命名和引用保护

**Files:**
- Modify: `server/TinyLang/Endpoints/AudioEndpoints.cs`
- Modify: `server/TinyLang/Services/AudioResourceService.cs`
- Modify: `server/TinyLang/Dtos/AudioDtos.cs`
- Modify: `server/TinyLang.UnitTests/AudioEndpointTests.cs`
- Rename/replace: `server/TinyLang.UnitTests/AudioClipServiceTests.cs` -> `server/TinyLang.UnitTests/AudioResourceServiceTests.cs`

- [ ] **Step 1: 写 API 路由契约测试。**

管理员路由必须包含 `RequireAdmin`，播放路由必须包含 `RequireUser` 和 `RateLimitPolicies.AudioPlaybackLimit`；OpenAPI 路由中不得出现 `/publish`、`/unpublish` 或 `AudioClip` 类型。

- [ ] **Step 2: 实现管理员资源路由。**

路由如下：

```text
GET    /api/admin/audio
GET    /api/admin/audio/{id}
PATCH  /api/admin/audio/{id}/name
POST   /api/admin/audio/{id}/retry-upload
POST   /api/admin/audio/{id}/reprocess
DELETE /api/admin/audio/{id}
POST   /api/audio/{id}/playback
```

列表支持 `page`、`pageSize`、`keyword`、`status`，关键字按 `NormalizedName` 查询但响应显示原始完整文件名。

- [ ] **Step 3: 实现播放状态和短期地址。**

`GetPlaybackAsync` 只查询 `Status = Ready` 且输出元数据完整的资源；非 Ready 返回 `AudioNotReady`，不存在返回 `AudioNotFound`。返回短期 URL、过期时间和时长，不返回对象名。endpoint 设置 `Cache-Control: private, no-store`。

- [ ] **Step 4: 实现删除引用保护。**

服务层先尝试删除，数据库 FK Restrict 冲突转换为 `AudioInUse`；删除未引用资源时删除 `AudioResource`、处理 job 和其专属媒体对象。不要在 `AudioResource` 中添加消费者集合，也不要对未来文章/听写表建本轮字段。

- [ ] **Step 5: 运行 API 和服务测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~AudioResourceServiceTests|FullyQualifiedName~AudioEndpointTests|FullyQualifiedName~OpenApiContractTests"`

Expected: PASS。

## Task 6: 移除单词和例句音频耦合，保持单词模块可用

**Files:**
- Modify: `server/TinyLang/Entities/WordPronunciation.cs`
- Modify: `server/TinyLang/Entities/ExampleSentence.cs`
- Modify: `server/TinyLang/Dtos/WordDtos.cs`
- Modify: `server/TinyLang/Dtos/WordValidators.cs`
- Modify: `server/TinyLang/Services/WordService.cs`
- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Modify: `server/TinyLang/Policies/WordVisibilityPolicy.cs`
- Modify: `server/TinyLang.UnitTests/WordValidatorsTests.cs`、`WordServiceTests.cs`、`WordStudyServiceTests.cs`、`WordEndpointTests.cs`、`WordStudyModelTests.cs`

- [ ] **Step 1: 添加回归测试，确认文本词条仍可创建、编辑、发布和学习。**

将现有依赖 `AudioClipId` 的有效请求改为不含音频字段，并增加断言：创建/更新响应不含 `audioClipId`；学习响应仍包含单词、释义、例句文本和排序字段。

- [ ] **Step 2: 移除实体和 DTO 音频字段。**

删除 `WordPronunciation.AudioClipId/AudioClip`、`ExampleSentence.AudioClipId/AudioClip` 及 `WordPronunciationInput`、`ExampleSentenceInput`、响应模型中的对应属性。删除重复音频 ID、音频存在性、类型和状态验证规则。保留例句 `Sentence`、`Translation`、排序和词性等非音频字段。

- [ ] **Step 3: 清理服务查询和可见性策略。**

删除 `Include(AudioClip)`、音频 ID 校验、音频状态条件和播放字段投影。`WordVisibilityPolicy` 只判断词条发布状态和文本子项完整性；`WordStudyService` 不再为例句或发音返回音频 ID。

- [ ] **Step 4: 运行单词全量回归测试。**

Run: `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~Word"`

Expected: PASS，且编译器不再报告 `AudioClip` 引用。

## Task 7: 新建 EF Core 重建 migration

**Files:**
- Create: `server/TinyLang/Database/Migrations/20260816170000_RebuildAudioResourceModule.cs`（EF CLI 可能使用实际执行时的时间戳前缀）
- Create: `server/TinyLang/Database/Migrations/20260816170000_RebuildAudioResourceModule.Designer.cs`
- Modify: `server/TinyLang/Database/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: 先构建模型并检查差异。**

Run: `dotnet ef migrations add RebuildAudioResourceModule --project server/TinyLang/TinyLang.csproj --startup-project server/TinyLang/TinyLang.csproj --output-dir Database/Migrations`

Expected: 生成 migration，差异只涉及旧音频表/关系、单词/例句音频 FK/索引和新 `audio_resources`/job 结构；不得删除文章媒体、视频媒体或用户头像结构。

- [ ] **Step 2: 检查 migration 内容并补齐删除顺序。**

迁移先删除 `word_pronunciations`、`example_sentences` 指向旧音频的 FK/索引，再删除旧 `audio_processing_jobs` 和 `audio_clips`；随后创建 `audio_resources`、新的 processing job FK、`NormalizedName` 唯一索引和 `SourceMediaResourceId` Restrict FK。由于开发数据无保留价值，允许 `DropTable`，但 Down 必须能够恢复旧表结构以便本地回滚。

- [ ] **Step 3: 在空数据库和升级数据库上执行迁移。**

Run: `dotnet ef database update --project server/TinyLang/TinyLang.csproj --startup-project server/TinyLang/TinyLang.csproj`

Expected: migration 成功，数据库包含 `audio_resources`，旧 `audio_clips` 不存在，唯一索引和 FK 行为符合设计。

## Task 8: 构建独立管理端音频库

**Files:**
- Create: `admin/src/services/audioApi.js`
- Create: `admin/src/services/audioContracts.js`
- Create: `admin/src/pages/AudioLibrary.jsx`
- Create: `admin/src/features/audio/AudioUploadControl.jsx`
- Create: `admin/src/features/audio/AudioTable.jsx`
- Modify: `admin/src/router/index.jsx`
- Modify: `admin/src/router/navigation.js`
- Modify: `admin/src/services/baseApi.js`（增加 `AudioResource` tag）

- [ ] **Step 1: 写音频 API normalizer 和页面测试。**

创建 `admin/src/services/audioApi.test.js`、`admin/src/pages/AudioLibrary.test.jsx`，先验证列表响应规范化、状态标签、Ready 播放、重命名冲突、Failed 重试和删除冲突提示。

```js
expect(normalizeAudioResource({ name: "lesson.mp3", status: "Ready" })).toEqual(
  expect.objectContaining({ name: "lesson.mp3", status: "Ready" }),
);
```

- [ ] **Step 2: 实现 RTK Query 音频 API。**

`audioApi.js` 提供列表、详情、上传初始化/确认、Multipart 操作、重命名、重新上传、重新处理、删除和播放 mutation；所有 mutation 只失效 `{ type: "AudioResource", id: "LIST" }` 或具体资源 tag。状态显示映射为 `Uploading/Queued/Processing/Ready/Failed`，不再有发布/下架或用途筛选。

- [ ] **Step 3: 实现上传控件。**

复用 `objectStorageTransport.js` 的 PUT、进度、AbortController 和 `videoUploadApi` 中已验证的 Multipart 编排；根据能力阈值选择简单 PUT 或 Multipart。文件选择后保留完整文件名，上传初始化后立即显示 `Uploading`，确认后轮询列表直到 `Ready` 或 `Failed`。取消不删除已创建资源，失败资源显示重新上传/重新处理入口。

- [ ] **Step 4: 实现列表、搜索、筛选和操作。**

`AudioTable.jsx` 提供名称搜索、状态筛选、分页、详情元数据、Ready 试听、重命名对话框、Failed 重试/重新上传、删除确认。试听先调用 `/api/audio/{id}/playback`，非 Ready 显示“音频暂不可用”，不直接拼接对象存储地址。

- [ ] **Step 5: 接入路由和导航。**

新增 `/audio` 路由，导航 label 为“音频资源”，沿用现有 `AuthGuard`/管理员布局。不得在文章、单词或视频导航中嵌入音频库。

- [ ] **Step 6: 运行管理端测试和构建。**

Run: `cd admin && pnpm test -- --run audioApi AudioLibrary`

Expected: PASS。

Run: `cd admin && pnpm build`

Expected: 构建成功。

## Task 9: 删除单词专用管理端音频功能

**Files:**
- Modify: `admin/src/services/wordsApi.js`
- Modify: `admin/src/services/wordContracts.js`
- Modify: `admin/src/pages/WordEditor.jsx`
- Modify: `admin/src/pages/Words.jsx`
- Delete: `admin/src/features/words/WordAudioPicker.jsx`
- Delete: `admin/src/features/words/WordAudioUploadControl.jsx`
- Delete: `admin/src/features/words/WordAudioPicker.test.jsx`
- Delete: `admin/src/features/words/WordAudioUploadControl.test.jsx`
- Modify: `admin/src/pages/WordEditor.test.jsx`、`admin/src/pages/Words.test.jsx`、`admin/src/services/wordsApi.test.js`

- [ ] **Step 1: 先删除页面测试中的音频 fixture 和交互断言。**

保留词头、释义、词性、例句文本、发布/归档和分页测试；删除打开音频选择器、上传进度、发布音频、音频播放和重试音频断言。

- [ ] **Step 2: 清理 words API。**

从 `wordsApi.js` 删除 `getWordAudioOptions`、音频 capability/presign/confirm/create/publish/retry/playback endpoints 及其 hooks；从 `wordContracts.js` 删除对应 normalizer、音频字段和 `AudioClip` tag。Word mutation 的请求体只保留文本字段。

- [ ] **Step 3: 清理 WordEditor 状态和控件。**

删除 `WordAudioPicker` import、音频选择器状态、`AudioClipId` 表单字段、发音/例句音频按钮和展示；保留例句增删改、排序、折叠和未保存变更保护。

- [ ] **Step 4: 运行管理端单词回归测试。**

Run: `cd admin && pnpm test -- --run WordEditor Words wordsApi`

Expected: PASS，且 `rg -n "WordAudio|AudioClip|audioClipId" admin/src/pages admin/src/features/words admin/src/services/wordsApi.js admin/src/services/wordContracts.js` 无匹配。

## Task 10: OpenAPI、端到端回归和文档收尾

**Files:**
- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs`、`AudioEndpointTests.cs`、`UploadEndpointTests.cs`
- Modify: `admin/README.md`（仅补充音频库开发入口和测试命令）
- Modify: `docs/superpowers/specs/2026-08-16-audio-resource-module-design.md`（仅在实现与规格有已批准的术语调整时）

- [ ] **Step 1: 增加 OpenAPI 禁止旧契约断言。**

断言生成的 OpenAPI JSON 不包含 `AudioClipKind`、`AudioPublicationStatus`、`publish`、`unpublish`、单词专属音频 schema；包含 `AudioResourceStatus`、音频上传初始化、列表、重命名、重试、重新处理、删除和播放路径。

- [ ] **Step 2: 运行后端全量测试和构建。**

Run: `dotnet test server/tiny-lang.slnx`

Expected: 所有测试通过。

Run: `dotnet build server/tiny-lang.slnx --no-restore`

Expected: 构建成功且无 `AudioClip` 编译引用。

- [ ] **Step 3: 运行管理端全量测试和构建。**

Run: `cd admin && pnpm test -- --run`

Expected: 所有测试通过。

Run: `cd admin && pnpm build`

Expected: 构建成功。

- [ ] **Step 4: 做最终引用和范围扫描。**

Run: `rg -n "AudioClip|AudioClipKind|AudioPublicationStatus|WordAudio|ExampleSentence.*Audio|AudioClipId" server/TinyLang admin/src`

Expected: 仅允许视频模型中的 `AudioCodec/AudioBitrate`、迁移历史文本和与本模块无关的日志说明；不得存在旧实体、旧 endpoint、旧 DTO 或单词/例句运行时代码引用。确认文章实体没有新增朗读字段，听写模块没有提前实现。

- [ ] **Step 5: 记录开发数据库重建说明。**

在 `admin/README.md` 或现有开发文档中明确：本 migration 会删除旧音频、单词/例句音频关系和开发数据；部署前必须备份并确认只适用于已接受数据丢失的开发数据库。

## 自检清单

- [ ] 规格中的五种资源状态、名称大小写不敏感唯一和并发重名分配均有领域/服务测试。
- [ ] 上传开始即创建 `AudioResource(Uploading)`，简单 PUT 和 Multipart 确认都进入 `Queued`。
- [ ] 处理成功即 `Ready`，没有发布/下架状态；非 Ready 播放返回稳定错误。
- [ ] 只有管理员能上传、处理、重命名、重试和删除；普通登录用户只能按 ID 播放。
- [ ] 删除由服务层和 Restrict FK 双重保护，引用时返回 `AudioInUse`。
- [ ] 单词和例句音频字段、验证、播放和管理端控件已移除，单词文本功能仍通过测试。
- [ ] 没有文章朗读字段、听写业务或视频模块改造。
- [ ] migration、OpenAPI、后端测试、管理端测试和构建均有明确执行命令。
