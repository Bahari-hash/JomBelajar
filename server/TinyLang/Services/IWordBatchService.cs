using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 提供词条批量导入的只读校验能力。
/// </summary>
public interface IWordBatchService
{
    /// <summary>
    /// 校验批量词条内容并解析其共享音频名称。
    /// </summary>
    Task<BatchWordValidationResponse> ValidateAsync(
        BatchWordRequest request,
        CancellationToken cancellationToken);
}
