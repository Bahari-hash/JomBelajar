using System.Text.Json;
using System.Text.Json.Serialization;
using TinyLang.Entities.Enums;

namespace TinyLang.Infrastructure;

/// <summary>
/// 将用户角色限制为精确的字符串白名单 JSON 契约。
/// </summary>
public sealed class UserRoleJsonConverter : JsonConverter<UserRole>
{
    /// <inheritdoc />
    public override UserRole Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            UserRoleParser.TryParse(reader.GetString(), out var role))
        {
            return role;
        }

        throw new JsonException("The user role must be an exact supported role name.");
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        UserRole value,
        JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value))
        {
            throw new JsonException("The user role is not defined.");
        }

        writer.WriteStringValue(value.ToString());
    }
}
