using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义对象存储媒体公共直链的交付边界。
/// </summary>
public interface IVideoDeliveryUrlService
{
    /// <summary>
    /// 为对象生成永久对象存储公共直链。
    /// </summary>
    /// <param name="objectName">需要交付的完整对象名称。</param>
    /// <param name="protectedPrefix">token 必须覆盖的规范化对象前缀。</param>
    /// <returns>访问地址；永久直链的失效时间为 null。</returns>
    VideoDeliveryUrl CreateUrl(string objectName, string protectedPrefix);
}
