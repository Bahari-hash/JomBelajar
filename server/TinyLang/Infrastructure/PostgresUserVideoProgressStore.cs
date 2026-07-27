using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 PostgreSQL ON CONFLICT 原子插入或更新用户视频进度。
/// </summary>
public sealed class PostgresUserVideoProgressStore : IUserVideoProgressStore
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 使用具体 PostgreSQL EF Core 上下文创建进度存储。
    /// </summary>
    public PostgresUserVideoProgressStore(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<UserVideoProgress> UpsertAsync(
        Guid userId,
        Guid videoId,
        double positionSeconds,
        bool isCompleted,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_video_progress
                ("Id", "UserId", "VideoId", "PositionSeconds", "IsCompleted",
                 "LastPlayedAt", "CreatedAt", "UpdatedAt")
            VALUES
                ({id}, {userId}, {videoId}, {positionSeconds}, {isCompleted},
                 {now}, {now}, {now})
            ON CONFLICT ("UserId", "VideoId") DO UPDATE SET
                "PositionSeconds" = EXCLUDED."PositionSeconds",
                "IsCompleted" = user_video_progress."IsCompleted" OR EXCLUDED."IsCompleted",
                "LastPlayedAt" = EXCLUDED."LastPlayedAt",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
        return await _db.UserVideoProgress.AsNoTracking()
            .SingleAsync(
                value => value.UserId == userId && value.VideoId == videoId,
                cancellationToken);
    }
}
