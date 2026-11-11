using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Localization;

namespace WwTool.UI.Feedback;

/// <summary>
/// 窗口内对话框的公共契约，供覆盖层处理 Escape / Enter。
/// </summary>
public interface IDialogViewModel
{
    /// <summary>Escape 或点遮罩：按“取消”结束。</summary>
    void Cancel();

    /// <summary>Enter：执行默认操作；当前不可确认时返回 false。</summary>
    bool TryAccept();
}

/// <summary>
/// 有返回值的对话框视图模型。子类在合适时机调用 <see cref="Close"/>。
/// </summary>
public abstract class DialogViewModel<TResult> : ObservableObject, IDialogViewModel
{
    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>对话框结束时完成。</summary>
    public Task<TResult> Result => _completion.Task;

    /// <summary>取消时的返回值。</summary>
    protected abstract TResult CancelResult { get; }

    /// <summary>按“取消”结束；需要中止进行中工作的对话框可重写（记得调用基类）。</summary>
    public virtual void Cancel() => Close(CancelResult);

    public abstract bool TryAccept();

    protected void Close(TResult result) => _completion.TrySetResult(result);
}

/// <summary>
/// 确认 / 提示对话框。危险操作用 <see cref="IsDanger"/> 显示危险样式。
/// </summary>
public sealed partial class ConfirmDialogViewModel : DialogViewModel<bool>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string ConfirmText { get; init; }

    /// <summary>为空时只显示确认按钮（提示框）。</summary>
    public string? CancelText { get; init; }

    public bool IsDanger { get; init; }

    /// <summary>正文后的 Danger Bold 警示行（如「该操作不可逆！」）。</summary>
    public string? Warning { get; init; }

    /// <summary>危险说明框文字（如删除账号会一并清除的数据）。</summary>
    public string? DangerNote { get; init; }

    /// <summary>按钮行上边距：普通确认框为 34，带危险说明框时为 18（定稿）。</summary>
    public Avalonia.Thickness ActionsMargin => new(0, string.IsNullOrEmpty(DangerNote) ? 34 : 18, 0, 0);

    public bool HasCancel => !string.IsNullOrEmpty(CancelText);

    protected override bool CancelResult => false;

    public override bool TryAccept()
    {
        Close(true);
        return true;
    }

    [RelayCommand]
    private void Confirm() => Close(true);

    [RelayCommand]
    private void Dismiss() => Cancel();
}

/// <summary>
/// 窗口内覆盖层对话框。同一时间只显示一个，后来的排队等待。
/// </summary>
public interface IDialogService
{
    /// <summary>显示任意对话框视图模型（其视图需在 ViewLocator 中登记），返回对话框结果。</summary>
    Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog);

    /// <summary>确认框；确认返回 true，取消、Escape 或点遮罩返回 false。</summary>
    Task<bool> ConfirmAsync(string title, string message, bool isDanger = false, string? confirmText = null);

    /// <summary>确认框的完整形式，可带警示行与危险说明框。</summary>
    Task<bool> ConfirmAsync(ConfirmDialogViewModel dialog);

    /// <summary>只有“确定”的提示框。</summary>
    Task AlertAsync(string title, string message);
}

/// <summary>
/// <see cref="IDialogService"/> 的实现；<see cref="Current"/> 由 Shell 的覆盖层绑定显示。只在 UI 线程调用。
/// </summary>
public sealed partial class DialogService(LocalizationService localization) : ObservableObject, IDialogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>当前显示的对话框视图模型；为空时覆盖层隐藏。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private IDialogViewModel? _current;

    public bool IsOpen => Current is not null;

    public async Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> dialog)
    {
        await _gate.WaitAsync();
        try
        {
            Current = dialog;
            return await dialog.Result;
        }
        finally
        {
            Current = null;
            _gate.Release();
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, bool isDanger = false, string? confirmText = null) =>
        ShowAsync(new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            IsDanger = isDanger,
            ConfirmText = confirmText ?? localization["Common_Confirm"],
            CancelText = localization["Common_Cancel"],
        });

    public Task<bool> ConfirmAsync(ConfirmDialogViewModel dialog) => ShowAsync(dialog);

    public Task AlertAsync(string title, string message) =>
        ShowAsync(new ConfirmDialogViewModel
        {
            Title = title,
            Message = message,
            ConfirmText = localization["Common_Ok"],
        });
}
