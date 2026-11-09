using System.ComponentModel;
using System.Reflection;

namespace WwTool.Core.Extensions;

/// <summary>
/// 与界面语言无关的枚举扩展。多语言描述由界面层的本地化服务提供。
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 读取 <see cref="DescriptionAttribute"/>；没有时返回枚举名。
    /// 返回的是固定中文描述，只用于存储与日志，不得直接作为界面文案。
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);
        FieldInfo? field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }
}
