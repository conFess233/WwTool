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
    private readonly ErrorReporter _errors;
    private readonly CancellationTokenSource _lifetime = new();

    public LoginDialogViewModel(IAccountLoginService login, LocalizationService localization, ErrorReporter errors)
    {
        _login = login;
        _localization = localization;
        _errors = errors;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _error;

    /// <summary>出错的输入框：用于抖动与红色描边。</summary>
    [ObservableProperty] private LoginField _errorField;

    /// <summary>触发风控、正在网页里做安全验证（密码下方显示提示条）。</summary>
    [ObservableProperty] private bool _isVerifying;

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
            ShowError(_localization["Login_EmailEmpty"], LoginField.Email);
            return;
        }
        if (string.IsNullOrEmpty(Password))
        {
            ShowError(_localization["Login_PwdEmpty"], LoginField.Password);
            return;
        }

        Error = null;
        ErrorField = LoginField.None;
        IsBusy = true;
        try
        {
            // 触发风控时在对话框内显示「正在唤起网页进行安全验证」提示条。
            await _login.LoginAsync(Email.Trim(), Password,
                () => Avalonia.Threading.Dispatcher.UIThread.Post(() => IsVerifying = true), _lifetime.Token);
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
            ShowError(ex is WwToolAuthException ? ex.Message : report?.Message ?? ex.Message,
                ex is WwToolAuthException ? LoginField.Password : LoginField.None);
        }
        finally
        {
            IsBusy = false;
            IsVerifying = false;
        }
    }

    /// <summary>每次出错都触发一次（即使文字相同），视图据此抖动对应输入框。</summary>
    public event EventHandler? ErrorRaised;

    private void ShowError(string message, LoginField field)
    {
        Error = message;
        ErrorField = field;
        ErrorRaised?.Invoke(this, EventArgs.Empty);
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

public enum LoginField
{
    None,
    Email,
    Password,
}
