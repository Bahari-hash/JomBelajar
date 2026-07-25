namespace TinyLang.UnitTests;

/// <summary>
/// 为依赖 UTC 时间的上传测试提供可控时钟。
/// </summary>
internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// 将测试时钟推进指定时长。
    /// </summary>
    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
