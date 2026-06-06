using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using WwTool.Common.Context;
using WwTool.Common.Enums;
using WwTool.Common.Events;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels.Dialogs
{
    /// <summary>
    /// 登录弹窗视图模型
    /// 处理邮箱登录、极验风控验证、授权码生成及用户数据同步的完整登录流程
    /// </summary>
    public class LoginViewModel : BindableBase, IDialogAware
    {
        /// <summary>
        /// 配置服务
        /// </summary>
        private readonly IConfigService _configService;
        /// <summary>
        /// 登录服务，处理邮箱登录和 Token 自动登录
        /// </summary>
        private readonly ILoginService _loginService;
        /// <summary>
        /// UI 状态服务
        /// </summary>
        private readonly IUIStateService _uiStateService;
        /// <summary>
        /// 数据获取服务
        /// </summary>
        private readonly IGetDataService _getDataService;
        /// <summary>
        /// 本地数据库服务
        /// </summary>
        private readonly LocalDataService _localDb;
        /// <summary>
        /// 事件聚合器，控制背景模糊
        /// </summary>
        private readonly IEventAggregator _eventAggregator;

        /// <summary>
        /// 弹窗标题
        /// </summary>
        private string _title = LanguageManager.Instance["Dialog_AddAccount"];
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 用户输入的邮箱地址
        /// </summary>
        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        /// <summary>
        /// 用户输入的密码
        /// </summary>
        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// 登录操作是否正在执行，用于控制输入框和按钮的启用状态
        /// </summary>
        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaisePropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        public bool IsNotBusy => !IsBusy;

        /// <summary>
        /// 登录错误提示消息
        /// </summary>
        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// 登录命令
        /// </summary>
        public DelegateCommand LoginCommand { get; }
        /// <summary>
        /// 取消命令
        /// </summary>
        public DelegateCommand CancelCommand { get; }

        private DialogCloseListener RequestClose { get; }
        DialogCloseListener IDialogAware.RequestClose => RequestClose;

        public LoginViewModel(
            IConfigService configService,
            ILoginService loginService,
            IUIStateService uiStateService,
            IGetDataService getDataService,
            LocalDataService localDb,
            IEventAggregator eventAggregator)
        {
            _configService = configService;
            _loginService = loginService;
            _uiStateService = uiStateService;
            _getDataService = getDataService;
            _localDb = localDb;
            _eventAggregator = eventAggregator;

            LoginCommand = new DelegateCommand(LoginAsync, () => !IsBusy)
                .ObservesProperty(() => IsBusy);
            CancelCommand = new DelegateCommand(Cancel);
        }

        /// <summary>
        /// 执行登录流程：邮箱登录 -> 极验验证（如有）-> 生成授权码 -> 同步用户数据
        /// </summary>
        private async void LoginAsync()
        {
            if (string.IsNullOrEmpty(Email))
            {
                ErrorMessage = LanguageManager.Instance["Login_EmailEmpty"];
                return;
            }
            if (string.IsNullOrEmpty(Password))
            {
                ErrorMessage = LanguageManager.Instance["Login_PwdEmpty"];
                return;
            }

            ErrorMessage = string.Empty;
            IsBusy = true;

            await ExceptionHelper.ExecuteAsync(async () =>
            {
                // 发起邮箱登录请求
                var request = new EmailLoginRequest
                {
                    Email = Email,
                    Password = Crypto.EncodePassword(Password)
                };

                var response = await _loginService.EmailLoginAsync(request);

                if (response == null)
                {
                    throw new WwToolAuthException(LanguageManager.Instance["Login_NoData"]);
                }

                // 极验风控验证处理
                if (response.Codes == 41000)
                {
                    _uiStateService.ShowToast(LanguageManager.Instance["Login_RiskTitle"], LanguageManager.Instance["Login_RiskMsg"], NotificationType.Warning);

                    var geetestData = await GeetestServer.SolveGeetestAsync(_configService.App.GeetestPort);

                    if (geetestData == null || geetestData.Count == 0)
                    {
                        throw new WwToolAuthException(LanguageManager.Instance["Login_GeetestFail"]);
                    }

                    if (geetestData.TryGetValue("captcha_output", out var captchaOutput))
                        request.GeetestCaptchaOutput = captchaOutput;
                    if (geetestData.TryGetValue("gen_time", out var genTime))
                        request.GeetestGenTime = genTime;
                    if (geetestData.TryGetValue("lot_number", out var lotNumber))
                        request.GeetestLotNumber = lotNumber;
                    if (geetestData.TryGetValue("pass_token", out var passToken))
                        request.GeetestPassToken = passToken;

                    response = await _loginService.EmailLoginAsync(request);

                    if (response == null)
                    {
                        throw new WwToolAuthException(LanguageManager.Instance["Login_GeetestNoData"]);
                    }
                }

                if (response.Codes != 0)
                {
                    throw new WwToolAuthException(response.ErrorDescription ?? string.Format(LanguageManager.Instance["Login_AuthFail"], response.Codes));
                }

                // 生成授权码
                var oauthResponse = await _loginService.GenerateAsync(new GenerateRequest());
                if (oauthResponse == null || oauthResponse.Codes != 0 || string.IsNullOrEmpty(oauthResponse.OauthCode))
                {
                    throw new WwToolAuthException(oauthResponse?.ErrorDescription ?? LanguageManager.Instance["Login_GenerateFail"]);
                }

                // 同步玩家关联角色数据并更新到本地数据库
                await _getDataService.SyncAllUserDataAsync(oauthCode: oauthResponse.OauthCode);

                _uiStateService.ShowToast(LanguageManager.Instance["Login_SuccessTitle"], LanguageManager.Instance["Login_SuccessMsg"], NotificationType.Success);

                // 登录成功，关闭弹窗并返回 OK
                RequestClose.Invoke(new DialogResult { Result = ButtonResult.OK });
            }, "登录游戏账号");

            IsBusy = false;
        }

        /// <summary>
        /// 取消登录，关闭弹窗
        /// </summary>
        private void Cancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => !IsBusy;

        /// <summary>
        /// 弹窗打开时触发，启用全局背景模糊
        /// </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            _eventAggregator.GetEvent<GlobalBlurEvent>().Publish(true);
        }

        /// <summary>
        /// 弹窗关闭时触发，取消全局背景模糊
        /// </summary>
        public void OnDialogClosed()
        {
            _eventAggregator.GetEvent<GlobalBlurEvent>().Publish(false);
        }
    }
}
