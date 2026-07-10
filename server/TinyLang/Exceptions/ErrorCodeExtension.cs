using System.ComponentModel;
using System.Reflection;

namespace TinyLang.Exceptions;

public static class ErrorCodeExtension
{
    public static string GetMessage(this ErrorCodes error)
    {
        var field = typeof(ErrorCodes).GetField(error.ToString());
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? error.ToString();
    }
}
