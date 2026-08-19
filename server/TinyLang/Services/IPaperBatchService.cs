using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IPaperBatchService
{
    Task<PaperBatchValidationResponse> ValidateAsync(
        PaperBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<PaperBatchImportResult> ImportAsync(
        Guid adminId,
        PaperBatchRequest request,
        CancellationToken cancellationToken = default);
}

