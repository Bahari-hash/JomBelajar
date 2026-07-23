using System.ComponentModel;

namespace TinyLang.Exceptions;

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

    [Description("文章正文包含不安全或无效的 HTML 内容.")]
    ArticleContentInvalid,

    [Description("文章当前状态不允许执行该操作.")]
    ArticleStatusConflict,

    [Description("文章状态无效.")]
    ArticleStatusInvalid,

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
