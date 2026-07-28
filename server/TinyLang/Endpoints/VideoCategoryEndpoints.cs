using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义登录用户和管理员视频分类管理 endpoints。
/// </summary>
public static class VideoCategoryEndpoints
{
    /// <summary>
    /// 注册用户分类目录和管理员分类维护路由。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapVideoCategoriesApi(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet("/video-categories", GetPublicListAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        var admin = endpoints.MapGroup("/admin/video-categories")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        admin.MapGet("", GetAdminListAsync);
        admin.MapPost("", CreateAsync);
        admin.MapPut("/{id:guid}", UpdateAsync);
        admin.MapDelete("/{id:guid}", DeleteAsync);
        return endpoints;
    }

    /// <summary>
    /// 返回登录用户可见的启用视频分类分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<VideoCategoryResponse>>> GetPublicListAsync(
        [AsParameters] VideoCategoryListRequest request,
        IVideoCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetPublicListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 返回管理员可见的视频分类分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<VideoCategoryResponse>>> GetAdminListAsync(
        [AsParameters] AdminVideoCategoryListRequest request,
        IVideoCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.GetAdminListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 创建启用的视频分类并返回其资源位置。
    /// </summary>
    public static async Task<Created<VideoCategoryResponse>> CreateAsync(
        CreateVideoCategoryRequest request,
        IVideoCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var response = await categoryService.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/admin/video-categories/{response.Id}", response);
    }

    /// <summary>
    /// 更新视频分类字段和启用状态。
    /// </summary>
    public static async Task<Ok<VideoCategoryResponse>> UpdateAsync(
        Guid id,
        UpdateVideoCategoryRequest request,
        IVideoCategoryService categoryService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await categoryService.UpdateAsync(
            id,
            request,
            cancellationToken));

    /// <summary>
    /// 删除视频分类并返回无响应体成功结果。
    /// </summary>
    public static async Task<NoContent> DeleteAsync(
        Guid id,
        IVideoCategoryService categoryService,
        CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
