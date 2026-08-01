using System.ComponentModel;

namespace TinyLang.Exceptions;

/// <summary>
/// 定义验证和业务失败使用的稳定错误标识及本地化消息。
/// </summary>
public enum ErrorCodes
{
    // --** System Errors **--

    [Description("未知系统错误，请联系管理员或稍后再试")]
    UnexpectedError,

    [Description("请求负载参数校验错误")]
    RequestValidationFailed,

    // --** User Errors **--

    [Description("用户名不能为空.")]
    UsernameRequired,

    [Description("用户名长度最大不超过30个字符.")]
    UsernameLengthLimit,

    [Description("用户名只能由字母, 数字, 下划线, 分隔符构成.")]
    UsernameFormatInvalid,

    [Description("邮箱不能为空.")]
    EmailRequired,

    [Description("邮箱长度最大不超过100个字符.")]
    EmailLengthLimit,

    [Description("无效的邮箱格式.")]
    EmailFormatInvalid,

    [Description("密码不能为空.")]
    PasswordRequired,

    [Description("密码长度最大不超过50个字符.")]
    PasswordLengthLimit,

    [Description("密码长度最少为8个字符.")]
    PasswordLengthMinimum,

    [Description("密码只能由字母, 数字, 下划线, @#$等符号组成.")]
    PasswordFormatInvalid,

    [Description("验证码不能为空.")]
    VerificationCodeRequired,

    [Description("验证码长度只能是6个字符.")]
    VerificationCodeLengthLimit,

    [Description("验证码只能是数字.")]
    VerificationCodeFormatInvalid,

    [Description("验证码错误或者已经失效.")]
    VerificationCodeInvalid,

    [Description("邮箱已经被占用.")]
    EmailAlreadyExists,

    [Description("新邮箱不能与当前邮箱相同.")]
    EmailUnchanged,

    [Description("用户名已经被占用.")]
    UsernameAlreadyExists,

    [Description("用户不存在.")]
    UserNotFound,

    [Description("用户名或密码错误.")]
    UsernameOrPasswordWrong,

    [Description("邮箱或密码错误.")]
    InvalidCredentials,

    [Description("刷新令牌无效或已过期.")]
    RefreshTokenInvalid,

    [Description("用户权鉴无效.")]
    TokenInvalid,

    [Description("用户被封禁, 无法执行操作.")]
    UserAlreadyBanned,

    [Description("用户封禁原因不能为空.")]
    BanUserReasonRequired,

    [Description("用户封禁原因最大不能超过500个字符.")]
    BanUserReasonLengthLimit,

    [Description("用户昵称最大长度不能超过60个字符.")]
    NicknameLengthLimit,

    [Description("用户简介最大长度不超过500个字符.")]
    BioLengthLimit,

    [Description("头像链接最大长度不能超过500个字符.")]
    AvatarUrlLengthLimit,

    [Description("头像链接必须是有效的 HTTP 或 HTTPS 地址.")]
    AvatarUrlFormatInvalid,

    [Description("角色不能为空.")]
    RoleRequired,

    [Description("用户角色无效.")]
    RoleInvalid,

    [Description("管理员不能封禁自己.")]
    CannotBanSelf,

    [Description("管理员不能修改自己的角色.")]
    CannotChangeOwnRole,

    // --** Media Resource Errors **--

    [Description("媒体文件名不能为空.")]
    MediaOriginalNameRequired,

    [Description("媒体文件名长度最大不能超过255个字符.")]
    MediaOriginalNameLengthLimit,

    [Description("媒体文件名不能包含路径信息.")]
    MediaOriginalNameInvalid,

    [Description("媒体文件扩展名不能为空.")]
    MediaExtensionRequired,

    [Description("媒体文件扩展名不受支持.")]
    MediaExtensionInvalid,

    [Description("媒体文件扩展名与原始文件名不一致.")]
    MediaExtensionMismatch,

    [Description("媒体文件 Content-Type 不能为空.")]
    MediaContentTypeRequired,

    [Description("媒体文件 Content-Type 与扩展名或资源模块不匹配.")]
    MediaContentTypeInvalid,

    [Description("媒体文件大小必须大于0.")]
    MediaSizeInvalid,

    [Description("媒体文件大小超过当前类型的上传限制.")]
    MediaSizeLimitExceeded,

    [Description("媒体资源所属模块无效.")]
    ResourceModuleInvalid,

    [Description("媒体资源不存在.")]
    MediaResourceNotFound,

    [Description("无权操作其他用户上传的媒体资源.")]
    MediaResourceOwnershipMismatch,

    [Description("媒体资源当前状态不允许执行该操作.")]
    MediaResourceStatusConflict,

    [Description("媒体文件尚未上传完成.")]
    MediaResourceUploadIncomplete,

    [Description("媒体文件实际大小与申报大小不一致.")]
    MediaResourceSizeMismatch,

    [Description("媒体文件实际 Content-Type 与申报类型不一致.")]
    MediaResourceContentTypeMismatch,

    [Description("对象存储服务暂时不可用.")]
    ObjectStorageUnavailable,

    [Description("该媒体文件必须使用分片上传.")]
    MultipartUploadRequired,

    [Description("该媒体文件未达到分片上传阈值.")]
    MultipartUploadNotRequired,

    [Description("分片上传会话不存在.")]
    MultipartUploadNotFound,

    [Description("无权操作其他用户的分片上传会话.")]
    MultipartUploadOwnershipMismatch,

    [Description("分片上传会话已经过期.")]
    MultipartUploadExpired,

    [Description("分片上传的 part 列表无效.")]
    MultipartUploadPartsInvalid,

    [Description("媒体资源正在后台归档，请稍后重试.")]
    MediaResourceFinalizing,

    [Description("当前用户的未完成上传数量或容量已达到上限.")]
    MultipartUploadQuotaExceeded,

    // --** Video Errors **--

    [Description("视频不存在.")]
    VideoNotFound,

    [Description("视频标题不能为空.")]
    VideoTitleRequired,

    [Description("视频标题长度最大不能超过200个字符.")]
    VideoTitleLengthLimit,

    [Description("视频简介长度最大不能超过2000个字符.")]
    VideoDescriptionLengthLimit,

    [Description("视频原始语言不能为空或格式无效.")]
    VideoLanguageInvalid,

    [Description("视频源媒体资源无效.")]
    VideoSourceInvalid,

    [Description("视频源媒体资源尚未完成上传归档.")]
    VideoSourceNotActive,

    [Description("视频源媒体资源已被其他视频占用.")]
    VideoSourceAlreadyUsed,

    [Description("视频创建人筛选标识无效.")]
    VideoCreatedByInvalid,

    [Description("视频当前状态不允许执行该操作.")]
    VideoStatusConflict,

    [Description("视频已被其他管理员修改，请刷新后重试.")]
    VideoConcurrencyConflict,

    [Description("视频当前状态或处理任务不允许归档.")]
    VideoArchiveConflict,

    [Description("视频处理任务当前状态不允许重试.")]
    VideoRetryConflict,

    [Description("播放位置无效.")]
    VideoProgressInvalid,

    [Description("视频分类标识集合无效.")]
    VideoCategoryIdsInvalid,

    [Description("单个视频最多只能关联10个分类.")]
    VideoCategoryCountLimit,

    [Description("视频分类不能重复.")]
    VideoCategoryDuplicate,

    [Description("视频分类不存在.")]
    VideoCategoryNotFound,

    [Description("视频分类仍被视频使用，请先清空关联.")]
    VideoCategoryInUse,

    [Description("视频分类已停用.")]
    VideoCategoryInactive,

    [Description("视频分类名称不能为空.")]
    VideoCategoryNameRequired,

    [Description("视频分类名称长度最大不能超过100个字符.")]
    VideoCategoryNameLengthLimit,

    [Description("视频分类 Slug 不能为空.")]
    VideoCategorySlugRequired,

    [Description("视频分类 Slug 长度最大不能超过120个字符.")]
    VideoCategorySlugLengthLimit,

    [Description("视频分类 Slug 只能包含字母、数字和单个连字符.")]
    VideoCategorySlugFormatInvalid,

    [Description("视频分类描述长度最大不能超过500个字符.")]
    VideoCategoryDescriptionLengthLimit,

    [Description("视频分类名称已存在.")]
    VideoCategoryNameConflict,

    [Description("视频分类 Slug 已存在.")]
    VideoCategorySlugConflict,

    // --** Audio Errors **--

    [Description("音频不存在.")]
    AudioNotFound,

    [Description("音频标题不能为空.")]
    AudioTitleRequired,

    [Description("音频标题长度最大不能超过200个字符.")]
    AudioTitleLengthLimit,

    [Description("音频简介长度最大不能超过2000个字符.")]
    AudioDescriptionLengthLimit,

    [Description("音频语言标签不能为空或格式无效.")]
    AudioLanguageInvalid,

    [Description("音频用途无效.")]
    AudioKindInvalid,

    [Description("音频处理状态无效.")]
    AudioProcessingStatusInvalid,

    [Description("音频发布状态无效.")]
    AudioPublicationStatusInvalid,

    [Description("音频源媒体资源无效.")]
    AudioSourceInvalid,

    [Description("音频源媒体资源尚未完成上传归档.")]
    AudioSourceNotActive,

    [Description("音频源媒体资源已被其他音频占用.")]
    AudioSourceAlreadyUsed,

    [Description("音频当前状态不允许执行该操作.")]
    AudioStatusConflict,

    [Description("音频处理任务当前状态不允许重试.")]
    AudioRetryConflict,

    [Description("音频处理服务暂时不可用.")]
    AudioProcessingUnavailable,

    [Description("音频源媒体属性无效.")]
    AudioSourceMetadataInvalid,

    [Description("音频输出校验失败.")]
    AudioOutputValidationFailed,

    // --** Article Errors **--

    [Description("文章不存在.")]
    ArticleNotFound,

    [Description("文章标题不能为空.")]
    ArticleTitleRequired,

    [Description("文章标题长度最大不能超过200个字符.")]
    ArticleTitleLengthLimit,

    [Description("文章摘要长度最大不能超过500个字符.")]
    ArticleSummaryLengthLimit,

    [Description("文章正文不能为空.")]
    ArticleContentRequired,

    [Description("文章正文长度超过限制.")]
    ArticleContentLengthLimit,

    [Description("文章正文包含不安全或无效的内容.")]
    ArticleContentInvalid,

    [Description("文章当前状态不允许执行该操作.")]
    ArticleStatusConflict,

    [Description("文章状态无效.")]
    ArticleStatusInvalid,

    [Description("文章已被其他管理员修改，请刷新后重试.")]
    ArticleConcurrencyConflict,

    [Description("发布文章前必须选择分类.")]
    ArticleCategoryRequired,

    [Description("文章分类标识无效.")]
    ArticleCategoryInvalid,

    [Description("文章分类不存在.")]
    ArticleCategoryNotFound,

    [Description("文章分类已停用.")]
    ArticleCategoryInactive,

    [Description("单篇文章最多只能关联10个分类.")]
    ArticleCategoryCountLimit,

    [Description("文章分类不能重复.")]
    ArticleCategoryDuplicate,

    [Description("文章媒体资源无效.")]
    ArticleMediaInvalid,

    [Description("文章媒体资源尚未确认上传.")]
    ArticleMediaNotConfirmed,

    [Description("无权使用该文章媒体资源.")]
    ArticleMediaOwnershipMismatch,

    [Description("文章媒体声明与正文图片引用不一致.")]
    ArticleMediaNotReferenced,

    [Description("单篇文章最多只能关联100个媒体资源.")]
    ArticleMediaCountLimit,

    [Description("文章媒体资源不能重复.")]
    ArticleMediaDuplicate,

    // --** Article Category Errors **--

    [Description("文章分类名称不能为空.")]
    ArticleCategoryNameRequired,

    [Description("文章分类名称长度最大不能超过100个字符.")]
    ArticleCategoryNameLengthLimit,

    [Description("文章分类 Slug 不能为空.")]
    ArticleCategorySlugRequired,

    [Description("文章分类 Slug 长度最大不能超过120个字符.")]
    ArticleCategorySlugLengthLimit,

    [Description("文章分类 Slug 只能包含字母、数字和单个连字符.")]
    ArticleCategorySlugFormatInvalid,

    [Description("文章分类描述长度最大不能超过500个字符.")]
    ArticleCategoryDescriptionLengthLimit,

    [Description("文章分类名称已存在.")]
    ArticleCategoryNameConflict,

    [Description("文章分类 Slug 已存在.")]
    ArticleCategorySlugConflict,

    [Description("文章分类仍被文章使用，请先清空分类中的文章.")]
    ArticleCategoryInUse,

    // --** Word Errors **--

    [Description("词条不存在.")]
    WordNotFound,

    [Description("词头不能为空.")]
    WordHeadwordRequired,

    [Description("词头长度最大不能超过200个字符.")]
    WordHeadwordLengthLimit,

    [Description("词条语言标签不能为空或格式无效.")]
    WordLanguageInvalid,

    [Description("词性无效.")]
    WordPartOfSpeechInvalid,

    [Description("释义不能为空.")]
    WordDefinitionRequired,

    [Description("释义长度最大不能超过2000个字符.")]
    WordDefinitionLengthLimit,

    [Description("用法说明长度最大不能超过1000个字符.")]
    WordUsageNoteLengthLimit,

    [Description("例句不能为空.")]
    WordSentenceRequired,

    [Description("例句长度最大不能超过2000个字符.")]
    WordSentenceLengthLimit,

    [Description("例句翻译不能为空.")]
    WordTranslationRequired,

    [Description("例句翻译长度最大不能超过2000个字符.")]
    WordTranslationLengthLimit,

    [Description("发音口音标签长度最大不能超过100个字符.")]
    WordAccentTagLengthLimit,

    [Description("发音 IPA 长度最大不能超过200个字符.")]
    WordIpaLengthLimit,

    [Description("词条子项集合无效.")]
    WordChildCollectionInvalid,

    [Description("词条子项数量超过限制.")]
    WordChildCountLimit,

    [Description("词条子项标识无效.")]
    WordChildIdInvalid,

    [Description("词条子项标识重复或不属于当前聚合.")]
    WordChildIdConflict,

    [Description("词条子项排序值无效.")]
    WordSortOrderInvalid,

    [Description("同级词条子项排序值不能重复.")]
    WordSortOrderConflict,

    [Description("词条发音音频标识无效.")]
    WordPronunciationAudioInvalid,

    [Description("同一词条不能重复使用相同的发音音频.")]
    WordPronunciationAudioDuplicate,

    [Description("词条最多只能包含一个默认发音，发布时必须恰好包含一个.")]
    WordDefaultPronunciationConflict,

    [Description("词条发布状态无效.")]
    WordStatusInvalid,

    [Description("相同语言的词条已存在.")]
    WordDuplicate,

    [Description("词条关联的音频不存在.")]
    WordAudioNotFound,

    [Description("词条关联的音频当前不可用.")]
    WordAudioUnavailable,

    [Description("词条关联的音频用途不匹配.")]
    WordAudioKindMismatch,

    [Description("词条关联的音频语言不兼容.")]
    WordAudioLanguageMismatch,

    [Description("词条内容不满足发布要求.")]
    WordPublishRequirementsNotMet,

    [Description("词条当前状态不允许执行该操作.")]
    WordStatusConflict,

    [Description("词条已被其他管理员修改，请刷新后重试.")]
    WordConcurrencyConflict,

    [Description("词条当前状态不允许归档.")]
    WordArchiveConflict,

    [Description("已发布词条必须先下架才能删除.")]
    WordPublishedDeleteConflict,

    [Description("批量词条行数必须介于1到100之间.")]
    WordBatchRowCountInvalid,

    [Description("批量词条子项总数超过限制.")]
    WordBatchChildCountLimit,

    [Description("批量词条文本总量超过限制.")]
    WordBatchTextLengthLimit,

    [Description("批量词条校验失败.")]
    WordBatchValidationFailed,

    // --** Word Study Errors **--

    [Description("本轮背诵单词数量必须介于1到100之间.")]
    WordStudyWordCountInvalid,

    [Description("背诵会话抽词模式无效.")]
    WordStudySelectionModeInvalid,

    [Description("背诵结果无效.")]
    WordStudyResultInvalid,

    [Description("背诵会话不存在.")]
    WordStudySessionNotFound,

    [Description("当前已有进行中的背诵会话.")]
    WordStudyActiveSessionExists,

    [Description("当前没有符合条件的可背诵词条.")]
    WordStudyNoEligibleWords,

    [Description("背诵会话当前状态不允许执行该操作.")]
    WordStudySessionNotActive,

    [Description("背诵会话项不存在.")]
    WordStudyItemNotFound,

    [Description("该会话项已经提交了不同的背诵结果.")]
    WordStudyItemResultConflict,

    [Description("背诵会话已被其他请求修改，请刷新后重试.")]
    WordStudyConcurrencyConflict,

    [Description("词条已有用户学习历史，不能删除.")]
    WordHasStudyHistory,

    // --** Online Quiz Errors **--

    [Description("试卷不存在.")]
    PaperNotFound,

    [Description("试卷标题不能为空.")]
    PaperTitleRequired,

    [Description("试卷标题长度最大不能超过200个字符.")]
    PaperTitleLengthLimit,

    [Description("试卷描述长度最大不能超过2000个字符.")]
    PaperDescriptionLengthLimit,

    [Description("试卷说明长度最大不能超过5000个字符.")]
    PaperInstructionsLengthLimit,

    [Description("试卷语言标签不能为空或格式无效.")]
    PaperLanguageInvalid,

    [Description("试卷发布状态无效.")]
    PaperStatusInvalid,

    [Description("试卷题目集合无效.")]
    PaperQuestionCollectionInvalid,

    [Description("试卷题目或答案子项数量超过限制.")]
    PaperChildCountLimit,

    [Description("试卷题目或答案子项标识无效.")]
    PaperChildIdInvalid,

    [Description("试卷题目或答案子项标识重复或不属于当前聚合.")]
    PaperChildIdConflict,

    [Description("试卷题型无效.")]
    PaperQuestionTypeInvalid,

    [Description("试卷题型与标准答案结构不匹配.")]
    PaperQuestionShapeInvalid,

    [Description("试卷题干不能为空.")]
    PaperQuestionPromptRequired,

    [Description("试卷题干长度最大不能超过5000个字符.")]
    PaperQuestionPromptLengthLimit,

    [Description("试卷答案解析长度最大不能超过5000个字符.")]
    PaperExplanationLengthLimit,

    [Description("试卷题目分值必须介于1到100之间.")]
    PaperPointsInvalid,

    [Description("试卷及格分必须介于0和总分之间.")]
    PaperPassingScoreInvalid,

    [Description("试卷子项排序值无效.")]
    PaperSortOrderInvalid,

    [Description("同级试卷子项排序值不能重复.")]
    PaperSortOrderConflict,

    [Description("单选题选项不能为空.")]
    PaperOptionTextRequired,

    [Description("单选题选项长度最大不能超过2000个字符.")]
    PaperOptionTextLengthLimit,

    [Description("填空题可接受答案不能为空.")]
    PaperAcceptedAnswerRequired,

    [Description("填空题答案长度最大不能超过1000个字符.")]
    PaperAnswerTextLengthLimit,

    [Description("填空题可接受答案规范化后不能重复.")]
    PaperAcceptedAnswerDuplicate,

    [Description("试卷内容不满足发布要求.")]
    PaperPublishRequirementsNotMet,

    [Description("试卷当前状态不允许执行该操作.")]
    PaperStatusConflict,

    [Description("试卷已有用户测验记录，内容已永久锁定.")]
    PaperContentLocked,

    [Description("已发布试卷必须先下架才能删除.")]
    PaperPublishedDeleteConflict,

    [Description("试卷已被其他请求修改，请刷新后重试.")]
    PaperConcurrencyConflict,

    [Description("测验记录不存在.")]
    PaperAttemptNotFound,

    [Description("测验当前状态不允许继续保存答案.")]
    PaperAttemptNotInProgress,

    [Description("测验尚未提交，不能查看结果.")]
    PaperAttemptNotSubmitted,

    [Description("题目不属于当前测验试卷.")]
    PaperAttemptQuestionNotFound,

    [Description("提交的题目答案结构与题型不匹配.")]
    PaperAttemptAnswerShapeInvalid,

    [Description("所选选项不属于当前题目.")]
    PaperAttemptSelectedOptionInvalid,

    [Description("当前已有进行中的试卷测验.")]
    PaperAttemptActiveConflict,

    [Description("测验已被其他请求修改，请刷新后重试.")]
    PaperAttemptConcurrencyConflict,

    // --** Pagination Errors **--

    [Description("页码必须大于或等于1.")]
    PageInvalid,

    [Description("每页数量必须在1到100之间.")]
    PageSizeInvalid,

    [Description("查询关键词长度最大不能超过200个字符.")]
    KeywordLengthLimit,

    [Description("查询关键词包含无效字符.")]
    KeywordInvalid,
}
