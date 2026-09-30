using System.Collections.ObjectModel;
using WwTool.Common.Models.Domain;

namespace WwTool.UI.ViewModels;

/// <summary>在 UI 线程刷新账号列表并选择首选账号，不触发加载或修改配置。</summary>
internal static class AccountList
{
    internal static AccountSummary? Refresh(
        ObservableCollection<AccountSummary> target,
        IReadOnlyList<AccountSummary> accounts,
        string? preferredUid = null)
    {
        // 先读取首选项，避免控件在清空集合时改变当前选择。
        AccountSummary? selected = string.IsNullOrEmpty(preferredUid)
            ? accounts.FirstOrDefault()
            : accounts.FirstOrDefault(x => x.Uid == preferredUid) ?? accounts.FirstOrDefault();
        target.Clear();
        foreach (AccountSummary account in accounts)
            target.Add(account);
        return selected;
    }
}
