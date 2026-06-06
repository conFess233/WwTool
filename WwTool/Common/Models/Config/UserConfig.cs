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
        private string? _searchGachaApiUrl = "https://aki-gm-resources-oversea.aki-game.net/aki/gacha/index.html#/record?";
        private bool _autoLoadLocalData = true;
        private bool _isGlassEffectEnabled = true;
        private string _currentTheme = "DarkTheme"; // 默认加载深色主题
        private LanguageType _appLanguage = LanguageType.ZhHans;

        public string? GamePath { get => _gamePath; set => SetProperty(ref _gamePath, value); }
        public string? LastUserId { get => _lastUserId; set => SetProperty(ref _lastUserId, value); }
        public string? SearchGachaApiUrl { get => _searchGachaApiUrl; set => SetProperty(ref _searchGachaApiUrl, value); }
        public bool AutoLoadLocalData { get => _autoLoadLocalData; set => SetProperty(ref _autoLoadLocalData, value); }
        public bool IsGlassEffectEnabled
        {
            get => _isGlassEffectEnabled;
            set
            {
                SetProperty(ref _isGlassEffectEnabled, value);
            }
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
    }
}
