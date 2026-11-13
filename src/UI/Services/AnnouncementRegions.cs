using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Services;

namespace WwTool.UI.Services;

/// <summary>
/// 公告区服（首页游戏公告卡与活动页共用同一个选择）：设置里记住了就用记住的；
/// 否则按当前账号（UID 小于 2 亿为国服），没有账号时简体中文界面用国服、其他用国际服。
/// </summary>
public static class AnnouncementRegions
{
    public static LauncherRegion Resolve(IConfigService config, AccountContext accounts, LocalizationService localization)
    {
        string? saved = config.User.AnnouncementRegion;
        if (saved == "CN") return LauncherRegion.China;
        if (saved == "Global") return LauncherRegion.Global;
        if (accounts.Current?.Uid is { } uid && long.TryParse(uid, out long id))
            return id < 200_000_000 ? LauncherRegion.China : LauncherRegion.Global;
        return localization.CurrentLanguage == LanguageType.ZhHans ? LauncherRegion.China : LauncherRegion.Global;
    }

    /// <summary>记住选择；返回区服是否有变化。</summary>
    public static bool Save(IConfigService config, AccountContext accounts, LocalizationService localization, LauncherRegion region)
    {
        LauncherRegion before = Resolve(config, accounts, localization);
        config.User.AnnouncementRegion = region == LauncherRegion.China ? "CN" : "Global";
        return before != region;
    }
}
