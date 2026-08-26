# 例句共享音频支持设计规格

**日期：** 2026-08-17  
**项目：** tiny-lang

## 1. 背景与目标

单词模块已经完成共享音频适配，但例句目前仍只有原文、译文和排序字段。管理员无法为例句关联已有音频或上传新的音频资源，用户端单词学习卡片也无法播放例句读音。

本阶段为每条例句增加一个可空的共享音频关联。管理员可以从音频资源库选择，也可以从本地上传并立即关联；用户可以在单词学习卡片中播放有关联音频的例句。音频的上传、处理、播放和删除保护继续由独立 `AudioResource` 模块负责。

## 2. 已确认约束

- 每条例句最多关联一个音频资源。
- 例句音频允许为空。
- 同一个音频资源可以被多条例句复用。
- 管理员可以关联任意处理状态的音频，只要求资源存在。
- 只有管理员可以上传、选择、替换和解除例句音频关联。
- 用户端只在单词学习卡片中为有关联音频的例句显示播放按钮。
- 用户点击播放按钮时才请求共享播放地址，不预加载例句音频。
- 删除例句或解除关联不会删除共享音频资源。
- 删除被例句引用的音频资源必须返回现有 `AudioInUse` 冲突错误。
- 数据库中的现有单词域数据没有保留价值，migration 可以进行破坏式更新。
- 破坏式清理只覆盖单词、释义、例句和单词学习数据，不触碰文章、音频资源、用户、试卷或视频模块。

## 3. 方案选择

采用在 `ExampleSentence` 上直接保存可空音频外键的方案：

```csharp
public Guid? AudioResourceId { get; set; }
public AudioResource? AudioResource { get; set; }
```

不抽取新的通用音频关联领域实体，也不建立例句音频中间表。当前关系明确为“一条例句最多一个音频”，直接外键能够保持模型、查询和删除约束简单。

管理端新增领域文案专用的 `ExampleSentenceAudioControl`，内部继续组合共享的 `AudioResourcePickerDialog` 和 `AudioUploadControl`。本阶段不重构已经稳定的 `WordAudioControl`，避免为了少量 UI 重复扩大回归范围。

## 4. 领域模型与数据库

### 4.1 ExampleSentence

`ExampleSentence` 保留现有 `WordSenseId`、原文、译文和排序字段，并增加：

- `Guid? AudioResourceId`
- `AudioResource? AudioResource`

例句到音频资源是单向多对一关系。音频模块不增加例句反向导航，消费者模块通过外键引用共享资源。

### 4.2 EF Core 配置

`ExampleSentenceConfiguration` 增加：

- `AudioResourceId` 普通索引。
- `ExampleSentence.AudioResourceId -> AudioResource.Id` 外键。
- 删除行为为 `DeleteBehavior.Restrict`。

删除例句时，例句记录随释义或单词聚合删除，音频资源保持不变。删除被一条或多条例句引用的音频时，数据库 Restrict 约束阻止删除，音频服务继续将目标外键冲突映射为 `AudioInUse`。

### 4.3 破坏式 migration

新增 EF Core migration，并在 `Up` 中按依赖顺序清空：

1. `word_study_session_items`
2. `word_study_sessions`
3. `user_word_progress`
4. `example_sentences`
5. `word_senses`
6. `words`

随后为 `example_sentences` 增加可空 `AudioResourceId`、索引和 Restrict 外键。migration 不删除或清空 `audio_resources`，因此已有音频库资源仍可继续使用。

`Down` 同样先清空单词域数据，再移除例句音频外键、索引和字段，保证回滚不受新数据影响。

## 5. 后端契约

### 5.1 写入 DTO

`ExampleSentenceInput` 增加：

```csharp
public Guid? AudioResourceId { get; init; }
```

验证规则：

- `null` 合法。
- `Guid.Empty` 返回 `WordExampleAudioInvalid`。
- 不限制音频资源状态。

创建和更新单词仍采用完整目标集合语义。请求中不存在的例句会被删除，保留例句通过 ID 更新，新例句不携带 ID。

### 5.2 管理端响应

新增例句音频摘要 DTO，包含：

- `id`
- `name`
- `status`
- `durationSeconds`
- `lastFailureCode`

`AdminExampleSentenceResponse` 增加可空 `Audio` 属性。管理端只获得展示和关联所需信息，不暴露对象存储名称、上传地址或处理作业信息。

### 5.3 用户端响应

`ExampleSentenceResponse` 增加：

```csharp
Guid? AudioResourceId
```

该字段自动进入：

- 用户单词详情中的释义和例句。
- 单词学习下一个词响应。
- 单词学习会话项目内容响应。

用户响应不返回例句音频名称、状态或失败代码。播放可用性由共享播放接口统一判断。

### 5.4 OpenAPI

OpenAPI 契约测试需要断言：

- `ExampleSentenceInput.audioResourceId` 是可空 UUID。
- `AdminExampleSentenceResponse.audio` 是可空的例句音频摘要引用。
- `ExampleSentenceResponse.audioResourceId` 是可空 UUID。
- 例句 DTO 不出现旧 `audioClipId`、对象存储字段或上传字段。

## 6. 服务逻辑

### 6.1 音频存在性校验

创建或更新单词前，服务收集请求中所有例句的非空 `AudioResourceId`，去重后使用单次查询检查这些资源是否全部存在。任一资源不存在时返回 `WordExampleAudioInvalid`。

单词自身的 `AudioResourceId` 继续使用现有 `WordAudioInvalid` 语义，例句音频使用独立错误码，保证字段错误能够准确定位。

### 6.2 目标集合更新

`CreateExample` 和 `ApplyExampleValues` 同步例句的 `AudioResourceId`：

- 新建例句保存请求中的可空音频 ID。
- 更新已有例句可以保留、替换或清空音频 ID。
- 删除例句只删除例句记录。
- 例句排序暂存和最终排序逻辑保持不变。

管理详情投影通过例句的 `AudioResource` 导航返回音频摘要；用户详情和学习投影只返回 `AudioResourceId`。

### 6.3 并发与数据库异常

例句音频外键使用明确命名的约束。若资源在存在性检查之后、保存之前被并发删除，保存阶段只在命中该约束时映射为 `WordExampleAudioInvalid`。其他外键或数据库异常不能被误映射。

现有 `ConcurrencyStamp` 继续保护整个单词聚合。例句音频变化与例句文本变化一起参与单词更新请求，不引入独立例句编辑 endpoint。

### 6.4 音频删除

`AudioResourceService.DeleteAsync` 继续依赖数据库 Restrict 外键作为最终一致性保护。被文章、单词或例句引用的音频均返回 `AudioInUse`，且不会删除数据库记录或对象存储文件。

## 7. 管理端设计

### 7.1 表单模型和契约

每个例句表单对象增加：

```js
audio: null
```

加载管理详情时，将服务端例句音频摘要规范化为 `example.audio`。提交时转换为：

```js
audioResourceId: example.audio?.id ?? null
```

音频摘要复用管理端已有的状态、UUID、时长和失败代码校验规则。非法响应必须由 contract normalizer 拒绝。

### 7.2 ExampleSentenceAudioControl

新增 `admin/src/features/words/ExampleSentenceAudioControl.jsx`。控件放在展开后的例句编辑区域中，位于原文和译文输入框之后。

交互包括：

1. 无关联时显示“未关联例句音频”。
2. 管理员可以打开音频资源库选择已有资源。
3. 管理员可以展开本地上传控件。
4. 上传初始化后立即把 `Uploading` 音频写入当前例句表单。
5. 上传完成后使用最新音频摘要刷新表单。
6. 管理员可以解除关联，但不删除音频资源。
7. `Failed` 音频保留关联，并提示前往音频资源库重新上传或处理。

单词保存期间，控件关闭已展开的资源选择器和上传区域，防止保存请求期间的回调继续修改表单。删除例句时只从表单目标集合移除例句。

### 7.3 折叠摘要

折叠后的例句标题区域在有关联音频时显示简洁的“已关联音频”标识，便于管理员在多条例句中快速定位音频状态。无音频时不显示占位标识。

## 8. 用户端设计

### 8.1 类型

用户端 `ExampleSentence` 类型增加：

```ts
audioResourceId: string | null;
```

单词学习 API 的其他类型和路由保持不变。

### 8.2 播放交互

在展开释义后，每条例句的原文右侧显示独立播放入口：

- 仅在 `audioResourceId` 非空时渲染。
- 使用共享 `AudioPlaybackButton` 的 `icon` 模式。
- 向按钮传递当前例句的音频资源 ID。
- 无障碍标签包含例句序号，例如“播放例句 1 音频”。
- 无音频例句保持现有纯文本布局。

用户点击按钮时才请求播放地址。音频非 `Ready`、资源不存在、网络错误和播放器互斥行为继续由共享音频播放模块处理。

## 9. 错误处理

新增：

- `WordExampleAudioInvalid`：例句音频 ID 为空 GUID、资源不存在，或保存期间例句音频外键失效。

继续使用：

- `AudioInUse`：删除被文章、单词或例句引用的音频。
- `AudioNotReady`：用户尝试播放未处理成功的音频。
- `WordConcurrencyConflict`：单词聚合并发标识过期。

管理端字段错误通过现有问题详情路径映射到对应例句的 `audioResourceId`。用户端播放错误继续显示共享播放器定义的稳定提示。

## 10. 测试策略

### 10.1 后端

- EF 模型包含可空例句音频外键、索引和 Restrict 删除行为。
- `Guid.Empty` 返回 `WordExampleAudioInvalid`。
- 无音频例句可以创建和更新。
- 多条例句可以引用同一音频资源。
- `Uploading`、`Processing` 和 `Failed` 音频可以关联。
- 不存在的音频返回 `WordExampleAudioInvalid`。
- 更新可以保留、替换和解除已有例句音频。
- 完整目标集合更新不会丢失未修改例句的音频关联。
- 管理详情返回例句音频摘要。
- 用户详情、下一个学习项和会话项目返回例句 `audioResourceId`。
- 被例句引用的音频删除返回 `AudioInUse`。
- 保存期间音频被并发删除时返回 `WordExampleAudioInvalid`。
- 非目标数据库异常不会被误映射。

### 10.2 管理端

- contract normalizer 接受合法的可空例句音频摘要并拒绝非法数据。
- 可以从音频库选择、从本地上传、解除关联和展示失败状态。
- 保存期间已展开的选择器和上传区域会关闭。
- 编辑器正确加载已有例句音频。
- 创建和更新 payload 包含每条例句的可空 `audioResourceId`。
- 折叠摘要只在有关联音频时显示。
- 删除例句不会触发音频删除请求。

### 10.3 用户端

- 有音频的例句显示图标播放按钮。
- 无音频的例句不显示按钮。
- 多条例句的播放按钮具有可区分的标签并传递正确资源 ID。
- 例句文本、译文、释义折叠和学习结果操作保持正常。
- 单词自身的音频播放不受例句音频改动影响。

### 10.4 migration 与全量验收

- migration 升级和降级 SQL 均先清空目标单词域数据。
- 幂等 SQL 只变更单词域表和 `example_sentences.AudioResourceId`。
- migration 不删除文章、音频资源、用户、试卷或视频表。
- 运行后端完整测试和构建。
- 运行管理端完整测试、lint 和生产构建。
- 运行用户端完整测试、lint、TypeScript 和生产构建。
- 扫描生产代码，确认不存在旧 `audioClipId` 例句契约。

## 11. 交付边界

实现按以下阶段推进，每个阶段完成并验证后停止，由用户检查并手动提交：

1. 后端实体、DTO、验证器、服务和 API 契约。
2. 破坏式数据库 migration。
3. 管理端例句音频控件和编辑器接入。
4. 用户端例句音频播放。
5. OpenAPI、遗留引用扫描和三端全量验收。

## 12. 非本阶段范围

- 一条例句关联多个音频。
- 例句专属上传或播放 API。
- 自动生成、录制或剪辑例句音频。
- 单词听写、例句听写和文章听写。
- 用户端独立单词详情页面。
- 旧开发数据迁移或兼容层。
- 文章、试卷和视频模块的其他重构。
