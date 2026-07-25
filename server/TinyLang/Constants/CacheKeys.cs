namespace TinyLang.Constants;

/// <summary>
/// 定义 Redis 键的命名空间及构建规则。
/// </summary>
public static class CacheKeys
{
    public const string RedisInstanceName = "tiny-lang";

    /// <summary>
    /// 为业务键添加 TinyLang 实例前缀。
    /// </summary>
    /// <param name="key">未包含实例前缀的业务键。</param>
    /// <returns>可写入 Redis 的完整键。</returns>
    public static string BuildRedisKey(string key) => RedisInstanceName + key;
}
