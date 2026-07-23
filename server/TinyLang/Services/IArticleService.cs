using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IArticleService
{
    Task<ArticleResponse> CreateDraftAsync(
        Guid editorId,
        CreateArticleRequest request,
        CancellationToken cancellationToken = default);

    Task<ArticleResponse> UpdateAsync(
        Guid articleId,
        Guid editorId,
        UpdateArticleRequest request,
        CancellationToken cancellationToken = default);

    Task<ArticleResponse> PublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    Task<ArticleResponse> UnpublishAsync(
        Guid articleId,
        Guid editorId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid articleId, Guid editorId, CancellationToken cancellationToken = default);

    Task<ArticleResponse> GetEditorByIdAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<PagedResponse<ArticleListItemResponse>> GetEditorListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default);

    Task<ArticleResponse> GetPublicByIdAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<PagedResponse<ArticleListItemResponse>> GetPublicListAsync(
        ArticleListRequest request,
        CancellationToken cancellationToken = default);
}
