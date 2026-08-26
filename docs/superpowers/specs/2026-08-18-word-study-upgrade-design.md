# 单词背诵模块升级设计

## 目标

将现有基础单词背诵会话升级为由后端统一维护状态的两阶段学习系统，支持顺序学习新词、按固定遗忘曲线复习旧词、记忆与拼写轮播、收藏、停止复习和学习统计。

本次改造覆盖 `/app` 用户端和 `/server` 后端，不修改 `/admin` 管理端的单词维护流程。系统继续只支持一种学习语言，所有日期边界继续使用 UTC。

## 已确认的业务规则

### 新词学习

- 新词必须按数据库中的固定顺序推送，不允许随机选择。
- `words` 表增加不可变的递增 `StudyOrder`，由 PostgreSQL 生成。
- 管理员编辑单词不会改变 `StudyOrder`。
- 用户每次按 `DailyWordStudyCount` 领取一组未学习单词。
- 同一天允许完成多组，不设置额外的每日硬上限。
- 完成一组后可以选择“再学一组”，继续领取后续未学习单词。
- 未完成的新词会话可以跨 UTC 日期继续；完成当前活动组之前不能领取下一组。
- 单词计入实际完成拼写的 UTC 日期，而不是会话创建日期。

### 旧词复习

- 新词首次完整完成后，安排 1 天后的首次复习。
- 固定复习间隔依次为 1、2、4、7、15、30 天。
- 完成 30 天阶段后，后续每次成功均安排 30 天后复习。
- 复习中曾选择“没记住”或曾拼写错误，本次复习即记为失败；完成本轮后重置为 1 天后复习。
- 只有记忆阶段一次记住且拼写阶段一次正确，才进入下一复习阶段。
- 复习词按 `NextReviewAt` 升序、`StudyOrder` 升序领取，逾期最久的词优先。
- 每组最多领取 `DailyWordReviewCount` 个到期词，默认 50，允许范围为 1 到 200。
- 未完成的复习会话可以跨 UTC 日期继续，不清空、不判失败。
- 完成一组后若仍有积压，可以选择“再复习一组”。
- 尚未领取或尚未完成的词保持到期状态，之后继续优先出现。

### 记忆与拼写轮播

- 新词学习和旧词复习都先进行记忆阶段，再进行拼写阶段。
- 记忆阶段选择“记住了”后，该词通过当前阶段。
- 记忆阶段选择“没记住”后，记录失败并将该词移动到记忆队列末尾。
- 所有有效项目通过记忆阶段后，会话原子切换到拼写阶段。
- 拼写阶段只向客户端返回词性和释义，不返回词头、单词音频、例句或其他可能泄露答案的内容。
- 拼写答案由后端校验：忽略首尾空格和大小写，先进行 Unicode 规范化；重音符号、连字符和撇号必须匹配。
- 拼写错误后记录失败并将该词移动到拼写队列末尾。
- 所有有效项目拼写正确后，会话完成。
- 两个阶段的当前队首、失败记录和轮播顺序均由后端持久化。
- 页面刷新、重新登录或更换设备后必须恢复相同的阶段和队列状态。
- 活动会话只允许操作当前队首，不允许客户端自由跳转到未来项目。

### 停止复习

- 复习界面允许用户将当前词标记为“不再复习”，操作前显示确认弹窗。
- 确认后，会话项目立即标记为 `Excluded` 并移出当前记忆或拼写队列。
- 对应进度设置 `IsReviewExcluded = true`，记录排除时间并清空 `NextReviewAt`。
- 排除操作不计为复习成功或失败。
- 如果该词是本组最后一个待处理项目，会话正常完成。
- 用户中心提供“已停止复习”分页列表。
- 用户恢复复习后，取消排除状态并从 1 天阶段重新安排。
- 恢复操作不会修改已经完成的旧会话。

### 收藏

- 新词学习和旧词复习界面都允许收藏或取消收藏当前词。
- 收藏状态与学习进度、复习阶段和停止复习状态相互独立。
- 用户中心提供分页收藏列表，显示单词、释义和音频入口。
- 从收藏列表移除只删除用户收藏关系，不删除单词或学习进度。

### 统计

- 累计学习单词数是已经完整完成过新词记忆和拼写的去重单词数。
- 今日学习单词数是当前 UTC 日期内首次完整完成的新词数。
- 旧词复习、失败重试和重复拼写不增加学习单词数。
- “再学一组”中新完成的单词计入今日学习数。
- 统计在单词模块首页和用户中心摘要中展示。

## 架构

底层使用统一会话引擎，通过会话类型区分新词学习和旧词复习，通过会话阶段区分记忆和拼写。学习与复习对外使用独立 API，避免前端混用两类数据；内部复用队列推进、阶段切换、内容可见性、并发和拼写校验逻辑。

每个用户最多同时存在一个活动学习会话和一个活动复习会话。两类活动会话互不阻塞，但同一类型必须完成当前组后才能领取下一组。

所有推进操作均以数据库事务为边界。服务端验证用户所有权、会话类型、活动状态、当前阶段、当前队首和项目并发标识，然后更新队列、项目、会话及用户进度。

## 数据模型

### Word

增加：

- `StudyOrder: long`：数据库生成、不可变、唯一，用于新词学习顺序。

### User

保留：

- `DailyWordStudyCount`：每组新词数量，范围 1 到 100。

增加：

- `DailyWordReviewCount`：每组复习数量，默认 50，范围 1 到 200。

### UserWordProgress

每个 `(UserId, WordId)` 保持唯一，重构为：

- `FirstStudiedAt`：首次完整完成新词学习的时间。
- `LastStudiedAt`：最近一次完成学习或复习的时间。
- `LastReviewedAt`：最近一次完成复习的时间，可空。
- `ReviewStage`：当前固定复习阶段，范围 0 到 5。
- `NextReviewAt`：下次复习时间；停止复习时为空。
- `ReviewCount`：已完成复习次数。
- `SuccessfulReviewCount`：成功复习次数。
- `FailedReviewCount`：失败复习次数。
- `IsReviewExcluded`：是否停止后续复习。
- `ReviewExcludedAt`：停止复习时间，可空。
- `ConcurrencyStamp`：进度并发标识。

阶段语义：首次学习后 `ReviewStage = 0` 且 `NextReviewAt = 完成时间 + 1 天`；阶段 0 成功后进入阶段 1 并加 2 天，依次推进到阶段 5；阶段 5 成功后保持阶段 5 并加 30 天；任一失败完成后重置到阶段 0 并加 1 天。

### WordStudySession

保留用户、数量和生命周期时间，重构或增加：

- `SessionType: Learning | Review`
- `Phase: Memorization | Spelling`
- `Status: Active | Completed`
- `RequestedCount`
- `ActualCount`
- `StartedAt`
- `CompletedAt`
- `ConcurrencyStamp`

移除旧的随机选择、包含已学单词、每日唯一会话和放弃状态语义。建立两个过滤唯一索引，分别限制每个用户最多一个活动 `Learning` 会话和一个活动 `Review` 会话。

### WordStudySessionItem

每个会话中的 `WordId` 保持唯一，保存：

- `Position`：创建会话时的稳定原始位置。
- `Status: Pending | Completed | Excluded | Skipped`
- `MemorizationQueueOrder`
- `MemorizationAttemptCount`
- `HadMemorizationFailure`
- `MemorizationPassedAt`
- `SpellingQueueOrder`
- `SpellingAttemptCount`
- `HadSpellingFailure`
- `CompletedAt`
- `SkipReason`
- `ConcurrencyStamp`

记忆阶段按 `MemorizationQueueOrder` 取队首，拼写阶段按 `SpellingQueueOrder` 取队首。失败时使用当前最大队列序号加一完成后移。实时不可见或已删除的词标记为 `Skipped`，不创建或更新用户学习进度。

### UserWordFavorite

新增用户收藏关系：

- `Id`
- `UserId`
- `WordId`
- `CreatedAt`

对 `(UserId, WordId)` 建立唯一索引；用户删除时级联删除收藏，单词删除时级联删除对应收藏。

## 服务与 API

### 新词学习

- `GET /word-study/learning/overview`
  - 返回累计学习数、今日学习数、活动学习会话、是否还有未学词。
- `POST /word-study/learning/sessions`
  - 有活动学习会话时幂等返回现有会话。
  - 否则按 `StudyOrder` 领取下一组未学习词。
- `GET /word-study/learning/sessions/{sessionId}`
  - 返回可恢复的会话摘要和当前阶段队首。
- `POST /word-study/learning/sessions/{sessionId}/items/{itemId}/memorization`
  - 提交 `Remembered | Forgotten`。
- `POST /word-study/learning/sessions/{sessionId}/items/{itemId}/spelling`
  - 提交拼写文本并返回 `Correct | Incorrect` 及最新会话状态。

### 旧词复习

- `GET /word-study/review/overview`
  - 返回全部到期数量、逾期数量、活动复习会话和本组进度。
- `POST /word-study/review/sessions`
  - 有活动复习会话时幂等返回现有会话。
  - 否则按到期时间和单词顺序领取一组。
- `GET /word-study/review/sessions/{sessionId}`
- `POST /word-study/review/sessions/{sessionId}/items/{itemId}/memorization`
- `POST /word-study/review/sessions/{sessionId}/items/{itemId}/spelling`
- `POST /word-study/review/sessions/{sessionId}/items/{itemId}/exclude`

学习和复习使用分开的 endpoint 路径和服务入口，但共享内部会话推进器。每个命令携带项目并发标识，只允许当前阶段的当前队首提交。成功响应直接返回最新会话摘要和下一队首；陈旧提交返回稳定的并发冲突错误，客户端重新获取当前状态。

记忆阶段当前项返回完整词条内容和音频资源 ID。拼写阶段当前项只返回 `WordId`、词性和释义。后端不在拼写响应中返回标准词头。

### 用户设置、收藏和停止复习

- 扩展 `GET/PUT /users/me/word-study-settings`，增加 `dailyWordReviewCount`。
- `GET /users/me/word-favorites?page=&pageSize=`：分页查询收藏。
- `PUT /users/me/word-favorites/{wordId}`：幂等收藏。
- `DELETE /users/me/word-favorites/{wordId}`：幂等取消收藏。
- `GET /users/me/word-review-exclusions?page=&pageSize=`：分页查询停止复习词。
- `DELETE /users/me/word-review-exclusions/{wordId}`：恢复复习并安排 1 天后到期。

现有音频播放接口继续负责签发单词和例句音频播放地址。

到期数量统计 `NextReviewAt <= 当前时间` 的词；逾期数量统计
`NextReviewAt < UTC 当日零点` 的跨日积压词。

## 用户端设计

### 单词模块首页

`/words` 展示两个明确入口：

- 新词学习：今日学习数、累计学习数、活动组状态和开始/继续按钮。
- 旧词复习：当前到期总数、活动组状态和开始/继续按钮。

首页直接提供任务状态，不制作营销式介绍页面。

### 学习工作区

新词与复习复用一个阶段化工作区组件，但页面标题、统计和可用操作由模式决定。

记忆阶段展示：

- 当前单词和阶段进度。
- 单词音频按钮。
- 有序释义、用法、例句及例句音频。
- 收藏切换按钮。
- “没记住”和“记住了”操作。
- 复习模式额外提供“不再复习”，并使用确认弹窗。

拼写阶段展示：

- 当前阶段和本组进度。
- 词性与释义。
- 拼写输入框和提交按钮。
- 正确或错误反馈。
- 收藏切换按钮。

活动会话不展示可自由跳转的未来词单。用户离开页面后，重新进入时从后端恢复当前队首。完成页面可只读浏览本组结果，并根据剩余词量显示“再学一组”或“再复习一组”。

### 用户中心

- 扩展现有设置面板，同时编辑每组新词数量和每组复习数量。
- 展示累计学习数和今日学习数。
- 增加分页收藏本，支持播放单词音频和取消收藏。
- 增加分页“已停止复习”列表，支持恢复复习。

## 错误与并发

- 无可学习新词时返回明确的 `WordStudyNoEligibleWords` 类业务错误，并在首页显示词库已学完状态。
- 无到期复习词时返回空闲状态，不创建空会话。
- 会话类型、阶段或队首不匹配时返回稳定冲突错误。
- 项目并发标识过期时返回并发冲突，前端自动重新获取当前会话。
- 拼写答案为空或超过限制时返回字段校验错误。
- 单词在会话期间变为不可见时，后端在事务内跳过该项目并继续寻找下一项。
- 收藏和取消收藏采用幂等语义。
- 恢复未排除的词采用幂等语义。

## 数据迁移

本次仍处于开发阶段，现有学习数据没有保留价值。迁移将清空并重建：

- `word_study_session_items`
- `word_study_sessions`
- `user_word_progress`

保留用户、单词、释义、例句和音频数据。为现有单词生成稳定 `StudyOrder`，以当前 `CreatedAt`、`Id` 顺序初始化；以后由数据库序列生成。

## 测试策略

### 后端

- 模型和数据库约束测试：顺序字段、活动会话唯一性、进度阶段范围、收藏唯一性。
- 新词选择测试：严格按 `StudyOrder`、排除已学词、多组连续领取、跨日恢复。
- 复习选择测试：到期过滤、排除过滤、积压分批、到期时间和顺序稳定性。
- 状态机测试：记忆失败后移、阶段切换、拼写失败后移、会话完成。
- 拼写规范化测试：空格、大小写、Unicode、重音、连字符和撇号。
- 复习调度测试：1/2/4/7/15/30 天推进、30 天封顶、任一失败重置。
- 停止与恢复复习测试。
- 收藏、统计、用户隔离和授权测试。
- 并发测试：陈旧队首、重复提交、阶段竞争和唯一索引竞争。
- endpoint 契约测试：学习与复习路由严格分离，拼写响应不泄露词头。

### 用户端

- API 契约与缓存失效测试。
- 首页两个入口、统计和空状态测试。
- 记忆轮播、拼写轮播、跨刷新恢复和完成后继续下一组测试。
- 拼写阶段无答案泄露测试。
- 收藏切换和错误回滚测试。
- “不再复习”确认、移出队列和恢复列表测试。
- 设置范围和保存测试。
- 收藏本、停止复习列表和统计摘要测试。
- 窄屏和桌面布局的关键交互测试。

## 非目标

- 不增加管理员端学习进度管理功能。
- 不增加用户自定义时区，日期继续按 UTC 划分。
- 不实现 SM-2 或动态难度算法。
- 不保留旧学习会话和旧用户单词进度。
- 不增加单词派生词功能。
- 不在本次实现单词听写或文章听写。
