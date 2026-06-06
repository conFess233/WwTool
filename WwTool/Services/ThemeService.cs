using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 主题服务
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly IConfigService _configService;

        public ThemeService(IConfigService configService)
        {
            _configService = configService;
        }


        /// <summary>
        /// 初始化主题监听。在 App 启动时调用一次。
        /// </summary>
        public void Initialize()
        {
            // 启动时先应用一次本地保存的主题
            ApplyTheme(_configService.User.CurrentTheme);

            _configService.User.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_configService.User.CurrentTheme))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ApplyTheme(_configService.User.CurrentTheme);
                    });
                }
            };
        }

        /// <summary>
        /// 动态替换颜色资源字典
        /// </summary>
        private void ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName)) return;

            // 获取全局资源字典集合
            var mergedDicts = Application.Current.Resources.MergedDictionaries;

            // 寻找当前正在生效的颜色主题字典
            var oldThemeDict = mergedDicts.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.Contains("/UI/Resources/Themes/Colors/"));

            // 构造新主题字典
            var newThemeUri = new Uri($"/UI/Resources/Themes/Colors/{themeName}.xaml", UriKind.Relative);
            var newThemeDict = new ResourceDictionary { Source = newThemeUri };

            // 替换字典
            if (oldThemeDict != null)
            {
                int index = mergedDicts.IndexOf(oldThemeDict);
                mergedDicts.RemoveAt(index);
                mergedDicts.Insert(index, newThemeDict);
            }
            else
            {
                mergedDicts.Insert(0, newThemeDict);
            }
        }
    }
}
