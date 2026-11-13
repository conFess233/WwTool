using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace WwTool.UI.Pages.Events;

public partial class ChallengesView : UserControl
{
    private ChallengesViewModel? _viewModel;

    public ChallengesView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as ChallengesViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
    }

    // 换玩法或换一期时内容回到顶部。
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChallengesViewModel.Season) or nameof(ChallengesViewModel.IsTower)
            or nameof(ChallengesViewModel.IsWastes) or nameof(ChallengesViewModel.IsMatrix))
            ContentScroller.Offset = new Vector(0, 0);
    }
}
