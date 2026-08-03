using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WwTool.Common.Enums;

namespace WwTool.Common.Models.Config
{
    public class UserConfig : BindableBase
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

        public string? GamePath { get => _gamePath; set => SetProperty(ref _gamePath, value); }
        public string? LastUserId { get => _lastUserId; set => SetProperty(ref _lastUserId, value); }
        public string? SearchGachaApiUrl { get => _searchGachaApiUrl; set => SetProperty(ref _searchGachaApiUrl, value); }
        public bool AutoLoadLocalData { get => _autoLoadLocalData; set => SetProperty(ref _autoLoadLocalData, value); }
        public GachaServerRegion GachaServerRegion { get => _gachaServerRegion; set => SetProperty(ref _gachaServerRegion, value); }
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
            set => SetProperty(ref _glassOpacity, Math.Clamp(value, 65, 95));
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
        public bool IsReducedMotionEnabled
        {
            get => _isReducedMotionEnabled;
            set => SetProperty(ref _isReducedMotionEnabled, value);
        }
    }
}
