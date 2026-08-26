# 单词今日回顾与打卡 Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

Goal: 为单词背诵模块增加今日学习回顾、UTC 打卡日历和连续打卡统计，并将个人主页中的学习设置整理为集中折叠区。

Architecture: 保留现有学习/复习会话状态机，在会话项目完成的同一数据库事务中追加不可变的学习活动流水；新词组完成时幂等写入用户 UTC 自然日打卡记录，复习完成只写活动。通过独立的查询服务返回分页回顾和月份日历，前端复用现有音频播放、收藏 mutation 和页面查询基础设施。

Tech Stack: ASP.NET Core Minimal API、EF Core/Npgsql、FluentValidation、xUnit、React、TypeScript、Redux Toolkit Query、Tailwind/DaisyUI、Vitest/Testing Library。

---

## 文件边界

后端新增 WordStudyActivity、WordStudyCheckIn 实体及其枚举、EF 配置和破坏式迁移；扩展 IApplicationDbContext、ApplicationDbContext、WordStudyDtos、IWordStudyService、WordStudyService、WordStudySessionEngine 和 WordStudyEndpoints。学习活动写入逻辑属于会话引擎，查询和连续天数计算属于 WordStudyService，避免让 Minimal API 直接访问数据库。

前端扩展 wordStudyTypes.ts 和 wordStudyApi.ts，新增职责单一的 WordStudyTodayReview.tsx、WordStudyCheckInCalendar.tsx，并在 WordsPage.tsx、WordStudyCompletion.tsx、WordStudyWorkspace.tsx 中组合；ProfilePage.tsx 只负责把已有学习统计、设置、收藏和停止复习面板放入“单词学习”分组，设置面板本身继续负责草稿、校验和保存。

---

### Task 1: 建立活动流水和 UTC 打卡数据模型

Files:
- Create: server/TinyLang/Entities/Enums/WordStudyActivityType.cs
- Create: server/TinyLang/Entities/WordStudyActivity.cs
- Create: server/TinyLang/Entities/WordStudyCheckIn.cs
- Create: server/TinyLang/Database/Configurations/WordStudyActivityConfiguration.cs
- Create: server/TinyLang/Database/Configurations/WordStudyCheckInConfiguration.cs
- Modify: server/TinyLang/Interfaces/IApplicationDbContext.cs
- Modify: server/TinyLang/Infrastructure/ApplicationDbContext.cs（按仓库实际路径定位 ApplicationDbContext）
- Test: server/TinyLang.UnitTests/WordStudyModelTests.cs
- Create: EF migration 文件及其 Designer/Snapshot 更新（名称使用当前日期和 AddWordStudyActivityAndCheckIns）

- [ ] Step 1: 写模型失败测试

在 WordStudyModelTests.cs 增加测试，验证 WordStudyActivityType 只包含 Learning、Review，活动实体初始化 Id/CreatedAt 所需属性，打卡实体包含 StudyDateUtc、CheckedInAtUtc，并验证活动 (SessionItemId, ActivityType) 与打卡 (UserId, StudyDateUtc) 是唯一约束。

- [ ] Step 2: 运行模型测试确认失败

运行 dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyModelTests；预期新增类型和配置尚不存在导致编译失败。

- [ ] Step 3: 实现实体、枚举和配置

活动实体包含 UserId、WordId、SessionId、SessionItemId、ActivityType、CompletedAtUtc、审计时间和导航属性；打卡实体包含 UserId、StudyDateUtc、CheckedInAtUtc、审计时间。配置将枚举按字符串转换，活动建立唯一键 (SessionItemId, ActivityType)、查询索引 (UserId, CompletedAtUtc, WordId)，打卡建立唯一键/索引 (UserId, StudyDateUtc)；用户、会话、会话项级联删除，Word 使用现有墓碑策略所需的删除行为，确保活动行不会阻止词条删除且查询会过滤不可见词。

- [ ] Step 4: 注册 DbSet 并生成破坏式迁移

将 DbSet<WordStudyActivity> WordStudyActivities、DbSet<WordStudyCheckIn> WordStudyCheckIns 加入 IApplicationDbContext 和实现类，运行 dotnet ef migrations add AddWordStudyActivityAndCheckIns --project server/TinyLang --startup-project server/TinyLang。确认迁移只新增两张表、索引和外键，不修改历史迁移；开发数据库允许执行 dotnet ef database update。

- [ ] Step 5: 运行模型测试并提交

运行 dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyModelTests，预期 PASS；提交 feat(server): add word study activity and check-in models，不要加入 docs/。

### Task 2: 定义回顾/日历 DTO、校验和服务查询契约

Files:
- Modify: server/TinyLang/Dtos/WordStudyDtos.cs
- Create: server/TinyLang/Dtos/WordStudyDailyReviewValidators.cs
- Modify: server/TinyLang/Services/IWordStudyService.cs
- Modify: server/TinyLang/Services/WordStudyService.cs
- Modify: server/TinyLang/Exceptions/ErrorCodes.cs（仅在现有错误码模式下补充日期/月/分页参数错误）
- Test: server/TinyLang.UnitTests/WordStudyValidatorsTests.cs
- Test: server/TinyLang.UnitTests/WordStudyDailyReviewServiceTests.cs

- [ ] Step 1: 写 DTO/校验失败测试

覆盖 page >= 1、1 <= pageSize <= 100、1 <= month <= 12、年份在 DateTimeOffset 可表示范围内，以及无效参数返回现有验证错误格式。

- [ ] Step 2: 定义稳定的响应类型

在 WordStudyDtos.cs 增加 WordStudyTodayReviewResponse(StudyDateUtc, Items, Page, PageSize, TotalCount, TotalPages)、WordStudyTodayReviewItemResponse(WordId, Headword, ActivityType, CompletedAtUtc, Senses, Examples, AudioResourceId, IsFavorite)、WordStudyCheckInResponse(StudyDateUtc, CheckedInAtUtc)、WordStudyCheckInCalendarResponse(Year, Month, CheckedInDates, CurrentStreak, LongestStreak, TotalCheckInDays)。释义和例句结构复用现有 WordSenseResponse/ExampleSentenceResponse，不复制另一套音频 DTO。

- [ ] Step 3: 实现查询契约

在 IWordStudyService 增加 GetTodayReviewAsync(userId, page, pageSize, ct) 与 GetCheckInCalendarAsync(userId, year, month, ct)；在 WordStudyService 中以 _timeProvider.GetUtcNow() 的 UTC 日期计算 [dayStart, dayStart.AddDays(1))，回顾查询 join 当前可见单词、按 CompletedAtUtc DESC、WordId ASC、Id ASC 排序，保留同词的 Learning/Review 两条活动，投影有序释义/例句、音频资源 ID 和用户收藏状态，再做 Count/Skip/Take。

- [ ] Step 4: 实现打卡统计算法并写测试

查询指定月份的日期；对全部历史日期排序去重，当前连续天数从今天开始，今天未打卡则从昨天开始，遇到缺口停止；最长连续天数和累计天数基于全部历史日期计算。测试 UTC 跨日、月份边界、今天未打卡、孤立日期、用户隔离、不可见单词过滤和稳定分页。

- [ ] Step 5: 运行服务测试并提交

运行 dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudyDailyReview；预期新增查询和统计测试 PASS。提交 feat(server): add daily review and check-in query contracts。

### Task 3: 在学习/复习完成事务中写入活动和打卡

Files:
- Modify: server/TinyLang/Services/WordStudySessionEngine.cs
- Modify: server/TinyLang/Services/WordStudyService.cs
- Test: server/TinyLang.UnitTests/WordStudySessionEngineTests.cs
- Test: server/TinyLang.UnitTests/WordStudyLearningServiceTests.cs
- Test: server/TinyLang.UnitTests/WordStudyReviewServiceTests.cs

- [ ] Step 1: 写失败测试

为新词最后一个有效项目增加断言：同一事务写入一条 Learning 活动和一条当天 WordStudyCheckIn；重复命令、重复完成和唯一键竞争不产生第二条。为复习完成增加断言：写入一条 Review 活动且不写打卡。测试活动/打卡写入失败时会话状态和用户进度一起回滚，测试 UTC 午夜前后使用 TestTimeProvider。

- [ ] Step 2: 抽出幂等写入辅助方法

在 WordStudySessionEngine 内增加私有方法 AddActivityIfMissingAsync 和 AddCheckInIfMissingAsync，键分别使用 SessionItemId + ActivityType、UserId + StudyDateUtc；先查询现有行，插入时捕获数据库唯一冲突并清理跟踪状态后按成功处理。方法只在当前事务中调用，不打开第二事务。

- [ ] Step 3: 接入四类完成路径

在学习记忆/拼写命令确认项目完成、更新进度后追加 Learning 活动；当学习会话没有剩余有效项目时，以同一 UTC 日期调用打卡写入。复习记忆/拼写命令追加 Review 活动但跳过打卡；跳过、排除和规范化路径不新增活动。保持现有 SaveChangesAsync 和事务提交边界，使任一写入失败都回滚。

- [ ] Step 4: 验证并发和回归

运行 dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudy(SessionEngine|LearningService|ReviewService)；再运行完整后端单元测试。确认原有会话并发冲突错误码不变，提交 feat(server): record word study activities and check-ins atomically。

### Task 4: 暴露认证 API 并锁定 OpenAPI 契约

Files:
- Modify: server/TinyLang/Endpoints/WordStudyEndpoints.cs
- Modify: server/TinyLang/Program.cs 或现有 OpenAPI 配置（仅在需要显式响应类型时）
- Test: server/TinyLang.UnitTests/WordStudyEndpointTests.cs
- Test: server/TinyLang.UnitTests/OpenApiContractTests.cs

- [ ] Step 1: 写 endpoint 失败测试

验证认证用户可以调用 GET /word-study/review/today?page=1&pageSize=20 和 GET /word-study/check-ins?year=2026&month=8，匿名请求被现有 RequireUser 策略拒绝，响应 JSON 字段与 DTO 命名一致。

- [ ] Step 2: 注册路由和参数校验

在现有 /word-study 认证组中映射两个 GET；调用 EndpointIdentity.GetUserId，将查询参数绑定到请求记录并交给 FluentValidation/现有验证过滤器。默认 page=1,pageSize=20，月份接口使用当前 UTC 年月作为缺省值，错误响应沿用全局异常处理器。

- [ ] Step 3: 更新契约测试

断言 OpenAPI 包含两个路径、query 参数和 200/400/401 响应；断言用户 A 无法读取用户 B 的回顾或日历。运行 dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~(WordStudyEndpointTests|OpenApiContractTests)，预期 PASS；提交 feat(server): expose word study review and check-in endpoints。

### Task 5: 扩展前端 API 类型和今日回顾组件

Files:
- Modify: app/src/features/wordStudy/wordStudyTypes.ts
- Modify: app/src/features/wordStudy/wordStudyApi.ts
- Create: app/src/features/wordStudy/WordStudyTodayReview.tsx
- Create: app/src/features/wordStudy/WordStudyTodayReview.test.tsx
- Modify: app/src/features/audio/AudioPlaybackButton.tsx（仅在需要兼容回顾项的现有 prop 时）

- [ ] Step 1: 写组件失败测试

覆盖 loading skeleton、空状态、错误重试、分页按钮、Learning/Review 标签、释义/例句渲染、单词与例句音频按钮、收藏乐观更新失败回滚和窄屏不溢出。使用现有 authTestUtils、RTK Query mock 和 AudioPlaybackButton 测试模式。

- [ ] Step 2: 扩展类型和 API endpoints

增加 WordStudyTodayReviewItem、WordStudyTodayReview、WordStudyCheckInCalendar 类型；为 wordStudyApi 增加 TodayReview、CheckInCalendar tag，查询 /word-study/review/today 和 /word-study/check-ins，导出 useGetTodayReviewQuery、useGetCheckInCalendarQuery。完成新词/复习 mutation 后只 invalidate 今日回顾和日历相关 tag，不影响已有会话缓存策略。

- [ ] Step 3: 实现回顾列表

组件默认每页 20；按后端 page/totalPages 渲染上一页/下一页，独立处理请求错误；释义按 sortOrder 展示，例句显示翻译；存在 audioResourceId 时传给 AudioPlaybackButton，收藏按钮调用 useSetFavoriteMutation，失败时仅回滚当前条目并显示错误。

- [ ] Step 4: 运行前端测试并提交

运行 cd app && npm test -- --run src/features/wordStudy/WordStudyTodayReview.test.tsx src/features/wordStudy/wordStudyApi.test.ts，再运行 npm run lint；预期 PASS，提交 feat(app): add paged today word review。

### Task 6: 实现 UTC 打卡日历组件

Files:
- Create: app/src/features/wordStudy/WordStudyCheckInCalendar.tsx
- Create: app/src/features/wordStudy/WordStudyCheckInCalendar.test.tsx
- Modify: app/src/features/wordStudy/wordStudyTypes.ts、wordStudyApi.ts（若 Task 5 未完成）

- [ ] Step 1: 写失败测试

验证当前月份初始加载、前后月份切换、已打卡日期标记、当前/最长/累计统计显示、月份查询失败重试、按钮 aria-label 和 UTC 日期格式化不会因浏览器本地时区提前/延后一天。

- [ ] Step 2: 实现日历网格

使用 year/month 查询参数，根据 UTC 年月计算网格前导空格和当月天数；日期键只使用后端 YYYY-MM-DD，不调用 new Date("YYYY-MM-DD") 进行本地时区转换；切换月份时保留旧数据直到新请求完成并显示加载状态，错误提供独立重试。

- [ ] Step 3: 运行测试并提交

运行 cd app && npm test -- --run src/features/wordStudy/WordStudyCheckInCalendar.test.tsx，再运行 npm run format:check（按 package.json 实际脚本调整）；预期 PASS，提交 feat(app): add utc word study check-in calendar。

### Task 7: 接入单词首页和学习完成页

Files:
- Modify: app/src/pages/WordsPage.tsx
- Modify: app/src/features/wordStudy/WordStudyCompletion.tsx
- Modify: app/src/features/wordStudy/WordStudyWorkspace.tsx
- Modify: app/src/pages/WordLearningPage.tsx
- Modify: app/src/pages/WordReviewPage.tsx
- Modify: app/src/pages/WordsPage.test.tsx
- Modify: app/src/pages/WordLearningPage.test.tsx
- Modify: app/src/pages/WordReviewPage.test.tsx
- Modify: app/src/features/wordStudy/WordStudyWorkspace.test.tsx

- [ ] Step 1: 写接入失败测试

验证 /words 顺序为任务区、日历、今日回顾；完成页在会话状态为 Completed 时直接出现今日回顾，加载失败不会隐藏完成摘要和返回/继续按钮；新词完成后日历刷新，复习完成后仅回顾刷新；空状态和重试可用。

- [ ] Step 2: 让完成组件接收复用的回顾区域

在 WordStudyCompletion 中渲染 WordStudyTodayReview，由 WordStudyWorkspace 传递 session type 或使用当前认证查询；不要把完成页的回顾数据复制到学习/复习页面。保留已有“再学一组/再复习一组”和“返回单词首页”操作。

- [ ] Step 3: 组合首页区域并刷新缓存

在 WordsPage 添加 WordStudyCheckInCalendar 和 WordStudyTodayReview，为回顾区域提供标题、分页和独立状态；在学习/复习完成 mutation 成功后依靠 RTK Query tag invalidation 自动重新取数，不引入全局事件总线。

- [ ] Step 4: 运行页面测试并提交

运行 cd app && npm test -- --run src/pages/WordsPage.test.tsx src/pages/WordLearningPage.test.tsx src/pages/WordReviewPage.test.tsx src/features/wordStudy/WordStudyWorkspace.test.tsx，再运行 npm run build；预期 PASS，提交 feat(app): integrate review and check-in into study flows。

### Task 8: 整理个人主页中的学习设置

Files:
- Modify: app/src/pages/ProfilePage.tsx
- Modify: app/src/features/wordStudy/WordStudySettingsPanel.tsx
- Modify: app/src/pages/ProfilePage.test.tsx
- Modify: app/src/features/wordStudy/WordStudySettingsPanel.test.tsx

- [ ] Step 1: 写布局和状态测试

验证个人主页存在“单词学习”分组，统计、收藏、停止复习和设置集中在该分组；设置折叠区默认收起，展开后仍能编辑新词/复习数量；保存失败保留草稿并显示错误；账户安全不进入单词分组。

- [ ] Step 2: 实现分组和折叠区

在 ProfilePage 用一个有标题的学习区域包裹四个现有面板，将 WordStudySettingsPanel 放进 details/现有项目折叠模式并设置 open=false 的受控状态；保持所有原有 hooks、字段校验、保存 mutation 和错误映射不变。调整间距和边框，避免卡片嵌套卡片，确保窄屏下按钮和文本不溢出。

- [ ] Step 3: 运行测试并提交

运行 cd app && npm test -- --run src/pages/ProfilePage.test.tsx src/features/wordStudy/WordStudySettingsPanel.test.tsx 和 npm run lint；预期 PASS，提交 refactor(app): organize word study settings on profile。

### Task 9: 全量验证、迁移检查和交付审查

Files:
- Modify only files identified by failing verification; do not modify historical migrations.
- Do not add or commit docs/ or .superpowers/.

- [ ] Step 1: 后端完整验证

运行 dotnet build server/TinyLang.sln、dotnet test server/TinyLang.sln、dotnet ef migrations list --project server/TinyLang --startup-project server/TinyLang；确认迁移可应用到空开发数据库，API 使用 UTC，所有认证端点隔离用户数据。

- [ ] Step 2: 前端完整验证

运行 cd app && npm test -- --run、npm run lint、npm run build、npm run format:check（以实际 package scripts 为准）；重点检查页面窄屏渲染、音频按钮可访问名称和回顾分页。

- [ ] Step 3: 检查工作区和提交边界

运行 git status --short、git diff --check、git diff --stat；确认仅业务代码、测试和迁移进入提交，docs/ 与 .superpowers/ 保持未跟踪。按任务粒度完成剩余提交，并在交付说明中列出迁移命令、测试结果和任何环境限制。

---

## 自审结果

- 今日回顾：Task 2、Task 5、Task 7 覆盖活动类型、分页、音频、例句、收藏、完成页和首页入口。
- 打卡规则与 UTC 连续统计：Task 1、Task 2、Task 3、Task 6 覆盖唯一性、最后一组新词打卡、复习不打卡、当前/最长/累计统计和时区边界。
- 用户隔离与认证：Task 2、Task 4、Task 9 覆盖查询过滤、RequireUser、OpenAPI 和契约。
- 学习设置整理：Task 8 覆盖主页分组、默认折叠、草稿保留和账户安全边界。
- 音频播放：Task 5/7 明确复用现有 AudioPlaybackButton，没有新增播放器或改变资源访问策略。
- 数据库与文档边界：Task 1 只新增迁移，Task 9 明确不提交 docs/ 和 .superpowers/。

## Execution Handoff

计划已完成并保存至 docs/superpowers/plans/2026-08-18-word-study-daily-review-checkin.md。有两种执行方式：

1. Subagent-Driven（推荐） - 每个任务分派新的子代理，任务之间进行审查，迭代速度快。
2. Inline Execution - 在当前会话中使用 executing-plans，按批次执行并设置检查点。

请选择执行方式。
