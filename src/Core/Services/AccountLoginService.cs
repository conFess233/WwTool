using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Exceptions;
using WwTool.Core.Infrastructure;
using WwTool.Core.Models.Api.Requests;
using WwTool.Core.Models.Api.Responses;

namespace WwTool.Core.Services;

/// <summary>邮箱登录添加游戏账号：登录 → （需要时）极验验证 → 生成授权码 → 同步账号数据到本地。</summary>
public interface IAccountLoginService
{
    /// <param name="onCaptchaRequired">触发风控、即将打开浏览器验证时回调（用于提示用户）。</param>
    /// <exception cref="WwToolAuthException">登录、验证或授权失败。</exception>
    Task LoginAsync(string email, string password, Action? onCaptchaRequired, CancellationToken cancellationToken = default);
}

public sealed class AccountLoginService(
    ILoginService login,
    IGetDataService getData,
    IConfigService config,
    IAppLauncher launcher,
    ILocalizer text) : IAccountLoginService
{
    /// <summary>接口返回此码表示需要极验验证。</summary>
    private const int CaptchaRequiredCode = 41000;

    public async Task LoginAsync(string email, string password, Action? onCaptchaRequired, CancellationToken cancellationToken = default)
    {
        var request = new EmailLoginRequest
        {
            Email = email,
            Password = Crypto.EncodePassword(password),
        };
        EmailLoginResponse response = await login.EmailLoginAsync(request, cancellationToken)
            ?? throw new WwToolAuthException(text["Login_NoData"]);

        if (response.Codes == CaptchaRequiredCode)
        {
            onCaptchaRequired?.Invoke();
            Dictionary<string, string> captcha = await GeetestServer.SolveGeetestAsync(launcher, config.App.GeetestPort, cancellationToken);
            if (captcha.Count == 0) throw new WwToolAuthException(text["Login_GeetestFail"]);
            request.GeetestCaptchaOutput = captcha.GetValueOrDefault("captcha_output");
            request.GeetestGenTime = captcha.GetValueOrDefault("gen_time");
            request.GeetestLotNumber = captcha.GetValueOrDefault("lot_number");
            request.GeetestPassToken = captcha.GetValueOrDefault("pass_token");
            response = await login.EmailLoginAsync(request, cancellationToken)
                ?? throw new WwToolAuthException(text["Login_GeetestNoData"]);
        }

        if (response.Codes != 0)
        {
            throw new WwToolAuthException(string.IsNullOrEmpty(response.ErrorDescription) ? string.Format(text["Login_AuthFail"], response.Codes) : response.ErrorDescription);
        }

        GenerateResponse? oauth = await login.GenerateAsync(new GenerateRequest(), cancellationToken);
        if (oauth is null || oauth.Codes != 0 || string.IsNullOrEmpty(oauth.OauthCode))
        {
            throw new WwToolAuthException(string.IsNullOrEmpty(oauth?.ErrorDescription) ? text["Login_GenerateFail"] : oauth.ErrorDescription);
        }

        // 同步授权码关联的全部游戏角色，写入本地账号列表。
        await getData.SyncAllUserDataAsync(oauthCode: oauth.OauthCode, cancellationToken: cancellationToken);
    }
}
