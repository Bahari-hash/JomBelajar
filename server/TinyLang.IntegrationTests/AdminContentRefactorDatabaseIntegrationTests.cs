using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TinyLang.Database;
using TinyLang.Entities.Enums;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 使用真实 PostgreSQL 验证 Admin 内容重构的数据迁移、约束和回滚 schema。
/// </summary>
public sealed class AdminContentRefactorDatabaseIntegrationTests
{
    private const string PreviousMigration =
        "20260730065819_UpgradeArticleMarkdownContract";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 从上一版本升级后验证角色、审计字段、字幕退役和分类 Restrict，再回滚 schema。
    /// </summary>
    [Fact]
    public async Task MigrationShouldPreserveContentAndApplyIrreversibleDataCleanup()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL Admin content migration tests.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var schema = $"tiny_lang_admin_refactor_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync(cancellationToken);
        await ExecuteSchemaCommandAsync(
            adminConnection,
            $"CREATE SCHEMA \"{schema}\"",
            cancellationToken);

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(schemaConnection)
                .Options;
            LegacyIds ids;
            await using (var db = new ApplicationDbContext(options))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration, cancellationToken);
                ids = await SeedPreviousSchemaAsync(db, cancellationToken);
                await migrator.MigrateAsync(cancellationToken: cancellationToken);
            }

            await VerifyUpAsync(options, schemaConnection, ids, cancellationToken);

            await using (var db = new ApplicationDbContext(options))
            {
                await db.GetService<IMigrator>()
                    .MigrateAsync(PreviousMigration, cancellationToken);
            }

            await VerifyDownSchemaAsync(schemaConnection, ids, cancellationToken);
        }
        finally
        {
            await ExecuteSchemaCommandAsync(
                adminConnection,
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                cancellationToken);
        }
    }

    /// <summary>
    /// 在上一版本 schema 中写入 Editor/Admin、Owner 数据、分类关联和字幕残留。
    /// </summary>
    private static async Task<LegacyIds> SeedPreviousSchemaAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var ids = new LegacyIds(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "users"
                ("Id", "Username", "Email", "PasswordHash", "Role", "IsBanned",
                 "IsDeleted", "TokenVersion", "CreatedAt", "UpdatedAt")
            VALUES
                ({ids.EditorId}, {"legacy-editor"}, {"legacy-editor@example.test"},
                 {"hash"}, {"Editor"}, {false}, {false}, {7}, {Now}, {Now}),
                ({ids.AdminId}, {"legacy-admin"}, {"legacy-admin@example.test"},
                 {"hash"}, {"Admin"}, {false}, {false}, {3}, {Now}, {Now});
            """, cancellationToken);

        await InsertMediaResourceAsync(
            db,
            ids.VideoSourceId,
            ids.EditorId,
            "CourseVideo",
            "video.mp4",
            "video/mp4",
            cancellationToken);
        await InsertMediaResourceAsync(
            db,
            ids.AudioSourceId,
            ids.AdminId,
            "Audio",
            "audio.mp3",
            "audio/mpeg",
            cancellationToken);
        await InsertMediaResourceAsync(
            db,
            ids.SubtitleResourceId,
            ids.EditorId,
            "VideoSubtitle",
            "subtitle.vtt",
            "text/vtt",
            cancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "videos"
                ("Id", "OwnerId", "SourceMediaResourceId", "Title", "OriginalLanguage",
                 "ProcessingStatus", "PublicationStatus", "ConcurrencyStamp",
                 "CreatedAt", "UpdatedAt")
            VALUES
                ({ids.VideoId}, {ids.EditorId}, {ids.VideoSourceId}, {"Legacy video"},
                 {"en"}, {"Ready"}, {"Draft"}, {Guid.NewGuid()}, {Now}, {Now});

            INSERT INTO "audio_clips"
                ("Id", "OwnerId", "SourceMediaResourceId", "Title", "LanguageTag",
                 "Kind", "ProcessingStatus", "PublicationStatus", "ConcurrencyStamp",
                 "CreatedAt", "UpdatedAt")
            VALUES
                ({ids.AudioId}, {ids.AdminId}, {ids.AudioSourceId}, {"Legacy audio"},
                 {"en"}, {"Other"}, {"Ready"}, {"Draft"}, {Guid.NewGuid()}, {Now}, {Now});

            INSERT INTO "video_subtitles"
                ("Id", "VideoId", "MediaResourceId", "LanguageTag", "DisplayName",
                 "IsDefault", "SortOrder", "CreatedAt", "UpdatedAt")
            VALUES
                ({Guid.NewGuid()}, {ids.VideoId}, {ids.SubtitleResourceId}, {"en"},
                 {"English"}, {true}, {0}, {Now}, {Now});

            INSERT INTO "multipart_upload_sessions"
                ("Id", "MediaResourceId", "UploaderId", "ProviderUploadId", "Status",
                 "PartSize", "PartCount", "ExpiresAt", "AttemptCount",
                 "StagingCleanupRequired", "ConcurrencyStamp", "CreatedAt", "UpdatedAt")
            VALUES
                ({Guid.NewGuid()}, {ids.SubtitleResourceId}, {ids.EditorId}, {"provider-id"},
                 {"Pending"}, {5242880L}, {1}, {Now.AddHours(1)}, {0}, {false},
                 {Guid.NewGuid()}, {Now}, {Now});

            INSERT INTO "video_categories"
                ("Id", "Name", "Slug", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES
                ({ids.CategoryId}, {"Legacy category"}, {"legacy-category"},
                 {true}, {Now}, {Now});

            INSERT INTO "video_category_assignments" ("VideoId", "VideoCategoryId")
            VALUES ({ids.VideoId}, {ids.CategoryId});
            """, cancellationToken);
        return ids;
    }

    /// <summary>
    /// 写入一个满足上一版本约束的媒体资源。
    /// </summary>
    private static Task<int> InsertMediaResourceAsync(
        ApplicationDbContext db,
        Guid resourceId,
        Guid uploaderId,
        string module,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "media_resources"
                ("Id", "UploaderId", "ObjectName", "OriginalName", "Module", "Status",
                 "Size", "Extension", "ContentType", "ConcurrencyStamp",
                 "CreatedAt", "UpdatedAt")
            VALUES
                ({resourceId}, {uploaderId}, {$"legacy/{resourceId:N}/{fileName}"},
                 {fileName}, {module}, {"Active"}, {1024L}, {Path.GetExtension(fileName)},
                 {contentType}, {Guid.NewGuid()}, {Now}, {Now});
            """, cancellationToken);

    /// <summary>
    /// 验证 Up 保留 Owner 数据、回填审计、清理字幕并启用 Restrict 和 Archived。
    /// </summary>
    private static async Task VerifyUpAsync(
        DbContextOptions<ApplicationDbContext> options,
        string connectionString,
        LegacyIds ids,
        CancellationToken cancellationToken)
    {
        await using var db = new ApplicationDbContext(options);
        var editor = await db.Users.AsNoTracking()
            .SingleAsync(value => value.Id == ids.EditorId, cancellationToken);
        var admin = await db.Users.AsNoTracking()
            .SingleAsync(value => value.Id == ids.AdminId, cancellationToken);
        var video = await db.Videos
            .SingleAsync(value => value.Id == ids.VideoId, cancellationToken);
        var audio = await db.AudioClips.AsNoTracking()
            .SingleAsync(value => value.Id == ids.AudioId, cancellationToken);

        editor.Role.Should().Be(UserRole.User);
        editor.TokenVersion.Should().Be(8);
        admin.Role.Should().Be(UserRole.Admin);
        admin.TokenVersion.Should().Be(3);
        video.CreatedById.Should().Be(ids.EditorId);
        video.LastEditorId.Should().Be(ids.EditorId);
        audio.CreatedById.Should().Be(ids.AdminId);
        audio.LastEditorId.Should().Be(ids.AdminId);
        video.ArchivedAt.Should().BeNull();

        video.PublicationStatus = VideoPublicationStatus.Archived;
        video.ArchivedAt = Now;
        video.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);

        var deleteCategory = async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"video_categories\" WHERE \"Id\" = {ids.CategoryId}",
            cancellationToken);
        await deleteCategory.Should().ThrowAsync<PostgresException>()
            .Where(exception => exception.SqlState == PostgresErrorCodes.ForeignKeyViolation);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        (await RelationExistsAsync(connection, "video_subtitles", cancellationToken))
            .Should().BeFalse();
        (await CountAsync(
            connection,
            "SELECT COUNT(*) FROM \"media_resources\" WHERE \"Module\" = 'VideoSubtitle'",
            cancellationToken)).Should().Be(0);
        (await CountAsync(
            connection,
            "SELECT COUNT(*) FROM \"multipart_upload_sessions\" WHERE \"MediaResourceId\" = @id",
            cancellationToken,
            ids.SubtitleResourceId)).Should().Be(0);
    }

    /// <summary>
    /// 验证 Down 恢复旧列、级联约束和空字幕表，但不恢复不可逆业务数据。
    /// </summary>
    private static async Task VerifyDownSchemaAsync(
        string connectionString,
        LegacyIds ids,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        (await ColumnExistsAsync(connection, "videos", "OwnerId", cancellationToken))
            .Should().BeTrue();
        (await ColumnExistsAsync(connection, "videos", "CreatedById", cancellationToken))
            .Should().BeFalse();
        (await ColumnExistsAsync(connection, "videos", "LastEditorId", cancellationToken))
            .Should().BeFalse();
        (await ColumnExistsAsync(connection, "videos", "ArchivedAt", cancellationToken))
            .Should().BeFalse();
        (await RelationExistsAsync(connection, "video_subtitles", cancellationToken))
            .Should().BeTrue();
        (await CountAsync(
            connection,
            "SELECT COUNT(*) FROM \"video_subtitles\"",
            cancellationToken)).Should().Be(0);
        (await ScalarAsync<Guid>(
            connection,
            "SELECT \"OwnerId\" FROM \"videos\" WHERE \"Id\" = @id",
            cancellationToken,
            ids.VideoId)).Should().Be(ids.EditorId);
        (await ScalarAsync<string>(
            connection,
            "SELECT \"Role\" FROM \"users\" WHERE \"Id\" = @id",
            cancellationToken,
            ids.EditorId)).Should().Be("User");
        (await ScalarAsync<int>(
            connection,
            "SELECT \"TokenVersion\" FROM \"users\" WHERE \"Id\" = @id",
            cancellationToken,
            ids.EditorId)).Should().Be(8);
    }

    /// <summary>
    /// 查询当前 schema 是否包含指定表。
    /// </summary>
    private static async Task<bool> RelationExistsAsync(
        NpgsqlConnection connection,
        string relationName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass(@name) IS NOT NULL",
            connection);
        command.Parameters.AddWithValue("name", relationName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// 查询当前 schema 的指定表是否包含给定列。
    /// </summary>
    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = @table
                  AND column_name = @column
            )
            """,
            connection);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// 执行返回行数的 SQL，可选按 id 参数筛选。
    /// </summary>
    private static Task<long> CountAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        Guid? id = null)
        => ScalarAsync<long>(connection, commandText, cancellationToken, id);

    /// <summary>
    /// 执行返回单个非空值的 SQL，可选按 id 参数筛选。
    /// </summary>
    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        Guid? id = null)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        if (id.HasValue)
        {
            command.Parameters.AddWithValue("id", id.Value);
        }
        return (T)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// 执行隔离 schema 的创建或删除命令。
    /// </summary>
    private static async Task ExecuteSchemaCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 保存上一 schema 中用于升级和回滚断言的稳定标识。
    /// </summary>
    private sealed record LegacyIds(
        Guid EditorId,
        Guid AdminId,
        Guid VideoSourceId,
        Guid AudioSourceId,
        Guid SubtitleResourceId,
        Guid VideoId,
        Guid AudioId,
        Guid CategoryId);
}
