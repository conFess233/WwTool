using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WwTool.Core.Models.Config
{
    /// <summary>
    /// API 配置信息
    /// </summary>
    public class ApiConfig
    {
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 5;
        public int DelayMs { get; set; } = 1000;

        public FixedParams FixedParams { get; set; } = new FixedParams();
        public Servers Servers { get; set; } = new Servers();
        public CommonHeaders CommonHeaders { get; set; } = new CommonHeaders();
        public Urls Urls { get; set; } = new Urls();

    }


    public class FixedParams
    {
        public string ClientId { get; set; } = "7rxmydkibzzsf12om5asjnoo";
        public string ClientSecret { get; set; } = "32gh5r0p35ullmxrzzwk40ly";
        public string ProductKey { get; set; } = "5c063821193f41e09f1c4fdd7567dda3";
        public string H5GoogleClientId { get; set; } = "1082547014227-2nktfupu0taqjea6eaheuhoqign2fjn4.apps.googleusercontent.com";

        public string Platform { get; set; } = "PC";
        public string ProjectId { get; set; } = "G153";
        public string SdkVersion { get; set; } = "2.6.0h";
        public string Area { get; set; } = "Mcn";
        public string RedirectUri { get; set; } = "1";
        public string ResponseType { get; set; } = "code";
        public string GrantType { get; set; } = "authorization_code";
        public string DefaultE { get; set; } = "1";
        public string LauncherScope { get; set; } = "launcher";
        public int LoginType { get; set; } = 1;

        public string LoginProductId { get; set; } = "A1730";
        public string AuthProductId { get; set; } = "A1725";

        public string AutoLoginChannelId { get; set; } = "171";
        public string EmailLoginChannelId { get; set; } = "240";
    }

    public class Servers
    {
        public string Asia { get; set; } = "86d52186155b148b5c138ceb41be9650";
        public string America { get; set; } = "591d6af3a3090d8ea00d8f86cf6d7501";
        public string Europe { get; set; } = "6eb2a235b30d05efd77bedb5cf60999e";
        public string HMT { get; set; } = "919752ae5ea09c1ced910dd668a63ffb";
        public string SEA { get; set; } = "10cd7254d57e58ae560b15d51e34b4c8";
    }

    public class CommonHeaders
    {

        public string AcceptEncoding { get; set; } = "gzip, deflate, br, zstd";
        public string AcceptLanguage { get; set; } = "zh-Hans";
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36";
        public string H5Source { get; set; } = "h5";
        public string DefaultContentType { get; set; } = "application/json";
    }
    public class Urls
    {
        // 国际服抽卡记录链接
        public string GachaUrlNET { get; set; } = "https://gmserver-api.aki-game2.net/gacha/record/query";
        // 国服抽卡记录链接
        public string GachaUrlCN { get; set; } = "https://gmserver-api.aki-game2.com/gacha/record/query";
        public string EmailLoginUrl { get; set; } = "https://sdkapi.kurogame-service.com/sdkcom/v2/login/emailPwd.lg";
        public string GenerateUrl { get; set; } = "https://sdkapi.kurogame-service.com/sdkcom/v2/user/oauth/code/generate.lg";
        public string GetTokenUrl { get; set; } = "https://sdkapi.kurogame-service.com/sdkcom/v2/auth/getToken.lg";
        public string AutoLoginUrl { get; set; } = "https://sdkapi.kurogame-service.com/sdkcom/v2/login/auto.lg";
        public string GetUserInfoUrl { get; set; } = "https://gar-service.aki-game.net/UserRegion/GetUserInfo";
        public string QueryRoleUrl { get; set; } = "https://pc-launcher-sdk-api.kurogame.net/game/queryRole";
        public string QueryPlayerInfoUrl { get; set; } = "https://pc-launcher-sdk-api.kurogame.net/game/queryPlayerInfo";
        public string H5LoginUrl { get; set; } = "https://api.kurobbs.com/user/loginForH5";
        public string GuideBaseUrl { get; set; } = "https://guide-server.aki-game.net";
        public string GuideFallbackBaseUrl { get; set; } = "https://guide-server-1.aki-game.net";
        /// <summary>首页「官方攻略站」打开的网页。</summary>
        public string GuideSiteUrl { get; set; } = "https://wiki.kurobbs.com/mc/home";
        /// <summary>首页公告卡「前往官网查看全部公告」的默认网页。</summary>
        public string OfficialSiteUrl { get; set; } = "https://mc.kurogame.com/";

        /// <summary>国际服启动器背景配置，<c>{0}</c> 为语言代码（zh-Hans / en / ja 等均有）。</summary>
        public string LauncherBackgroundUrlNET { get; set; } = "https://prod-alicdn-gamestarter.kurogame.com/launcher/game/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/G153/official/background/51684874bd1f48e3ab98fb8b06b96ceb/{0}.json";

        /// <summary>国际服启动器资讯（活动 / 公告 / 新闻、轮播图），<c>{0}</c> 为语言代码。</summary>
        public string LauncherNewsUrlNET { get; set; } = "https://prod-alicdn-gamestarter.kurogame.com/launcher/app/50004_P7xcUZnEr1AXIGON25E6KjpOgTlVrg6e/G153/official/information/{0}.json";

        /// <summary>国服启动器资讯，<c>{0}</c> 为语言代码（只有 zh-Hans）。</summary>
        public string LauncherNewsUrlCN { get; set; } = "https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/app/10003_oLNgHF1CESo51DGHN2odtp40e3oI1HfZ/G152/official/information/{0}.json";

        /// <summary>官网新闻页（「前往官网查看全部公告」）：国服。</summary>
        public string NewsPageUrlCN { get; set; } = "https://mc.kurogames.com/main/news/";

        /// <summary>官网新闻页：国际服，<c>{0}</c> 为官网语言路径（en / ja / zh-tw）。</summary>
        public string NewsPageUrlNET { get; set; } = "https://wutheringwaves.kurogames.com/{0}/main/news/";

        /// <summary>国服启动器背景配置，<c>{0}</c> 为语言代码（只有 zh-Hans）。</summary>
        public string LauncherBackgroundUrlCN { get; set; } = "https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/game/10003_oLNgHF1CESo51DGHN2odtp40e3oI1HfZ/G152/official/background/e4ea009e702a4961be86c815d87dbe68/{0}.json";
    }
}
