using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using WwTool.UI.Shell;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // 主题色块与强调色（含「跟随主题」）不直接绑命令：先截下旧主题画面，再切换，见动效「主题切换」。
        AddHandler(Button.ClickEvent, OnSwatchClick, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private async void OnSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control source || DataContext is not SettingsViewModel vm) return;
        // ToggleButton 也是 Button。
        Button? button = source as Button ?? source.FindAncestorOfType<Button>();
        if (button?.DataContext is not ThemeSwatch swatch) return;
        bool isPreset = vm.Presets.Contains(swatch);
        // 「跟随主题」胶囊：点击会自行切换勾选，按当前选中状态复原，不破坏绑定。
        if (button is Avalonia.Controls.Primitives.ToggleButton toggle) toggle.SetCurrentValue(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, swatch.IsSelected);
        if (swatch.IsSelected) return;

        if (TopLevel.GetTopLevel(this) is MainWindow window
            && button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window) is { } origin)
        {
            await window.BeginThemeRevealAsync(origin);
        }
        if (isPreset) vm.SelectPresetCommand.Execute(swatch);
        else vm.SelectAccentCommand.Execute(swatch);
    }
}
