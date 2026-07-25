namespace TinyLang.Interfaces;

/// <summary>
/// 定义敏感明文的自适应散列及校验契约。
/// </summary>
public interface ISecretHasher
{
    /// <summary>
    /// 为敏感明文生成带盐散列。
    /// </summary>
    /// <param name="secretPlain">待保护的明文。</param>
    /// <returns>可持久化的散列值。</returns>
    string Hash(string secretPlain);

    /// <summary>
    /// 验证明文是否与已存储散列匹配。
    /// </summary>
    /// <param name="secretPlain">待验证的明文。</param>
    /// <param name="secretHash">已存储的散列值。</param>
    /// <returns>匹配时返回 <see langword="true"/>。</returns>
    bool Verify(string secretPlain, string secretHash);
}
