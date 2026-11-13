using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WwTool.Core.Enums;

namespace WwTool.Core.Models.Config
{
    public class UserConfig : ObservableObject
    {
        private string? _gamePath;
        private string? _lastUserId;
        private string? _searchGachaApiUrl = "aki/gacha/index.html#/record?";
        private bool _autoLoadLocalData = true;
        private bool _isGlassEffectEnabled = true;
        private int _glassOpacity = 80;
        private string _currentTheme = "DarkTheme"; // 默认加载深色主题
        private string _baseTheme = "DeepBlue";
        private string _accentTheme = "FollowTheme";
        private LanguageType _appLanguage = LanguageType.ZhHans;
        private bool _isReducedMotionEnabled;
        private GachaServerRegion _gachaServerRegion = GachaServerRegion.China;
        private NotificationDisplayMode _notificationDisplayMode = NotificationDisplayMode.Concise;
        private bool _useCustomBackground;
        private string? _customBackgroundPath;
        private bool _useDynamicBackground;
        private int _backgroundPeekTransparency = 80;
        private string? _announcementRegion;
        private bool _pullsCompactView;

        public string? GamePath { get => _gamePath; set => SetProperty(ref _gamePath, value); }
        public string? LastUserId { get => _lastUserId; set => SetProperty(ref _lastUserId, value); }
        public string? SearchGachaApiUrl { get => _searchGachaApiUrl; set => SetProperty(ref _searchGachaApiUrl, value); }
        public bool AutoLoadLocalData { get => _autoLoadLocalData; set => SetProperty(ref _autoLoadLocalData, value); }
        public GachaServerRegion GachaServerRegion { get => _gachaServerRegion; set => SetProperty(ref _gachaServerRegion, value); }
        public NotificationDisplayMode NotificationDisplayMode { get => _notificationDisplayMode; set => SetProperty(ref _notificationDisplayMode, value); }
        public bool IsGlassEffectEnabled
        {
            get => _isGlassEffectEnabled;
            set
            {
                SetProperty(ref _isGlassEffectEnabled, value);
            }
        }
        public int GlassOpacity
        {
            get => _glassOpacity;
            set => SetProperty(ref _glassOpacity, Math.Clamp(value, 10, 95));
        }
        public string CurrentTheme
        {
            get => _currentTheme;
            set => SetProperty(ref _currentTheme, value);
        }
        public LanguageType AppLanguage
        {
            get => _appLanguage;
            set => SetProperty(ref _appLanguage, value);
        }
        public string BaseTheme { get => _baseTheme; set => SetProperty(ref _baseTheme, value); }
        public string AccentTheme { get => _accentTheme; set => SetProperty(ref _accentTheme, value); }
        /// <summary>窗口背景：false 用默认底纹，true 用 <see cref="CustomBackgroundPath"/> 指向的图片。</summary>
        public bool UseCustomBackground { get => _useCustomBackground; set => SetProperty(ref _useCustomBackground, value); }
        public string? CustomBackgroundPath { get => _customBackgroundPath; set => SetProperty(ref _customBackgroundPath, value); }

        /// <summary>动态背景：使用启动器背景时播放其背景视频（仅 Windows），默认关闭。</summary>
        public bool UseDynamicBackground { get => _useDynamicBackground; set => SetProperty(ref _useDynamicBackground, value); }

        /// <summary>鼠标停在空白处时，背景以外的界面变透明以便查看背景；值为透明度百分比（0 关闭，默认 80）。</summary>
        public int BackgroundPeekTransparency
        {
            get => _backgroundPeekTransparency;
            set => SetProperty(ref _backgroundPeekTransparency, Math.Clamp(value, 0, 100));
        }

        /// <summary>首页游戏公告的区服：「CN」国服、「Global」国际服；为空时按当前账号自动选择。</summary>
        public string? AnnouncementRegion { get => _announcementRegion; set => SetProperty(ref _announcementRegion, value); }
        /// <summary>抽卡统计单卡池记录用「精简」（头像格子）视图，否则为「列表」。</summary>
        public bool PullsCompactView { get => _pullsCompactView; set => SetProperty(ref _pullsCompactView, value); }
        public bool IsReducedMotionEnabled
        {
            get => _isReducedMotionEnabled;
            set => SetProperty(ref _isReducedMotionEnabled, value);
        }
    }
}
