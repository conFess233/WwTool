using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WwTool.Common.Enums;
using WwTool.Extensions;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 全局多语言管理器，提供动态语言切换与基于键的字符串获取能力
    /// 实现了 INotifyPropertyChanged 接口，支持 WPF 的动态 UI 绑定
    /// </summary>
    public class LanguageManager : INotifyPropertyChanged
    {
        private static readonly Lazy<LanguageManager> _instance = new(() => new LanguageManager());
        public static LanguageManager Instance => _instance.Value;

        private Dictionary<string, string> _dict = new Dictionary<string, string>();
        /// <summary>
        /// 当前系统的运行语言
        /// </summary>
        public LanguageType CurrentLanguage { get; private set; } = LanguageType.ZhHans;

        private LanguageManager()
        {
        }

        /// <summary>
        /// 多语言文本索引器
        /// 通过语言字典中的键名获取对应的文本值
        /// </summary>
        /// <param name="key">多语言标识键</param>
        /// <returns>当前语言对应的文本，若不存在则返回键名本身</returns>
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                return _dict.TryGetValue(key, out var val) ? val : key;
            }
        }

        /// <summary>
        /// 切换应用程序的当前语言
        /// 加载对应语言的 JSON 配置文件，并触发 UI 刷新通知
        /// </summary>
        /// <param name="type">目标语言枚举类型</param>
        public void ChangeLanguage(LanguageType type)
        {
            CurrentLanguage = type;
            string code = type.GetCode();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local/Language", $"{code}.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        _dict = dict;
                    }
                }
                catch
                {
                    // 如果失败则回退为空字典
                    _dict = new Dictionary<string, string>();
                }
            }
            else
            {
                _dict = new Dictionary<string, string>();
            }

            // 通知所有索引器绑定进行更新
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(CurrentLanguage));
        }

        /// <summary>
        /// 获取指定键的多语言文本
        /// </summary>
        public string GetString(string key)
        {
            return this[key];
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
