using TinyLang.Entities.Enums;
using TinyLang.Entities;
using TinyLang.Models;

namespace TinyLang.Services;

/// <summary>
/// 定义媒体上传预签名和上传确认的业务契约。
/// </summary>
public interface IMediaResourceService
{
    /// <summary>
    /// 校验上传元数据，创建待确认资源并生成对象存储上传地址。
    /// </summary>
    /// <param name="uploaderId">上传用户标识。</param>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="size">文件大小，单位为字节。</param>
    /// <param name="contentType">文件媒体类型。</param>
    /// <param name="module">资源所属业务模块。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>待确认资源标识、对象名称和预签名上传地址。</returns>
    Task<MediaResourcePresignResult> CreatePendingResourceAndPresignAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证对象已按申报元数据上传，将资源移动到最终位置并激活记录。
    /// </summary>
    /// <param name="resourceId">待确认媒体资源标识。</param>
    /// <param name="uploaderId">执行确认的上传用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>已激活的媒体资源。</returns>
    Task<MediaResource> ConfirmAsync(
        Guid resourceId,
        Guid uploaderId,
        CancellationToken cancellationToken = default);
}
