using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Localization;
using WwTool.Core.Services;
using WwTool.UI.Feedback;

namespace WwTool.UI.Pages.Accounts;

/// <summary>
/// 添加游戏账号（邮箱 + 密码登录）。成功后结果为 true；密码只在对话框存活期间保留在内存里。
/// 登录中途取消会中止请求与极验等待。
/// </summary>
public sealed partial class LoginDialogViewModel : DialogViewModel<bool>
{
    private readonly IAccountLoginService _login;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly CancellationTokenSource _lifetime = new();

    public LoginDialogViewModel(IAccountLoginService login, LocalizationService localization, IUIStateService uiState, ErrorReporter errors)
    {
        _login = login;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _error;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    public bool IsIdle => !IsBusy;

    protected override bool CancelResult => false;

    public override bool TryAccept()
    {
        if (IsBusy) return false;
        _ = LoginAsync();
        return true;
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            Error = _localization["Login_EmailEmpty"];
            return;
        }
        if (string.IsNullOrEmpty(Password))
        {
            Error = _localization["Login_PwdEmpty"];
            return;
        }

        Error = null;
        IsBusy = true;
        try
        {
            await _login.LoginAsync(Email.Trim(), Password, () => _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Login_RiskTitle"],
                Message = _localization["Login_RiskMsg"],
                Type = NotificationType.Warning,
                Priority = NotificationPriority.Important,
                Source = "Login",
            }), _lifetime.Token);
            Password = string.Empty;
            Close(true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // 用户取消。
        }
        catch (Exception ex)
        {
            // 错误留在对话框内显示，便于改正后重试；仍写日志。
            ErrorReport? report = _errors.Report(ex, _localization["Dialog_AddAccount"], notifyUser: false);
            Error = ex is WwToolAuthException ? ex.Message : report?.Message ?? ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Dismiss() => Cancel();

    /// <summary>取消按钮、Escape、点遮罩都走这里：中止登录请求与极验等待，清掉密码。</summary>
    public override void Cancel()
    {
        _lifetime.Cancel();
        Password = string.Empty;
        base.Cancel();
    }
}
