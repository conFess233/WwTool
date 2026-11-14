using System.Globalization;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Services.Challenges;
using WwTool.UI.Feedback;
using WwTool.UI.Pages.Pulls;
using WwTool.UI.Services;

namespace WwTool.UI.Pages.Roles;

/// <summary>打开角色详情弹窗：补上本地抽卡记录里该角色的抽取，再交给对话框服务显示。</summary>
public sealed class CharacterDetails(
    EncoreService encore,
    EncoreImages images,
    PullsSession pulls,
    IConfigService config,
    LocalizationService localization,
    IAppLauncher launcher,
    IDialogService dialogs)
{
    public async Task ShowAsync(CharacterDetailRequest request)
    {
        try
        {
            await pulls.EnsureLoadedAsync();
        }
        catch (Exception)
        {
            // 抽卡记录读不出来时照样显示资料，只是没有「我的抽取」。
        }
        string language = config.User.AppLanguage.GetCode();
        var vm = new CharacterDetailViewModel(request, PullsOf(request.RoleId), language, encore, images, localization, launcher);
        await dialogs.ShowAsync(vm);
    }

    /// <summary>当前账号抽到该角色的记录，从早到晚。</summary>
    private List<CharacterPullRow> PullsOf(int roleId)
    {
        if (pulls.Snapshot is not { } snapshot || snapshot.Uid != pulls.Uid) return [];
        return FiveStarHistory.All(snapshot)
            .Where(c => c.ResourceId == roleId)
            .Select(c =>
            {
                PityRating rating = PityRatings.Of(c.Pity);
                return new CharacterPullRow(
                    c.Time?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "—",
                    c.Pity,
                    localization[PoolNames.TitleKey(c.Pool)],
                    PityRatings.Brush(rating),
                    localization[PityRatings.NameKey(rating)],
                    c.PityRatio,
                    c.IsGuaranteed, c.IsSoftPity, c.IsLost, c.IsStandardPool);
            })
            .ToList();
    }
}
