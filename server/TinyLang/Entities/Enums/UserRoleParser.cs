namespace TinyLang.Entities.Enums;

/// <summary>
/// 提供用户角色名称的严格白名单解析。
/// </summary>
public static class UserRoleParser
{
    /// <summary>
    /// 仅接受与已定义角色名称完全一致的 ordinal 字符串。
    /// </summary>
    /// <param name="value">待解析的角色名称。</param>
    /// <param name="role">解析成功后的角色。</param>
    /// <returns>角色名称为 <c>User</c> 或 <c>Admin</c> 时返回 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out UserRole role)
    {
        if (string.Equals(value, nameof(UserRole.User), StringComparison.Ordinal))
        {
            role = UserRole.User;
            return true;
        }

        if (string.Equals(value, nameof(UserRole.Admin), StringComparison.Ordinal))
        {
            role = UserRole.Admin;
            return true;
        }

        role = default;
        return false;
    }
}
