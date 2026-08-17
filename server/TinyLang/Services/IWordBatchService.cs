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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 校验并在单个事务中创建批量词条。
    /// </summary>
    Task<WordBatchImportResult> ImportAsync(
        Guid adminId,
        BatchWordRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 返回批量导入成功结果或完整的重新校验结果。
/// </summary>
public sealed record WordBatchImportResult(
    BatchWordImportResponse? Imported,
    BatchWordValidationResponse? Validation);
