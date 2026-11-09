using WwTool.Core.Abstractions;

namespace WwTool.Common.Utils;

/// <summary>
/// 迁移期适配器：让 Core 通过 <see cref="ILocalizer"/> 使用旧 WPF 程序的 <see cref="LanguageManager"/>。
/// </summary>
public sealed class LanguageManagerLocalizer : ILocalizer
{
    public string this[string key] => LanguageManager.Instance[key];
}
