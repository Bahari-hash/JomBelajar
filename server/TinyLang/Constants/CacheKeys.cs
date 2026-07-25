namespace TinyLang.Constants;

public static class CacheKeys
{
    public const string RedisInstanceName = "tiny-lang";

    public static string BuildRedisKey(string key) => RedisInstanceName + key;
}
