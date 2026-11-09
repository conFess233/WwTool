using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.UI.Navigation;

namespace WwTool.UI.Pages.Pulls;

/// <summary>
/// 抽卡统计的“详细图表”子页面。图表在下一步接入 LiveCharts。
/// </summary>
public sealed partial class PullsChartsViewModel(PullsSession session, INavigationService navigation) : ObservableObject, INavigationAware
{
    public const string PageKey = "pulls-charts";

    public PullsSession Session { get; } = session;

    public void OnNavigatedTo(object? parameter) => _ = Session.EnsureLoadedAsync();

    public void OnNavigatedFrom() { }

    [RelayCommand]
    private void Back() => navigation.NavigateTo("pulls");
}
