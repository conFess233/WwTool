using System.ComponentModel;
using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Enums;
using WwTool.Core.Extensions;

namespace WwTool.Core.Localization;

/// <summary>
/// 可注入的本地化服务，取代旧版静态 <c>LanguageManager.Instance</c>。
/// <para>
/// 界面通过索引器绑定 <c>[Key]</c>；切换语言时发出 <c>Item[]</c> 变更通知，所有索引器绑定一次性刷新，无需重启。
/// 语言文件位于 <c>&lt;AppDirectory&gt;/Local/Language/&lt;code&gt;.json</c>，为扁平的“键 → 文案”对象。
/// </para>
/// </summary>
public sealed class LocalizationService : ILocalizer, INotifyPropertyChanged
{
    /// <summary>WPF 与 Avalonia 都把此名称识别为“所有索引器值已变化”。</summary>
    public const string IndexerName = "Item[]";

    private readonly string _languageDirectory;
    private readonly ILoggerService? _logger;
    private IReadOnlyDictionary<string, string> _strings = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> _fallback = new Dictionary<string, string>();

    public LocalizationService(IAppPaths paths, ILoggerService? logger = null)
    {
        _languageDirectory = Path.Combine(paths.BundledResourceDirectory, "Language");
        _logger = logger;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前语言。</summary>
    public LanguageType CurrentLanguage { get; private set; } = LanguageType.ZhHans;

    /// <summary>
    /// 取文案：当前语言 → 简体中文 → 键本身。返回键本身便于在界面上一眼发现漏翻的键。
    /// </summary>
    public string this[string key] =>
        _strings.TryGetValue(key, out string? value) || _fallback.TryGetValue(key, out value) ? value : key;

    /// <summary>
    /// 切换语言并通知所有绑定刷新。语言文件缺失或损坏时保留简体中文回退并记录日志，不抛出。
    /// 应在 UI 线程调用，以便绑定在同一线程收到通知。
    /// </summary>
    public void ChangeLanguage(LanguageType language)
    {
        if (_fallback.Count == 0)
        {
            _fallback = Load(LanguageType.ZhHans);
        }

        _strings = language == LanguageType.ZhHans ? _fallback : Load(language);
        CurrentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private IReadOnlyDictionary<string, string> Load(LanguageType language)
    {
        string path = Path.Combine(_languageDirectory, language.GetCode() + ".json");
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                ?? new Dictionary<string, string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger?.Warn($"语言文件不可读，回退到简体中文：{Path.GetFileName(path)}", ex);
            return new Dictionary<string, string>();
        }
    }
}
