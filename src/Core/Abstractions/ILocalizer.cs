namespace WwTool.Core.Abstractions;

/// <summary>
/// 界面文案查询。Core 中需要生成面向用户的文字时通过它取语言资源，不直接依赖界面层的本地化实现。
/// </summary>
public interface ILocalizer
{
    /// <summary>按键取当前语言的文案；键不存在时返回键本身。</summary>
    string this[string key] { get; }
}
