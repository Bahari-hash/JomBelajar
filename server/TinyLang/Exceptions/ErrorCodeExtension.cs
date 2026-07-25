using System.ComponentModel;
using System.Reflection;

namespace TinyLang.Exceptions;

/// <summary>
/// 提供从错误码读取 <see cref="DescriptionAttribute"/> 消息的扩展方法。
/// </summary>
public static class ErrorCodeExtension
{
    /// <summary>
    /// 获取错误码的描述消息；缺少描述时回退为枚举名称。
    /// </summary>
    /// <param name="error">业务错误码。</param>
    /// <returns>面向客户端的错误消息。</returns>
    public static string GetMessage(this ErrorCodes error)
    {
        var field = typeof(ErrorCodes).GetField(error.ToString());
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? error.ToString();
    }
}
