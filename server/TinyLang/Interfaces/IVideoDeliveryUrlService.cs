using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义对象存储直链和供应商无关 CDN 签名路径的媒体交付边界。
/// </summary>
public interface IVideoDeliveryUrlService
{
    /// <summary>
    /// 为对象生成永久公共直链或覆盖服务端保护前缀的短期 CDN 地址。
    /// </summary>
    /// <param name="objectName">需要交付的完整对象名称。</param>
    /// <param name="protectedPrefix">token 必须覆盖的规范化对象前缀。</param>
    /// <returns>访问地址；永久直链的失效时间为 null。</returns>
    VideoDeliveryUrl CreateUrl(string objectName, string protectedPrefix);
}
