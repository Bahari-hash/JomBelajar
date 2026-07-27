using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义开发直连和供应商无关 CDN 签名路径的视频交付边界。
/// </summary>
public interface IVideoDeliveryUrlService
{
    /// <summary>
    /// 为受保护对象生成短期外部地址，签名覆盖其服务端定义的保护前缀。
    /// </summary>
    /// <param name="objectName">需要交付的完整对象名称。</param>
    /// <param name="protectedPrefix">token 必须覆盖的规范化对象前缀。</param>
    /// <returns>临时访问地址及其失效时间。</returns>
    VideoDeliveryUrl CreateUrl(string objectName, string protectedPrefix);
}
