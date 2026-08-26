# 单词背诵模块升级 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将现有基础单词会话升级为后端权威的两阶段学习系统，支持顺序新词学习、固定遗忘曲线复习、失败轮播、拼写校验、收藏、停止复习和学习统计。

**Architecture:** 继续使用统一的 `WordStudySession` / `WordStudySessionItem` 聚合，通过 `SessionType` 区分学习和复习、通过 `Phase` 区分记忆和拼写；对外提供严格分离的 learning/review API。`WordStudyService` 负责选择、概览和业务编排，新的 `WordStudySessionEngine` 负责共享队列状态机，`UserWordLibraryService` 负责收藏和停止复习列表。

**Tech Stack:** ASP.NET Core Minimal API、EF Core 10、PostgreSQL、FluentValidation、xUnit、React 19、TypeScript、Redux Toolkit Query、React Router 7、Tailwind CSS 4 / daisyUI、Vitest、Testing Library。

**提交约定：** 开始执行时创建独立 worktree；每个 Task 使用显式路径 `git add`。不得提交 `docs/`、`word-study.md` 或其他未跟踪文档，不得使用 `git add .` / `git add -A`。

---

## 文件结构与职责

### 后端领域与数据库

- `server/TinyLang/Entities/Word.cs`：增加数据库生成且不可变的 `StudyOrder`。
- `server/TinyLang/Entities/User.cs`：增加每组复习数量和收藏导航集合。
- `server/TinyLang/Entities/UserWordProgress.cs`：保存首次学习、复习阶段、到期时间、成功/失败计数和排除状态。
- `server/TinyLang/Entities/WordStudySession.cs`：保存会话类型、当前阶段和生命周期。
- `server/TinyLang/Entities/WordStudySessionItem.cs`：保存双阶段队列、尝试次数、失败标记和终态。
- `server/TinyLang/Entities/UserWordFavorite.cs`：保存用户与单词之间的收藏关系。
- `server/TinyLang/Entities/Enums/WordStudySessionType.cs`：`Learning | Review`。
- `server/TinyLang/Entities/Enums/WordStudyPhase.cs`：`Memorization | Spelling`。
- `server/TinyLang/Entities/Enums/WordStudySessionItemStatus.cs`：`Pending | Completed | Excluded | Skipped`。
- `server/TinyLang/Database/Configurations/*.cs`：实体约束、索引和删除行为。
- `server/TinyLang/Database/Migrations/<timestamp>_UpgradeWordStudyModule.cs`：清空旧学习数据并建立新模型。

### 后端业务与 API

- `server/TinyLang/Services/WordStudySchedule.cs`：固定 1/2/4/7/15/30 天调度和拼写规范化规则。
- `server/TinyLang/Services/WordStudySessionEngine.cs`：记忆后移、阶段切换、拼写后移、进度更新和会话完成。
- `server/TinyLang/Services/WordStudyService.cs`：学习/复习概览、顺序选词、到期选词和会话查询。
- `server/TinyLang/Services/IWordStudyService.cs`：学习与复习用例契约。
- `server/TinyLang/Services/UserWordLibraryService.cs`：收藏、取消收藏、停止复习列表和恢复复习。
- `server/TinyLang/Services/IUserWordLibraryService.cs`：用户词库用例契约。
- `server/TinyLang/Dtos/WordStudyDtos.cs`：会话、当前项、概览和命令 DTO。
- `server/TinyLang/Dtos/UserWordLibraryDtos.cs`：收藏与停止复习分页 DTO。
- `server/TinyLang/Endpoints/WordStudyEndpoints.cs`：分离的 learning/review 路由。
- `server/TinyLang/Endpoints/UserWordLibraryEndpoints.cs`：收藏和停止复习路由。

### 用户端

- `app/src/features/wordStudy/wordStudyTypes.ts`：新 API 的 discriminated union 类型。
- `app/src/features/wordStudy/wordStudyApi.ts`：学习、复习、收藏和排除 RTK Query endpoints。
- `app/src/features/wordStudy/WordStudyWorkspace.tsx`：共享会话工作区和阶段切换。
- `app/src/features/wordStudy/WordMemorizationCard.tsx`：完整词条记忆卡。
- `app/src/features/wordStudy/WordSpellingCard.tsx`：无答案泄露的拼写卡。
- `app/src/features/wordStudy/WordStudyCompletion.tsx`：完成摘要及下一组操作。
- `app/src/pages/WordsPage.tsx`：学习与复习首页。
- `app/src/pages/WordLearningPage.tsx`：新词学习会话容器。
- `app/src/pages/WordReviewPage.tsx`：旧词复习会话容器。
- `app/src/features/wordStudy/WordStudySettingsPanel.tsx`：学习与复习组大小设置。
- `app/src/features/wordStudy/WordStudySummaryPanel.tsx`：用户中心统计摘要。
- `app/src/features/wordStudy/WordFavoritesPanel.tsx`：分页收藏本。
- `app/src/features/wordStudy/WordReviewExclusionsPanel.tsx`：分页停止复习列表。

---

## Task 1：重建领域模型与纯业务规则

**Files:**

- Create: `server/TinyLang/Entities/UserWordFavorite.cs`
- Create: `server/TinyLang/Entities/Enums/WordStudySessionType.cs`
- Create: `server/TinyLang/Entities/Enums/WordStudyPhase.cs`
- Create: `server/TinyLang/Entities/Enums/WordMemorizationResult.cs`
- Create: `server/TinyLang/Entities/Enums/WordSpellingResult.cs`
- Create: `server/TinyLang/Services/WordStudySchedule.cs`
- Modify: `server/TinyLang/Entities/Word.cs`
- Modify: `server/TinyLang/Entities/User.cs`
- Modify: `server/TinyLang/Entities/UserWordProgress.cs`
- Modify: `server/TinyLang/Entities/WordStudySession.cs`
- Modify: `server/TinyLang/Entities/WordStudySessionItem.cs`
- Modify: `server/TinyLang/Entities/Enums/WordStudySessionStatus.cs`
- Modify: `server/TinyLang/Entities/Enums/WordStudySessionItemStatus.cs`
- Delete: `server/TinyLang/Entities/Enums/WordStudySelectionMode.cs`
- Delete: `server/TinyLang/Entities/Enums/WordStudyResult.cs`
- Test: `server/TinyLang.UnitTests/WordStudyModelTests.cs`
- Create test: `server/TinyLang.UnitTests/WordStudyScheduleTests.cs`

- [ ] **Step 1：先写领域模型和调度规则的失败测试。**

覆盖以下具体断言：

```csharp
[Fact]
public void NewProgressShouldScheduleFirstReviewAfterOneDay()
{
    var completedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z");
    var next = WordStudySchedule.AfterInitialLearning(completedAt);
    next.Stage.Should().Be(0);
    next.NextReviewAt.Should().Be(completedAt.AddDays(1));
}

[Theory]
[InlineData(0, 1, 2)]
[InlineData(1, 2, 4)]
[InlineData(2, 3, 7)]
[InlineData(3, 4, 15)]
[InlineData(4, 5, 30)]
[InlineData(5, 5, 30)]
public void SuccessfulReviewShouldAdvanceThroughFixedIntervals(
    int currentStage, int expectedStage, int expectedDays)
{
    var completedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z");
    var next = WordStudySchedule.AfterReview(
        currentStage,
        hadFailure: false,
        completedAt);
    next.Stage.Should().Be(expectedStage);
    next.NextReviewAt.Should().Be(completedAt.AddDays(expectedDays));
}

[Theory]
[InlineData("  ÉCOLE  ", "école", true)]
[InlineData("Co-Operate", "co-operate", true)]
[InlineData("coopérate", "cooperate", false)]
public void SpellingShouldIgnoreCaseAndOuterWhitespaceButKeepDiacritics(
    string answer, string headword, bool expected)
    => WordStudySchedule.IsCorrectSpelling(answer, headword)
        .Should().Be(expected);
```

同时断言实体默认状态：学习会话从 `Memorization/Active` 开始；会话项从 `Pending` 开始；用户默认 `DailyWordReviewCount = 50`；收藏实体生成非空 ID。

- [ ] **Step 2：运行测试确认 RED。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyModelTests|FullyQualifiedName~WordStudyScheduleTests"
```

Expected: FAIL，缺少新枚举、实体字段和 `WordStudySchedule`。

- [ ] **Step 3：实现新的枚举和实体形状。**

关键类型固定为：

```csharp
public enum WordStudySessionType { Learning, Review }
public enum WordStudyPhase { Memorization, Spelling }
public enum WordMemorizationResult { Remembered, Forgotten }
public enum WordSpellingResult { Correct, Incorrect }
public enum WordStudySessionStatus { Active, Completed }
public enum WordStudySessionItemStatus { Pending, Completed, Excluded, Skipped }

public sealed class WordStudySessionItem : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid WordId { get; set; }
    public int Position { get; set; }
    public WordStudySessionItemStatus Status { get; set; } =
        WordStudySessionItemStatus.Pending;
    public long MemorizationQueueOrder { get; set; }
    public int MemorizationAttemptCount { get; set; }
    public bool HadMemorizationFailure { get; set; }
    public DateTimeOffset? MemorizationPassedAt { get; set; }
    public long SpellingQueueOrder { get; set; }
    public int SpellingAttemptCount { get; set; }
    public bool HadSpellingFailure { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public WordStudySkipReason? SkipReason { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
```

`UserWordProgress` 使用设计规格中的字段，并增加 `ConcurrencyStamp`；删除旧 `LastResult`、`RememberedCount`、`ForgottenCount` 语义。`WordStudySession` 删除 `StudyDateUtc`、`IncludePreviouslyStudied`、`SelectionMode`、`AbandonedAt`，增加 `SessionType` 与 `Phase`。

- [ ] **Step 4：实现固定调度与拼写规范化。**

```csharp
public static class WordStudySchedule
{
    private static readonly int[] SuccessfulIntervals = [2, 4, 7, 15, 30];

    public static (int Stage, DateTimeOffset NextReviewAt)
        AfterInitialLearning(DateTimeOffset completedAt)
        => (0, completedAt.AddDays(1));

    public static (int Stage, DateTimeOffset NextReviewAt) AfterReview(
        int currentStage,
        bool hadFailure,
        DateTimeOffset completedAt)
    {
        if (currentStage is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(currentStage));
        if (hadFailure) return (0, completedAt.AddDays(1));
        var nextStage = Math.Min(currentStage + 1, 5);
        var days = currentStage >= 5
            ? 30
            : SuccessfulIntervals[currentStage];
        return (nextStage, completedAt.AddDays(days));
    }

    public static bool IsCorrectSpelling(string answer, string headword)
        => string.Equals(
            answer.Trim().Normalize(NormalizationForm.FormC),
            headword.Trim().Normalize(NormalizationForm.FormC),
            StringComparison.OrdinalIgnoreCase);
}
```

对非法阶段值抛出 `ArgumentOutOfRangeException`，并补充 `-1`、`6` 的测试。

- [ ] **Step 5：运行测试确认 GREEN。**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyModelTests|FullyQualifiedName~WordStudyScheduleTests"
```

Expected: PASS。

- [ ] **Step 6：提交领域模型。**

```bash
git add server/TinyLang/Entities server/TinyLang/Services/WordStudySchedule.cs server/TinyLang.UnitTests/WordStudyModelTests.cs server/TinyLang.UnitTests/WordStudyScheduleTests.cs
git commit -m "refactor(server): define word study domain state"
```

---

## Task 2：配置数据库并生成破坏式迁移

**Files:**

- Create: `server/TinyLang/Database/Configurations/UserWordFavoriteConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/WordConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/UserConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/UserWordProgressConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/WordStudySessionConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/WordStudySessionItemConfiguration.cs`
- Modify: `server/TinyLang/Interfaces/IApplicationDbContext.cs`
- Modify: `server/TinyLang/Database/ApplicationDbContext.cs`
- Create: `server/TinyLang/Database/Migrations/<timestamp>_UpgradeWordStudyModule.cs`
- Create: `server/TinyLang/Database/Migrations/<timestamp>_UpgradeWordStudyModule.Designer.cs`
- Modify: `server/TinyLang/Database/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `server/TinyLang.UnitTests/WordStudyModelTests.cs`

- [ ] **Step 1：写数据库元数据失败测试。**

使用 EF model metadata 断言：

```csharp
word.FindProperty(nameof(Word.StudyOrder))!.ValueGenerated
    .Should().Be(ValueGenerated.OnAdd);
progress.GetIndexes().Should().Contain(index =>
    index.Properties.Select(value => value.Name)
        .SequenceEqual(["UserId", "NextReviewAt", "WordId"]));
session.GetIndexes().Should().Contain(index =>
    index.IsUnique && index.GetFilter() ==
        "\"Status\" = 'Active' AND \"SessionType\" = 'Learning'");
session.GetIndexes().Should().Contain(index =>
    index.IsUnique && index.GetFilter() ==
        "\"Status\" = 'Active' AND \"SessionType\" = 'Review'");
favorite.GetIndexes().Should().ContainSingle(index => index.IsUnique);
```

同时断言进度阶段 0..5、复习计数一致性、用户复习组大小 1..200、会话项尝试次数非负等 check constraints。

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyModelTests
```

Expected: FAIL，模型尚未配置。

- [ ] **Step 3：实现 EF 配置和 DbSet。**

关键配置：

```csharp
builder.Property(value => value.StudyOrder)
    .HasDefaultValueSql("nextval('word_study_order_seq')")
    .ValueGeneratedOnAdd();
builder.HasIndex(value => value.StudyOrder).IsUnique();

builder.HasIndex(value => new { value.UserId, value.WordId }).IsUnique();
builder.HasIndex(value => new { value.UserId, value.NextReviewAt, value.WordId });

builder.HasIndex(value => value.UserId)
    .IsUnique()
    .HasFilter("\"Status\" = 'Active' AND \"SessionType\" = 'Learning'")
    .HasDatabaseName("IX_word_study_sessions_UserId_ActiveLearning");
builder.HasIndex(value => value.UserId)
    .IsUnique()
    .HasFilter("\"Status\" = 'Active' AND \"SessionType\" = 'Review'")
    .HasDatabaseName("IX_word_study_sessions_UserId_ActiveReview");
```

`ApplicationDbContext.OnModelCreating` 注册：

```csharp
modelBuilder.HasSequence<long>("word_study_order_seq");
```

新增 `DbSet<UserWordFavorite>`，并在 `User`、`Word` 上增加收藏导航集合。

- [ ] **Step 4：运行模型测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyModelTests
```

- [ ] **Step 5：生成迁移并改为明确的破坏式学习数据迁移。**

Run:

```bash
dotnet tool restore --tool-manifest server/dotnet-tools.json
dotnet tool run dotnet-ef migrations add UpgradeWordStudyModule --project server/TinyLang --startup-project server/TinyLang --output-dir Database/Migrations
```

检查生成的 `Up`，确保顺序为：

1. 删除 `word_study_session_items`、`word_study_sessions`、`user_word_progress`。
2. 创建 `word_study_order_seq`。
3. 为 `words` 增加临时可空 `StudyOrder`。
4. 使用 `ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id")` 回填现有单词。
5. 将 `StudyOrder` 改为非空、默认 `nextval('word_study_order_seq')`，并把 sequence 调整到当前最大值之后。
6. 为 `users` 增加默认 50 的 `DailyWordReviewCount`。
7. 按新模型重建进度、会话、会话项与收藏表。
8. 建立所有 check constraints、外键和过滤唯一索引。

`Down` 删除新学习表和收藏表、移除用户与单词新增字段，并按旧模型重建空的三张学习表；不尝试恢复已删除的开发数据。

- [ ] **Step 6：验证迁移脚本和模型。**

```bash
dotnet tool run dotnet-ef migrations script --project server/TinyLang --startup-project server/TinyLang --idempotent --output /tmp/tiny-lang-word-study.sql
rg -n "DELETE|DROP TABLE|StudyOrder|word_study_order_seq|DailyWordReviewCount|UserWordFavorite" /tmp/tiny-lang-word-study.sql
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyModelTests
```

Expected: 脚本包含破坏式重建和全部新字段；测试 PASS。

- [ ] **Step 7：提交数据库模型。**

```bash
git add server/TinyLang/Database server/TinyLang/Interfaces/IApplicationDbContext.cs server/TinyLang/Entities/User.cs server/TinyLang/Entities/Word.cs server/TinyLang.UnitTests/WordStudyModelTests.cs
git commit -m "refactor(server): rebuild word study persistence"
```

---

## Task 3：定义学习、复习和用户词库契约

**Files:**

- Modify: `server/TinyLang/Dtos/WordStudyDtos.cs`
- Modify: `server/TinyLang/Dtos/WordStudyValidators.cs`
- Create: `server/TinyLang/Dtos/UserWordLibraryDtos.cs`
- Create: `server/TinyLang/Dtos/UserWordLibraryValidators.cs`
- Modify: `server/TinyLang/Dtos/UserDtos.cs`
- Modify: `server/TinyLang/Dtos/UserValidators.cs`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs`
- Modify: `server/TinyLang.UnitTests/WordStudyValidatorsTests.cs`
- Modify: `server/TinyLang.UnitTests/UserValidatorsTests.cs`
- Create test: `server/TinyLang.UnitTests/UserWordLibraryValidatorsTests.cs`

- [ ] **Step 1：写 DTO 校验和答案隔离的失败测试。**

覆盖：记忆结果仅接受 `Remembered/Forgotten`；拼写答案 trim 后 1..255；并发标识不能为 `Guid.Empty`；收藏分页 page >= 1、pageSize 1..100；学习数量 1..100；复习数量 1..200。

```csharp
var request = new SubmitWordSpellingRequest
{
    Answer = "   ",
    ItemConcurrencyStamp = Guid.NewGuid()
};
(await validator.ValidateAsync(request)).IsValid.Should().BeFalse();
```

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyValidatorsTests|FullyQualifiedName~UserValidatorsTests|FullyQualifiedName~UserWordLibraryValidatorsTests"
```

- [ ] **Step 3：实现固定契约。**

核心 DTO 形状：

```csharp
public sealed record WordStudySessionStateResponse(
    Guid Id,
    WordStudySessionType SessionType,
    WordStudyPhase Phase,
    WordStudySessionStatus Status,
    int ActualCount,
    int CompletedCount,
    int MemorizationPassedCount,
    int SpellingPassedCount,
    int ExcludedCount,
    int SkippedCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    WordStudyCurrentItemResponse? CurrentItem);

public sealed record WordStudyCurrentItemResponse(
    WordStudyPhase Phase,
    Guid ItemId,
    Guid WordId,
    Guid ItemConcurrencyStamp,
    bool IsFavorite,
    WordMemorizationContentResponse? Memorization,
    WordSpellingPromptResponse? Spelling);

public sealed record WordMemorizationContentResponse(
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId);

public sealed record WordSpellingPromptResponse(
    IReadOnlyList<WordSpellingSenseResponse> Senses);

public sealed record WordStudyCompletedItemResponse(
    Guid ItemId,
    Guid WordId,
    int Position,
    WordStudySessionItemStatus Status,
    string? Headword,
    bool HadMemorizationFailure,
    bool HadSpellingFailure);

public sealed record WordStudyCommandResponse(
    WordSpellingResult? SpellingResult,
    WordStudySessionStateResponse Session);
```

`WordSpellingPromptResponse` 不得包含 `Headword`、例句或音频字段。概览 DTO 分开定义：

```csharp
public sealed record WordLearningOverviewResponse(
    int TotalLearnedCount,
    int TodayLearnedCount,
    bool HasMoreWords,
    WordStudySessionStateResponse? ActiveSession);

public sealed record WordReviewOverviewResponse(
    int DueCount,
    int OverdueCount,
    WordStudySessionStateResponse? ActiveSession);
```

`DueCount` 统计 `NextReviewAt <= now`；`OverdueCount` 统计
`NextReviewAt < UTC 当日零点`，使“已到期”和“跨日积压”口径稳定。

命令请求使用：

```csharp
public sealed record SubmitWordMemorizationRequest(
    WordMemorizationResult? Result,
    Guid ItemConcurrencyStamp);
public sealed record SubmitWordSpellingRequest
{
    public required string Answer { get; init; }
    public Guid ItemConcurrencyStamp { get; init; }
}
public sealed record ExcludeWordFromReviewRequest(Guid ItemConcurrencyStamp);
```

扩展设置：

```csharp
public sealed record UpdateWordStudySettingsRequest
{
    public int DailyWordStudyCount { get; init; }
    public int DailyWordReviewCount { get; init; }
}
public sealed record WordStudySettingsResponse(
    int DailyWordStudyCount,
    int DailyWordReviewCount);
```

- [ ] **Step 4：新增稳定错误码。**

至少增加：`WordStudySessionTypeConflict`、`WordStudyPhaseConflict`、`WordStudyQueueConflict`、`WordStudySpellingRequired`、`WordStudySpellingLengthLimit`、`WordReviewCountInvalid`、`WordReviewNoDueWords`、`WordReviewProgressNotFound`、`WordFavoriteWordNotFound`。

- [ ] **Step 5：运行契约测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyValidatorsTests|FullyQualifiedName~UserValidatorsTests|FullyQualifiedName~UserWordLibraryValidatorsTests"
```

- [ ] **Step 6：提交契约。**

```bash
git add server/TinyLang/Dtos server/TinyLang/Exceptions/ErrorCodes.cs server/TinyLang.UnitTests/WordStudyValidatorsTests.cs server/TinyLang.UnitTests/UserValidatorsTests.cs server/TinyLang.UnitTests/UserWordLibraryValidatorsTests.cs
git commit -m "refactor(server): define word study contracts"
```

---

## Task 4：实现顺序新词领取、概览与会话恢复

**Files:**

- Modify: `server/TinyLang/Services/IWordStudyService.cs`
- Rewrite: `server/TinyLang/Services/WordStudyService.cs`
- Create: `server/TinyLang/Services/WordStudySessionEngine.cs`
- Create: `server/TinyLang/Services/WordStudySessionProjector.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Rewrite test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudyWordDeletionTests.cs`

- [ ] **Step 1：写顺序选择和活动会话恢复失败测试。**

测试数据使用乱序 GUID、不同 `StudyOrder`，断言：

```csharp
var first = await service.StartLearningSessionAsync(userId);
first.CurrentItem!.WordId.Should().Be(wordWithStudyOrder1.Id);

var repeated = await service.StartLearningSessionAsync(userId);
repeated.Id.Should().Be(first.Id);
```

再覆盖：排除已有 `UserWordProgress` 的词；按 `DailyWordStudyCount` 截断；无未学词时 overview 的 `HasMoreWords=false`，start 返回 `WordStudyNoEligibleWords`；跨 UTC 日期仍恢复活动学习会话。

- [ ] **Step 2：运行 focused test 确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyServiceTests|FullyQualifiedName~WordStudyWordDeletionTests"
```

- [ ] **Step 3：实现学习概览和顺序领取。**

`IWordStudyService` 提供：

```csharp
Task<WordLearningOverviewResponse> GetLearningOverviewAsync(Guid userId, CancellationToken cancellationToken = default);
Task<WordStudySessionStateResponse> StartLearningSessionAsync(Guid userId, CancellationToken cancellationToken = default);
Task<WordStudySessionStateResponse> GetLearningSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
Task<IReadOnlyList<WordStudyCompletedItemResponse>> GetCompletedSessionItemsAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
```

领取查询固定为：

```csharp
var wordIds = await WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
    .Where(word => !_db.UserWordProgress.AsNoTracking().Any(progress =>
        progress.UserId == userId && progress.WordId == word.Id))
    .OrderBy(word => word.StudyOrder)
    .Take(dailyWordStudyCount)
    .Select(word => word.Id)
    .ToListAsync(cancellationToken);
```

创建项时 `Position`、`MemorizationQueueOrder`、`SpellingQueueOrder` 都使用数组索引。捕获 `IX_word_study_sessions_UserId_ActiveLearning` 唯一索引竞争后清理 tracking 并返回数据库中的活动 learning 会话。

- [ ] **Step 4：实现安全投影。**

`WordStudySessionProjector` 根据 `Phase` 只构造一种内容：

```csharp
if (session.Phase == WordStudyPhase.Memorization)
    return currentItem with { Memorization = fullContent, Spelling = null };
return currentItem with {
    Memorization = null,
    Spelling = new WordSpellingPromptResponse(
        word.Senses.Select(sense =>
            new WordSpellingSenseResponse(sense.PartOfSpeech, sense.Definition))
        .ToArray())
};
```

`WordStudySessionEngine` 在本 Task 先实现 `NormalizeCurrentItemAsync`：如果队首词实时不可见，事务内标记 `Skipped` 后继续寻找下一项；若会话已无有效项目则直接完成，若记忆阶段仍有有效项目但已全部通过则切换到 spelling。`WordStudySessionProjector` 只负责只读 DTO 构造，不在投影中写数据库。Task 5 再向同一 engine 增加用户命令。

- [ ] **Step 5：运行测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyServiceTests|FullyQualifiedName~WordStudyWordDeletionTests"
```

- [ ] **Step 6：提交学习查询。**

```bash
git add server/TinyLang/Services/IWordStudyService.cs server/TinyLang/Services/WordStudyService.cs server/TinyLang/Services/WordStudySessionEngine.cs server/TinyLang/Services/WordStudySessionProjector.cs server/TinyLang/Services/DependencyInjection.cs server/TinyLang.UnitTests/WordStudyServiceTests.cs server/TinyLang.UnitTests/WordStudyWordDeletionTests.cs
git commit -m "feat(server): create sequential learning sessions"
```

---

## Task 5：实现记忆轮播与阶段切换

**Files:**

- Modify: `server/TinyLang/Services/WordStudySessionEngine.cs`
- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Create test: `server/TinyLang.UnitTests/WordStudySessionEngineTests.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`

- [ ] **Step 1：写记忆状态机失败测试。**

覆盖：

1. `Forgotten` 增加尝试次数、设置 `HadMemorizationFailure` 并把序号改为当前最大值 + 1。
2. `Remembered` 使当前项通过记忆阶段，但总体状态仍为 `Pending`。
3. 最后一个有效项通过记忆后，会话切到 `Spelling`，首项按 `SpellingQueueOrder` 返回。
4. 非队首、错误 phase、错误 session type、陈旧并发标识均不改变数据并返回稳定冲突。
5. 重复点击只能有一个事务成功。

```csharp
var result = await engine.SubmitMemorizationAsync(
    userId, session.Id, first.Id,
    new SubmitWordMemorizationRequest(
        WordMemorizationResult.Forgotten,
        first.ConcurrencyStamp));

result.Session.CurrentItem!.ItemId.Should().Be(second.Id);
first.HadMemorizationFailure.Should().BeTrue();
first.MemorizationAttemptCount.Should().Be(1);
```

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudySessionEngineTests
```

- [ ] **Step 3：实现事务化记忆命令。**

核心状态迁移固定为：

```csharp
item.MemorizationAttemptCount++;
if (result == WordMemorizationResult.Forgotten)
{
    item.HadMemorizationFailure = true;
    item.MemorizationQueueOrder = await NextMemorizationOrderAsync(session.Id);
}
else
{
item.MemorizationPassedAt = now;
}
item.ConcurrencyStamp = Guid.NewGuid();

if (!await HasPendingMemorizationAsync(session.Id))
{
    session.Phase = WordStudyPhase.Spelling;
    session.ConcurrencyStamp = Guid.NewGuid();
}
```

`MemorizationPassedAt` 已在 Task 1 的实体与 Task 2 的迁移中建立。队首判断必须与查询投影使用同一排序条件。

- [ ] **Step 4：通过 service 暴露 learning/review 记忆入口。**

service 方法显式接收期望类型：

```csharp
SubmitLearningMemorizationAsync(...)
    => _engine.SubmitMemorizationAsync(
        userId, sessionId, itemId, WordStudySessionType.Learning, request, ct);
SubmitReviewMemorizationAsync(...)
    => _engine.SubmitMemorizationAsync(
        userId, sessionId, itemId, WordStudySessionType.Review, request, ct);
```

- [ ] **Step 5：运行测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudySessionEngineTests|FullyQualifiedName~WordStudyServiceTests"
```

- [ ] **Step 6：提交记忆状态机。**

```bash
git add server/TinyLang/Services/WordStudySessionEngine.cs server/TinyLang/Services/WordStudyService.cs server/TinyLang.UnitTests/WordStudySessionEngineTests.cs server/TinyLang.UnitTests/WordStudyServiceTests.cs
git commit -m "feat(server): persist memorization carousel"
```

---

## Task 6：实现拼写轮播、首次学习完成与统计

**Files:**

- Modify: `server/TinyLang/Services/WordStudySessionEngine.cs`
- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudySessionEngineTests.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`

- [ ] **Step 1：写拼写与学习统计失败测试。**

覆盖：错误答案后移且响应 `Incorrect`；正确答案完成项目；最后一项正确完成 learning session；首次学习创建 `UserWordProgress`；今日统计使用首次完成时间的 UTC 日期；重复命令不重复创建进度或计数。

```csharp
var incorrect = await engine.SubmitSpellingAsync(
    userId, session.Id, first.Id, WordStudySessionType.Learning,
    new SubmitWordSpellingRequest {
        Answer = "wrong",
        ItemConcurrencyStamp = first.ConcurrencyStamp
    });
incorrect.SpellingResult.Should().Be(WordSpellingResult.Incorrect);
incorrect.Session.CurrentItem!.ItemId.Should().Be(second.Id);
```

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudySessionEngineTests|FullyQualifiedName~WordStudyServiceTests"
```

- [ ] **Step 3：实现拼写状态迁移。**

错误分支：

```csharp
item.SpellingAttemptCount++;
item.HadSpellingFailure = true;
item.SpellingQueueOrder = await NextSpellingOrderAsync(session.Id);
item.ConcurrencyStamp = Guid.NewGuid();
```

正确分支：

```csharp
item.SpellingAttemptCount++;
item.Status = WordStudySessionItemStatus.Completed;
item.CompletedAt = now;
item.ConcurrencyStamp = Guid.NewGuid();
await CompleteLearningProgressAsync(session.UserId, item.WordId, now, ct);
```

`CompleteLearningProgressAsync` 只允许不存在进度时创建：

```csharp
var schedule = WordStudySchedule.AfterInitialLearning(now);
_db.UserWordProgress.Add(new UserWordProgress {
    UserId = userId,
    WordId = wordId,
    FirstStudiedAt = now,
    LastStudiedAt = now,
    ReviewStage = schedule.Stage,
    NextReviewAt = schedule.NextReviewAt
});
```

所有有效项终态后设置 `session.Status = Completed`、`CompletedAt = now`。

- [ ] **Step 4：实现 overview 统计查询。**

累计数直接 count 当前用户进度；今日数按 `[UTC today, UTC tomorrow)` 过滤 `FirstStudiedAt`。复习和失败尝试不得写 `FirstStudiedAt`。

- [ ] **Step 5：运行测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudySessionEngineTests|FullyQualifiedName~WordStudyServiceTests"
```

- [ ] **Step 6：提交拼写和统计。**

```bash
git add server/TinyLang/Services/WordStudySessionEngine.cs server/TinyLang/Services/WordStudyService.cs server/TinyLang.UnitTests/WordStudySessionEngineTests.cs server/TinyLang.UnitTests/WordStudyServiceTests.cs
git commit -m "feat(server): complete learning through spelling"
```

---

## Task 7：实现到期复习、调度推进和停止复习

**Files:**

- Modify: `server/TinyLang/Services/WordStudyService.cs`
- Modify: `server/TinyLang/Services/WordStudySessionEngine.cs`
- Modify: `server/TinyLang/Services/IWordStudyService.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudyServiceTests.cs`
- Modify test: `server/TinyLang.UnitTests/WordStudySessionEngineTests.cs`

- [ ] **Step 1：写复习领取与调度失败测试。**

覆盖：

- 仅领取 `NextReviewAt <= now` 且 `IsReviewExcluded=false` 的进度。
- 排序为 `NextReviewAt`、`Word.StudyOrder`，最多用户设置的 50 个。
- 有活动 review 会话时幂等恢复，不领取新到期词。
- learning 和 review 活动会话可以同时存在。
- 任一记忆/拼写失败后最终完成重置到 1 天。
- 两阶段首次通过时按 2/4/7/15/30 天推进并在阶段 5 保持 30 天。
- exclude 立即移出队列、清空 `NextReviewAt`，不增加成功/失败计数。
- 记忆阶段排除最后一个有效词时直接完成会话，不进入空的拼写阶段。

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyServiceTests|FullyQualifiedName~WordStudySessionEngineTests"
```

- [ ] **Step 3：实现复习概览和分批领取。**

```csharp
var due = from progress in _db.UserWordProgress.AsNoTracking()
          join word in WordVisibilityPolicy.Apply(_db.Words.AsNoTracking())
              on progress.WordId equals word.Id
          where progress.UserId == userId &&
                !progress.IsReviewExcluded &&
                progress.NextReviewAt != null &&
                progress.NextReviewAt <= now
          orderby progress.NextReviewAt, word.StudyOrder
          select word.Id;
```

用 `DailyWordReviewCount` 截断。无到期词时返回 `WordReviewNoDueWords`；overview 返回 due count 和以 UTC 当前时刻为界的 overdue count。
捕获 `IX_word_study_sessions_UserId_ActiveReview` 唯一索引竞争后返回已存在的活动 review 会话。

- [ ] **Step 4：在拼写正确时更新复习进度。**

```csharp
var hadFailure = item.HadMemorizationFailure || item.HadSpellingFailure;
var next = WordStudySchedule.AfterReview(
    progress.ReviewStage,
    hadFailure,
    now);
progress.ReviewStage = next.Stage;
progress.NextReviewAt = next.NextReviewAt;
progress.LastReviewedAt = now;
progress.LastStudiedAt = now;
progress.ReviewCount++;
if (hadFailure) progress.FailedReviewCount++;
else progress.SuccessfulReviewCount++;
progress.ConcurrencyStamp = Guid.NewGuid();
```

- [ ] **Step 5：实现 review exclude。**

验证 session type、当前队首和并发标识后：

```csharp
item.Status = WordStudySessionItemStatus.Excluded;
item.CompletedAt = now;
item.ConcurrencyStamp = Guid.NewGuid();
progress.IsReviewExcluded = true;
progress.ReviewExcludedAt = now;
progress.NextReviewAt = null;
progress.ConcurrencyStamp = Guid.NewGuid();
```

然后先检查会话是否仍有 `Pending` 有效项目：没有时直接完成 session；仍有有效项目且当前记忆阶段已经全部通过时才切换到 spelling。这样最后一个排除项不会产生空拼写阶段。

- [ ] **Step 6：运行测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyServiceTests|FullyQualifiedName~WordStudySessionEngineTests"
```

- [ ] **Step 7：提交复习功能。**

```bash
git add server/TinyLang/Services/WordStudyService.cs server/TinyLang/Services/WordStudySessionEngine.cs server/TinyLang/Services/IWordStudyService.cs server/TinyLang.UnitTests/WordStudyServiceTests.cs server/TinyLang.UnitTests/WordStudySessionEngineTests.cs
git commit -m "feat(server): schedule due word reviews"
```

---

## Task 8：实现收藏、停止复习列表、恢复和设置

**Files:**

- Create: `server/TinyLang/Services/IUserWordLibraryService.cs`
- Create: `server/TinyLang/Services/UserWordLibraryService.cs`
- Modify: `server/TinyLang/Services/UserService.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Modify: `server/TinyLang/Services/IUserService.cs`
- Create test: `server/TinyLang.UnitTests/UserWordLibraryServiceTests.cs`
- Modify test: `server/TinyLang.UnitTests/UserServiceTests.cs`

- [ ] **Step 1：写用户词库和设置失败测试。**

覆盖：收藏幂等、取消收藏幂等、用户隔离、分页按收藏时间倒序、只投影实时可见词、恢复复习设为阶段 0 且 `NextReviewAt=now+1d`、不存在进度时报稳定错误、设置同时保存学习 1..100 和复习 1..200。

- [ ] **Step 2：运行测试确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~UserWordLibraryServiceTests|FullyQualifiedName~UserServiceTests"
```

- [ ] **Step 3：实现用户词库 service。**

接口固定为：

```csharp
Task<PagedResponse<UserWordFavoriteResponse>> GetFavoritesAsync(
    Guid userId, UserWordLibraryListRequest request, CancellationToken ct = default);
Task SetFavoriteAsync(Guid userId, Guid wordId, bool favorite, CancellationToken ct = default);
Task<PagedResponse<UserWordReviewExclusionResponse>> GetReviewExclusionsAsync(
    Guid userId, UserWordLibraryListRequest request, CancellationToken ct = default);
Task RestoreReviewAsync(Guid userId, Guid wordId, CancellationToken ct = default);
```

`SetFavoriteAsync(true)` 在唯一索引竞争后读取已存在关系并成功返回；`false` 不存在时同样成功。分页响应复用现有 `PagedResponse<T>`。

- [ ] **Step 4：扩展用户设置 service。**

`GetWordStudySettingsAsync` 和 `UpdateWordStudySettingsAsync` 同时读取/保存两个字段；任何字段越界都不写入数据库。

- [ ] **Step 5：运行测试确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~UserWordLibraryServiceTests|FullyQualifiedName~UserServiceTests"
```

- [ ] **Step 6：提交用户词库。**

```bash
git add server/TinyLang/Services/IUserWordLibraryService.cs server/TinyLang/Services/UserWordLibraryService.cs server/TinyLang/Services/UserService.cs server/TinyLang/Services/IUserService.cs server/TinyLang/Services/DependencyInjection.cs server/TinyLang.UnitTests/UserWordLibraryServiceTests.cs server/TinyLang.UnitTests/UserServiceTests.cs
git commit -m "feat(server): manage user word library"
```

---

## Task 9：替换后端 endpoints 并验证授权契约

**Files:**

- Rewrite: `server/TinyLang/Endpoints/WordStudyEndpoints.cs`
- Create: `server/TinyLang/Endpoints/UserWordLibraryEndpoints.cs`
- Modify: `server/TinyLang/Endpoints/UserEndpoints.cs`
- Modify: `server/TinyLang/Program.cs`
- Rewrite test: `server/TinyLang.UnitTests/WordStudyEndpointTests.cs`
- Create test: `server/TinyLang.UnitTests/UserWordLibraryEndpointTests.cs`
- Modify test: `server/TinyLang.UnitTests/UserEndpointTests.cs`
- Modify test: `server/TinyLang.UnitTests/OpenApiContractTests.cs`

- [ ] **Step 1：写路由分离、认证和响应隔离失败测试。**

验证以下路由：

```text
GET  /api/word-study/learning/overview
POST /api/word-study/learning/sessions
GET  /api/word-study/learning/sessions/{sessionId}
GET  /api/word-study/learning/sessions/{sessionId}/results
POST /api/word-study/learning/sessions/{sessionId}/items/{itemId}/memorization
POST /api/word-study/learning/sessions/{sessionId}/items/{itemId}/spelling

GET  /api/word-study/review/overview
POST /api/word-study/review/sessions
GET  /api/word-study/review/sessions/{sessionId}
GET  /api/word-study/review/sessions/{sessionId}/results
POST /api/word-study/review/sessions/{sessionId}/items/{itemId}/memorization
POST /api/word-study/review/sessions/{sessionId}/items/{itemId}/spelling
POST /api/word-study/review/sessions/{sessionId}/items/{itemId}/exclude

GET    /api/users/me/word-favorites
PUT    /api/users/me/word-favorites/{wordId}
DELETE /api/users/me/word-favorites/{wordId}
GET    /api/users/me/word-review-exclusions
DELETE /api/users/me/word-review-exclusions/{wordId}
```

所有路由必须使用认证 principal 的用户 ID；请求其他用户会话返回 not found。序列化一个 spelling current item，断言 JSON 不包含 `headword`、`audioResourceId`、`examples`、`sentence`。

- [ ] **Step 2：运行 endpoint tests 确认 RED。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyEndpointTests|FullyQualifiedName~UserWordLibraryEndpointTests|FullyQualifiedName~UserEndpointTests|FullyQualifiedName~OpenApiContractTests"
```

- [ ] **Step 3：注册分离路由。**

`MapWordStudyApi` 内建立 `/word-study/learning` 和 `/word-study/review` 两个 group，均应用 `RequireUser`。`MapUserWordLibraryApi` 建立 `/users/me` group。删除旧 `/word-study/today`、通用 `/word-study/sessions`、`/next`、`/result` 和 `/abandon` 路由。

- [ ] **Step 4：实现 endpoint 到 service 的一对一转发。**

POST create 返回 `201 Created`；overview 和 session GET 返回 `200`；命令返回 `200`；收藏/取消收藏/恢复成功返回 `204 No Content`。路径和返回类型必须在 OpenAPI 中稳定可见。

- [ ] **Step 5：运行 endpoint tests 确认 GREEN。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WordStudyEndpointTests|FullyQualifiedName~UserWordLibraryEndpointTests|FullyQualifiedName~UserEndpointTests|FullyQualifiedName~OpenApiContractTests"
```

- [ ] **Step 6：运行后端完整测试。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/tiny-lang.slnx --no-restore
```

- [ ] **Step 7：提交后端 API。**

```bash
git add server/TinyLang/Endpoints server/TinyLang/Program.cs server/TinyLang.UnitTests/WordStudyEndpointTests.cs server/TinyLang.UnitTests/UserWordLibraryEndpointTests.cs server/TinyLang.UnitTests/UserEndpointTests.cs server/TinyLang.UnitTests/OpenApiContractTests.cs
git commit -m "feat(server): expose word learning and review APIs"
```

---

## Task 10：重建用户端类型和 RTK Query API

**Files:**

- Rewrite: `app/src/features/wordStudy/wordStudyTypes.ts`
- Rewrite: `app/src/features/wordStudy/wordStudyApi.ts`
- Modify: `app/src/features/wordStudy/wordStudyErrors.ts`
- Rewrite test: `app/src/features/wordStudy/wordStudyApi.test.ts`
- Modify: `app/src/features/auth/authErrors.ts`

- [ ] **Step 1：写 API 契约失败测试。**

依次 dispatch learning overview/start/memorization/spelling、review overview/start/exclude、favorite put/delete、exclusion restore，断言 method、URL、body 和 bearer token。特别断言命令 body：

```ts
expect(JSON.parse(request.data)).toEqual({
  result: "Forgotten",
  itemConcurrencyStamp: ITEM_STAMP,
});
expect(JSON.parse(spellingRequest.data)).toEqual({
  answer: "école",
  itemConcurrencyStamp: ITEM_STAMP,
});
```

再断言 spelling prompt 的 TypeScript fixture 无 `headword` 字段，收藏/排除 mutation 会失效对应列表和 overview tag。

- [ ] **Step 2：运行测试确认 RED。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/wordStudyApi.test.ts
```

- [ ] **Step 3：定义 discriminated union 类型。**

```ts
export type WordStudyCurrentItem =
  | {
      phase: "Memorization";
      itemId: string;
      wordId: string;
      itemConcurrencyStamp: string;
      isFavorite: boolean;
      memorization: WordMemorizationContent;
      spelling: null;
    }
  | {
      phase: "Spelling";
      itemId: string;
      wordId: string;
      itemConcurrencyStamp: string;
      isFavorite: boolean;
      memorization: null;
      spelling: WordSpellingPrompt;
    };
```

`WordSpellingPrompt` 只包含 `senses: { partOfSpeech; definition }[]`。定义 learning/review overview、session state、command response、paged favorite 和 exclusion 类型。

- [ ] **Step 4：实现 RTK Query endpoints 和 tags。**

使用 `LearningOverview`、`ReviewOverview`、`WordStudySession`、`WordFavorites`、`WordReviewExclusions`、`WordStudySettings` tags。命令成功后直接用返回的 session 更新对应 session cache，并失效 overview；不再保留旧 item 列表 optimistic patch。

- [ ] **Step 5：运行测试和类型检查确认 GREEN。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/wordStudyApi.test.ts
pnpm --dir app exec tsc -b --pretty false
```

- [ ] **Step 6：提交用户端 API。**

```bash
git add app/src/features/wordStudy/wordStudyTypes.ts app/src/features/wordStudy/wordStudyApi.ts app/src/features/wordStudy/wordStudyApi.test.ts app/src/features/wordStudy/wordStudyErrors.ts app/src/features/auth/authErrors.ts
git commit -m "refactor(app): consume word study APIs"
```

---

## Task 11：将 `/words` 改为学习与复习任务首页

**Files:**

- Rewrite: `app/src/pages/WordsPage.tsx`
- Rewrite test: `app/src/pages/WordsPage.test.tsx`

- [ ] **Step 1：写首页与路由失败测试。**

首页 fixture 返回今日学习 12、累计 320、到期 68、逾期 20。断言页面提供：

```tsx
expect(screen.getByRole("heading", { name: "单词学习" })).toBeVisible();
expect(screen.getByText("今日已学习 12 个")).toBeVisible();
expect(screen.getByText("累计学习 320 个")).toBeVisible();
expect(screen.getByText("待复习 68 个")).toBeVisible();
expect(screen.getByRole("link", { name: "开始新词学习" }))
  .toHaveAttribute("href", "/words/learning");
expect(screen.getByRole("link", { name: "开始旧词复习" }))
  .toHaveAttribute("href", "/words/review");
```

活动会话时按钮文案改为“继续”；无新词和无到期词时展示稳定空状态并禁用对应命令。

- [ ] **Step 2：运行测试确认 RED。**

```bash
pnpm --dir app exec vitest run src/pages/WordsPage.test.tsx
```

- [ ] **Step 3：实现任务首页。**

使用两个并列的任务区块，不嵌套卡片，不制作宣传 hero。桌面双列、窄屏单列；入口名称和核心数字在首屏可扫描。错误时分别重试 learning/review overview，不因一侧失败隐藏另一侧数据。

- [ ] **Step 4：运行测试确认 GREEN。**

```bash
pnpm --dir app exec vitest run src/pages/WordsPage.test.tsx
```

- [ ] **Step 5：提交首页。**

```bash
git add app/src/pages/WordsPage.tsx app/src/pages/WordsPage.test.tsx
git commit -m "feat(app): split word learning entry points"
```

---

## Task 12：实现共享记忆与拼写工作区

**Files:**

- Create: `app/src/features/wordStudy/WordStudyWorkspace.tsx`
- Create: `app/src/features/wordStudy/WordMemorizationCard.tsx`
- Create: `app/src/features/wordStudy/WordSpellingCard.tsx`
- Create: `app/src/features/wordStudy/WordStudyCompletion.tsx`
- Create test: `app/src/features/wordStudy/WordStudyWorkspace.test.tsx`
- Rewrite: `app/src/pages/WordLearningPage.tsx`
- Rewrite: `app/src/pages/WordReviewPage.tsx`
- Create test: `app/src/pages/WordLearningPage.test.tsx`
- Create test: `app/src/pages/WordReviewPage.test.tsx`
- Modify: `app/src/router/index.tsx`
- Modify test: `app/src/router/router.test.tsx`
- Delete: `app/src/features/wordStudy/WordStudyDirectory.tsx`
- Delete: `app/src/features/wordStudy/WordStudyDirectory.test.tsx`
- Replace: `app/src/features/wordStudy/WordStudyCard.tsx`
- Replace test: `app/src/features/wordStudy/WordStudyCard.test.tsx`

- [ ] **Step 1：写记忆轮播 UI 失败测试。**

断言完整词头、释义、例句和音频按钮可见；“没记住”发送 `Forgotten` 与并发标识，并切换到服务端返回的下一项；保存时两个结果按钮禁用；页面不存在未来词目录和自由跳转按钮。

- [ ] **Step 2：写拼写阶段 UI 失败测试。**

```tsx
expect(screen.queryByText("école")).not.toBeInTheDocument();
expect(screen.queryByRole("button", { name: /单词音频/ })).toBeNull();
await user.type(screen.getByRole("textbox", { name: "拼写单词" }), "ecole");
await user.click(screen.getByRole("button", { name: "提交拼写" }));
expect(await screen.findByText("拼写不正确，稍后再试一次")).toBeVisible();
```

再覆盖正确响应、阶段自动切换、请求失败保留输入、并发冲突后刷新会话。

- [ ] **Step 3：运行测试确认 RED。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudyWorkspace.test.tsx src/pages/WordLearningPage.test.tsx src/pages/WordReviewPage.test.tsx
```

- [ ] **Step 4：实现 `WordStudyWorkspace`。**

组件 props 固定为：

```ts
interface WordStudyWorkspaceProps {
  mode: "learning" | "review";
  session: WordStudySessionState;
  submitting: boolean;
  onMemorization: (
    item: WordStudyCurrentItem,
    result: WordMemorizationResult,
  ) => Promise<void>;
  onSpelling: (
    item: WordStudyCurrentItem,
    answer: string,
  ) => Promise<WordSpellingResult | null>;
  onToggleFavorite: (item: WordStudyCurrentItem) => Promise<void>;
  onExclude?: (item: WordStudyCurrentItem) => void;
}
```

根据 discriminant 渲染对应 card。进度使用“本阶段已通过 / 本组有效总数”，动态反馈不得改变工作区固定宽度。记忆卡复用现有 `AudioPlaybackButton`。

- [ ] **Step 5：实现两个页面容器。**

进入页面先获取 overview：有活动会话则获取最新 session；没有则调用 start mutation。学习与复习页面分别调用专属 mutation，禁止根据 mode 拼接 URL。退出页面不会 abandon 会话。

- [ ] **Step 6：注册并测试受保护的子路由。**

```tsx
{
  path: "words/learning",
  lazy: async () => ({
    Component: (await import("@/pages/WordLearningPage")).default,
  }),
},
{
  path: "words/review",
  lazy: async () => ({
    Component: (await import("@/pages/WordReviewPage")).default,
  }),
},
```

在 `router.test.tsx` 中断言两个路径都在未登录时进入认证边界，登录后分别渲染对应页面。

- [ ] **Step 7：实现完成态。**

完成组件展示本组完成/排除/跳过数量，并根据最新 overview 显示“再学一组”“再复习一组”或返回首页。点击下一组先刷新 overview，再创建新会话，避免使用完成时的旧积压数量。

- [ ] **Step 8：运行测试、类型检查和格式检查。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudyWorkspace.test.tsx src/pages/WordLearningPage.test.tsx src/pages/WordReviewPage.test.tsx src/router/router.test.tsx
pnpm --dir app exec tsc -b --pretty false
pnpm --dir app exec prettier --check "src/features/wordStudy/*.{ts,tsx}" "src/pages/Word*Page.tsx"
```

- [ ] **Step 9：提交工作区。**

```bash
git add app/src/features/wordStudy app/src/pages/WordLearningPage.tsx app/src/pages/WordLearningPage.test.tsx app/src/pages/WordReviewPage.tsx app/src/pages/WordReviewPage.test.tsx app/src/router/index.tsx app/src/router/router.test.tsx
git commit -m "feat(app): add word study carousel workspace"
```

---

## Task 13：接入收藏与“不再复习”交互

**Files:**

- Modify: `app/src/features/wordStudy/WordMemorizationCard.tsx`
- Modify: `app/src/features/wordStudy/WordSpellingCard.tsx`
- Modify: `app/src/features/wordStudy/WordStudyWorkspace.tsx`
- Create: `app/src/features/wordStudy/WordReviewExcludeDialog.tsx`
- Modify test: `app/src/features/wordStudy/WordStudyWorkspace.test.tsx`
- Modify test: `app/src/pages/WordReviewPage.test.tsx`

- [ ] **Step 1：写收藏失败回滚和排除确认测试。**

断言收藏按钮使用图标和可访问名称，成功后切换状态，失败时恢复原状态并显示错误。复习排除必须先打开 modal：

```tsx
await user.click(screen.getByRole("button", { name: "不再复习此词" }));
expect(screen.getByRole("heading", { name: "停止复习这个单词？" }))
  .toBeVisible();
await user.click(screen.getByRole("button", { name: "确认停止复习" }));
expect(excludeRequest).toMatchObject({
  itemId: ITEM_ID,
  itemConcurrencyStamp: ITEM_STAMP,
});
```

取消 modal 不发送请求；成功后使用响应中的下一项；最后一项排除后进入完成态。

- [ ] **Step 2：运行测试确认 RED。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudyWorkspace.test.tsx src/pages/WordReviewPage.test.tsx
```

- [ ] **Step 3：实现收藏切换。**

记忆和拼写卡都显示同一 `Bookmark` / `BookmarkCheck` 图标按钮。页面容器根据 `item.isFavorite` 调用 PUT 或 DELETE，使用局部 optimistic state；请求失败恢复并显示 alert，不改变会话队列。

- [ ] **Step 4：实现排除确认弹窗。**

使用项目现有 dialog/modal 模式；提交中禁止关闭和重复点击。弹窗不显示技术字段，命令成功后关闭并用 response.session 替换当前会话。

- [ ] **Step 5：运行测试确认 GREEN。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudyWorkspace.test.tsx src/pages/WordReviewPage.test.tsx
```

- [ ] **Step 6：提交收藏与排除交互。**

```bash
git add app/src/features/wordStudy app/src/pages/WordReviewPage.test.tsx
git commit -m "feat(app): manage study favorites and exclusions"
```

---

## Task 14：升级用户中心设置、统计、收藏本和停止复习列表

**Files:**

- Modify: `app/src/features/wordStudy/WordStudySettingsPanel.tsx`
- Modify test: `app/src/features/wordStudy/WordStudySettingsPanel.test.tsx`
- Create: `app/src/features/wordStudy/WordStudySummaryPanel.tsx`
- Create test: `app/src/features/wordStudy/WordStudySummaryPanel.test.tsx`
- Create: `app/src/features/wordStudy/WordFavoritesPanel.tsx`
- Create test: `app/src/features/wordStudy/WordFavoritesPanel.test.tsx`
- Create: `app/src/features/wordStudy/WordReviewExclusionsPanel.tsx`
- Create test: `app/src/features/wordStudy/WordReviewExclusionsPanel.test.tsx`
- Modify: `app/src/pages/ProfilePage.tsx`
- Modify test: `app/src/pages/ProfilePage.test.tsx`

- [ ] **Step 1：写双设置保存失败测试。**

同时加载两个字段；学习数量 101 和复习数量 201 分别显示独立错误；合法提交 body 固定为：

```ts
{
  dailyWordStudyCount: 30,
  dailyWordReviewCount: 80,
}
```

- [ ] **Step 2：写统计、收藏与停止复习列表失败测试。**

覆盖 overview 统计、分页、空状态、收藏音频、取消收藏、恢复复习确认和失败保留列表项。恢复成功后失效 exclusions 和 review overview。

- [ ] **Step 3：运行测试确认 RED。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudySettingsPanel.test.tsx src/features/wordStudy/WordStudySummaryPanel.test.tsx src/features/wordStudy/WordFavoritesPanel.test.tsx src/features/wordStudy/WordReviewExclusionsPanel.test.tsx src/pages/ProfilePage.test.tsx
```

- [ ] **Step 4：实现设置与统计面板。**

设置表单使用两个数值 input，不用自由文本模拟控件。保存成功文案说明新数量从下一组生效。统计面板读取 learning overview，只显示今日和累计数字，不展示内部 UTC 时间戳。

- [ ] **Step 5：实现两个分页管理面板。**

收藏和停止复习各自是独立页面区块，不在卡片中嵌套卡片。列表项展示词头、首个释义、必要的音频和明确操作；分页保持固定控制尺寸。恢复复习使用确认 dialog，说明恢复后 1 天到期。

- [ ] **Step 6：接入 `ProfilePage`。**

顺序为学习统计、学习设置、收藏本、已停止复习、账户安全。各区块独立加载和重试，一个区块失败不阻断资料编辑或其他区块。

- [ ] **Step 7：运行测试确认 GREEN。**

```bash
pnpm --dir app exec vitest run src/features/wordStudy/WordStudySettingsPanel.test.tsx src/features/wordStudy/WordStudySummaryPanel.test.tsx src/features/wordStudy/WordFavoritesPanel.test.tsx src/features/wordStudy/WordReviewExclusionsPanel.test.tsx src/pages/ProfilePage.test.tsx
pnpm --dir app exec tsc -b --pretty false
```

- [ ] **Step 8：提交用户中心。**

```bash
git add app/src/features/wordStudy/WordStudySettingsPanel.tsx app/src/features/wordStudy/WordStudySettingsPanel.test.tsx app/src/features/wordStudy/WordStudySummaryPanel.tsx app/src/features/wordStudy/WordStudySummaryPanel.test.tsx app/src/features/wordStudy/WordFavoritesPanel.tsx app/src/features/wordStudy/WordFavoritesPanel.test.tsx app/src/features/wordStudy/WordReviewExclusionsPanel.tsx app/src/features/wordStudy/WordReviewExclusionsPanel.test.tsx app/src/pages/ProfilePage.tsx app/src/pages/ProfilePage.test.tsx
git commit -m "feat(app): add personal word study library"
```

---

## Task 15：删除旧契约、运行完整回归并检查提交边界

**Files:**

- Verify/modify: `server/TinyLang/**`
- Verify/modify: `server/TinyLang.UnitTests/**`
- Verify/modify: `app/src/features/wordStudy/**`
- Verify/modify: `app/src/pages/Word*.tsx`
- Verify/modify: `app/src/pages/ProfilePage.tsx`

- [ ] **Step 1：扫描旧状态和旧 API 残留。**

```bash
rg -n "WordStudySelectionMode|IncludePreviouslyStudied|AbandonedAt|WordStudyResult|/word-study/today|/word-study/sessions/.*/next|/result|WordStudyDirectory" server app/src
```

Expected: 无业务代码匹配；历史 migration 中的旧字段允许保留。若需排除 migration，使用：

```bash
rg -n "WordStudySelectionMode|IncludePreviouslyStudied|AbandonedAt|WordStudyResult|/word-study/today|WordStudyDirectory" server/TinyLang --glob '!Database/Migrations/**' app/src
```

- [ ] **Step 2：运行后端完整验证。**

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/tiny-lang.slnx --no-restore
dotnet tool run dotnet-ef migrations script --project server/TinyLang --startup-project server/TinyLang --idempotent --output /tmp/tiny-lang-word-study-final.sql
```

Expected: 全部 exit code 0。

- [ ] **Step 3：运行用户端完整验证。**

```bash
pnpm --dir app lint
pnpm --dir app test
pnpm --dir app build
pnpm --dir app exec prettier --check "src/**/*.{ts,tsx,css,json}"
```

Expected: 全部 exit code 0。

- [ ] **Step 4：针对关键业务做最终契约复核。**

逐项确认：

1. 新词 SQL 只按 `StudyOrder` 选择未学词。
2. review SQL 只选择到期且未排除词，并按到期时间优先。
3. spelling response 类型和 JSON 均不包含答案字段。
4. learning/review 活动唯一索引相互独立。
5. 失败后移和 phase 切换均在事务内。
6. UTC 今日统计使用半开区间。
7. 收藏、排除和恢复均按认证用户隔离。

- [ ] **Step 5：检查 diff 和提交边界。**

```bash
git diff --check
git status --short
git diff --cached --name-only
word_study_base=$(git merge-base HEAD refactor-word)
git log --format= --name-only "$word_study_base"..HEAD | rg '^(docs/|word-study\.md$)' || true
```

Expected: staging area 为空；实现提交不包含 `docs/` 或 `word-study.md`。

- [ ] **Step 6：仅在最终修复产生代码 diff 时提交。**

只显式暂存实际修改的 `server/` 和 `app/` 文件：

```bash
git add -u -- server app
git commit -m "test: complete word study upgrade verification"
```

没有新 diff 时不创建空提交。

---

## 最终验收

1. 新词严格按不可变 `StudyOrder` 推送，编辑单词不改变学习顺序。
2. 用户可连续学习多组，未完成学习组可跨 UTC 日期恢复。
3. 学习和复习使用独立接口，并允许各有一个活动会话。
4. 记忆失败和拼写失败都持久化后移，刷新或跨设备不丢失队列。
5. 拼写由后端规范化校验，客户端在提交前无法取得标准答案。
6. 首次学习安排 1 天后复习；成功按 2/4/7/15/30 天推进；任一失败重置 1 天。
7. 到期复习按积压优先分组，每组默认 50，未完成会话跨日保留。
8. “不再复习”立即移出当前组，用户中心可恢复并从 1 天阶段开始。
9. 收藏与学习状态独立，可在学习/复习和用户中心切换。
10. 累计学习数和 UTC 今日学习数口径正确，不被复习或失败重试重复累计。
11. 旧开发学习数据被破坏式清空，用户、单词、释义、例句和音频保留。
12. 后端完整测试/构建、迁移脚本、用户端 lint/test/build/format 全部通过。
13. 所有实现提交不包含 `docs/` 或 `word-study.md`。
