using Microsoft.Extensions.DependencyInjection;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.UI.Feedback;
using WwTool.UI.Pages.Accounts;

namespace WwTool.UI.Services;

/// <summary>
/// 账号的增删操作，首页悬浮按钮、账号页、角色页（攻略站要求登录时）共用。
/// </summary>
public sealed class AccountActions(
    IServiceProvider services,
    IDialogService dialogs,
    AccountContext accounts,
    IUserDataService userData,
    LocalizationService localization,
    IUIStateService uiState,
    ErrorReporter errors)
{
    /// <summary>打开登录对话框；登录成功后刷新账号列表并返回 true。</summary>
    public async Task<bool> AddAccountAsync()
    {
        var dialog = ActivatorUtilities.CreateInstance<LoginDialogViewModel>(services);
        if (!await dialogs.ShowAsync(dialog)) return false;
        try
        {
            await accounts.RefreshAsync();
        }
        catch (Exception ex)
        {
            errors.Report(ex, localization["Msg_LoadingLocalAccount"]);
        }
        uiState.ShowToast(new NotificationRequest
        {
            Title = localization["Login_SuccessTitle"],
            Message = localization["Login_SuccessMsg"],
            Type = NotificationType.Success,
            Priority = NotificationPriority.Important,
            Source = "Accounts",
        });
        return true;
    }

    /// <summary>确认后删除账号及其全部本地数据（抽卡记录、快照、凭据）。不可恢复。</summary>
    public async Task<bool> DeleteAsync(AccountSummary account)
    {
        string name = string.IsNullOrWhiteSpace(account.Name) ? account.Uid : account.Name;
        bool confirmed = await dialogs.ConfirmAsync(
            localization["Dialog_DeleteUser"],
            string.Format(localization["Dialog_DeleteUserConfirm"], name, account.Uid),
            isDanger: true,
            confirmText: localization["Accounts_Delete"]);
        if (!confirmed) return false;
        try
        {
            // 删除成功后 IUserDataService 触发 AccountDeleted，AccountContext 会移除该账号并切换当前账号。
            await userData.DeleteAccountAsync(account.Uid);
            uiState.ShowToast(new NotificationRequest
            {
                Title = localization["Toast_Success"],
                Message = string.Format(localization["Accounts_Deleted"], name),
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Accounts",
            });
            return true;
        }
        catch (Exception ex)
        {
            errors.Report(ex, localization["Dialog_DeleteUser"]);
            return false;
        }
    }
}
